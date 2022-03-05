using System.Collections.Generic;
using System.Text;
using streamcmdproxy2.Data;
using streamcmdproxy2.Data.Models;
using streamcmdproxy2.Helpers;
using streamcmdproxy2.Helpers.Events;
using streamcmdproxy2.Youtube;
using Google.Apis.YouTube.v3.Data;
using ChatWell.YouTube;
using TwitchLib.Api;
using TwitchLib.Api.Services;
using TwitchLib.Api.Services.Events;
using TwitchLib.Api.Services.Events.LiveStreamMonitor;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Discord;
using Discord.WebSocket;

namespace streamcmdproxy2;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    private YouTubeChatClient _ytclient;

    private DiscordSocketClient _dcClient;
    private string _dcToken;
    private ulong _dcChannelId;
    private ISocketMessageChannel _dcChannel;

    private TwitchClient _twClient;
    private JoinedChannel _twChannel;
    private LiveStreamMonitorService Monitor;
    private TwitchAPI API;

    private bool _youTubeEnabled;
    private bool _discordEnabled;

    private Config _configuration;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;

        Track.TrackEvent += (sender, e) =>
        {
            TrackingEventArgs logEvent = (TrackingEventArgs)e;
            switch(logEvent.EventType)
            {
                case TrackingEventArgs.TrackingType.Info:
                    _logger.LogInformation($"[{logEvent.Source}] {logEvent.Message}");
                    break;
                case TrackingEventArgs.TrackingType.Warning:
                    _logger.LogWarning($"[{logEvent.Source}] {logEvent.Message}");
                    break;
                case TrackingEventArgs.TrackingType.Error:
                    _logger.LogError($"[{logEvent.Source}] {logEvent.Message}");
                    break;
                default:
                    _logger.LogInformation($"[{logEvent.Source}] {logEvent.Message}");
                    break;
            }
        };

        // load initial supported commands
        Command.InitialSetup();

        // Get config database
        _configuration = MongoDbContext.Instance.GetQueryableCollection<Config>(MongoDbCollections.ConfigCollection).FirstOrDefault();

        // -- CONFIG --
        _youTubeEnabled = _configuration.EnableYoutube;
        _discordEnabled = _configuration.EnableDiscord;

        // -- SETUP YOUTUBE --
        if (_youTubeEnabled)
        {
            var googleCredentialsFile = _configuration.GoogleCredentialsFile;
            var youtubeUserEmail = _configuration.YoutubeUserEmail;

            _ytclient = new YouTubeChatClient(new YoutubeAuthService(googleCredentialsFile, youtubeUserEmail));
            _ytclient.OnConnected += OnYTConnected;
            _ytclient.OnDisconnected += OnYTDisconnected;
            _ytclient.OnMessageReceived += OnYTMessageReceivedAsync;
        }

        // -- SETUP DISCORD --
        if (_discordEnabled)
        {
            _dcToken = _configuration.DiscordToken;
            _dcClient = new DiscordSocketClient();

            _dcChannelId = 0;
            ulong.TryParse(_configuration.DiscordChannelID, out _dcChannelId);

            _dcClient.Log += DcLogAsync;
            _dcClient.Ready += DcReadyAsync;
            _dcClient.MessageReceived += DcMessageReceivedAsync;
        }

        // -- SETUP TWITCH --
        EventManager.Instance.TwitchUpateReceived += OnTwitchUpdateReceived;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_discordEnabled)
        {
            await _dcClient.LoginAsync(TokenType.Bot, _dcToken);
            await _dcClient.StartAsync();
        }
        StartTwitch();

        while (!stoppingToken.IsCancellationRequested)
        {
            //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }

        StopTwitch();
    }

    #region Youtube
    
    private void OnYTConnected(object sender, bool connected)
    {
        _logger.LogInformation("Youtube connected: {time}", DateTimeOffset.Now);
        _ytclient.SendMessageAsync("streamcmdproxy for Youtube has connected.");
    }

    private void OnYTDisconnected(object sender, bool connected)
    {
        _logger.LogInformation("Youtube disconnected: {time}", DateTimeOffset.Now);
    }

    private async void OnYTMessageReceivedAsync(object sender, LiveChatMessageListResponse raisedEvent)
    {
        // There may be more than one message delivered between polls to the YouTube API
        foreach (var liveChatMessage in raisedEvent.Items)
        {
            _logger.LogInformation($"[YT] {DateTimeOffset.Now}: {liveChatMessage.AuthorDetails.DisplayName} - {liveChatMessage.Snippet.DisplayMessage}");
            if (liveChatMessage.Snippet.DisplayMessage.StartsWith("!"))
                HandleProxyMessage(liveChatMessage.Snippet.DisplayMessage);
        }
    }

    #endregion

    #region Discord

    private Task DcLogAsync(LogMessage log)
    {
        _logger.LogInformation(log.ToString());
        return Task.CompletedTask;
    }

    // The Ready event indicates that the client has opened a
    // connection and it is now safe to access the cache.
    private Task DcReadyAsync()
    {
        _logger.LogInformation($"{_dcClient.CurrentUser} is connected!");

        return Task.CompletedTask;
    }

    // This is not the recommended way to write a bot - consider
    // reading over the Commands Framework sample.
    private async Task DcMessageReceivedAsync(SocketMessage message)
    {
        // The bot should never respond to itself.
        if (message.Author.Id == _dcClient.CurrentUser.Id)
            return;

        // Ignore messages sent from other channels
        if (message.Channel.Id != _dcChannelId)
            return;

        _dcChannel = message.Channel;
        HandleProxyMessage(message.Content);
    }

    #endregion

    #region Twitch

    private void OnTWLog(object sender, OnLogArgs e)
    {
        _logger.LogInformation($"{e.DateTime.ToString()}: {e.BotUsername} - {e.Data}");
    }

    private void OnTWConnected(object sender, OnConnectedArgs e)
    {
        _logger.LogInformation("Twitch connected: {time}", DateTimeOffset.Now);
    }

    private void OnTWJoinedChannel(object sender, OnJoinedChannelArgs e)
    {
        _logger.LogInformation("Twitch joined channel {channel}: {time}", e.Channel, DateTimeOffset.Now);
        _twChannel = new JoinedChannel(e.Channel);
        _twClient.SendMessage(_twChannel, "streamcmdproxy for Twitch has connected.");
    }

    private void OnTWMessageReceived(object sender, OnMessageReceivedArgs e)
    {
        _logger.LogInformation($"[TW] {DateTimeOffset.Now}: {e.ChatMessage.Username} - {e.ChatMessage.Message}");
    }

    private void StartTwitch()
    {
        var twitchUserName = _configuration.TwitchUserName;
        var twitchAccessToken = _configuration.TwitchAccessToken;
        var twitchChannel = _configuration.TwitchChannel;

        ConnectionCredentials credentials = new ConnectionCredentials(twitchUserName, twitchAccessToken);
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        WebSocketClient customClient = new WebSocketClient(clientOptions);
        _twClient = new TwitchClient(customClient);
        _twClient.Initialize(credentials, twitchChannel);

        _twClient.OnLog += OnTWLog;
        _twClient.OnJoinedChannel += OnTWJoinedChannel;
        _twClient.OnMessageReceived += OnTWMessageReceived;
        _twClient.OnConnected += OnTWConnected;

        _twClient.Connect();

        API = new TwitchAPI();

        API.Settings.ClientId = _configuration.TwitchClientId;
        API.Settings.AccessToken = twitchAccessToken;

        Monitor = new LiveStreamMonitorService(API, 60);

        List<string> lst = new List<string> { twitchChannel };
        Monitor.SetChannelsById(lst);

        Monitor.OnStreamOnline += Monitor_OnStreamOnline;
        Monitor.OnStreamOffline += Monitor_OnStreamOffline;
        Monitor.OnStreamUpdate += Monitor_OnStreamUpdate;

        Monitor.OnServiceStarted += Monitor_OnServiceStarted;
        Monitor.OnChannelsSet += Monitor_OnChannelsSet;
        
        Monitor.Start();
    }

    private void StopTwitch()
    {
        if (_twClient != null)
        {
            if (_twClient.IsConnected)
                _twClient.Disconnect();

            _twClient.OnLog -= OnTWLog;
            _twClient.OnJoinedChannel -= OnTWJoinedChannel;
            _twClient.OnMessageReceived -= OnTWMessageReceived;
            _twClient.OnConnected -= OnTWConnected;
        }

        if (Monitor != null)
        {
            Monitor.Stop();

            Monitor.OnStreamOnline -= Monitor_OnStreamOnline;
            Monitor.OnStreamOffline -= Monitor_OnStreamOffline;
            Monitor.OnStreamUpdate -= Monitor_OnStreamUpdate;

            Monitor.OnServiceStarted -= Monitor_OnServiceStarted;
            Monitor.OnChannelsSet -= Monitor_OnChannelsSet;
        }
    }

    private void OnTwitchUpdateReceived(object sender, TwitchUpdateEventArgs e)
    {
        _configuration = MongoDbContext.Instance.GetQueryableCollection<Config>(MongoDbCollections.ConfigCollection).FirstOrDefault();

        StopTwitch();

        StartTwitch();
    }

    private async void Monitor_OnStreamOnline(object sender, OnStreamOnlineArgs e)
    {
        _logger.LogInformation("Twitch stream started");
        if (_youTubeEnabled) await _ytclient.ConnectAsync();
    }

    private void Monitor_OnStreamUpdate(object sender, OnStreamUpdateArgs e)
    {
        _logger.LogInformation("Twitch stream update received");
    }

    private async void Monitor_OnStreamOffline(object sender, OnStreamOfflineArgs e)
    {
        _logger.LogInformation("Twitch stream stopped");
        if (_youTubeEnabled) _ytclient.Disconnect();
    }

    private void Monitor_OnChannelsSet(object sender, OnChannelsSetArgs e)
    {
        _logger.LogInformation("Twitch channels set");
    }

    private void Monitor_OnServiceStarted(object sender, OnServiceStartedArgs e)
    {
        _logger.LogInformation("Twitch Monitoring started");
    }

    #endregion

    private void HandleProxyMessage(string message)
    {
        var allowedCommands = MongoDbContext.Instance.GetDocuments<Command>(MongoDbCollections.CommandCollection).ToList();
        if (message.StartsWith("!proxycommands"))
        { // Display list of supported commands on Youtube
            var sb = new StringBuilder();
            sb.Append("Proxy commands: ");
            foreach (var command in allowedCommands)
            {
                if (command == allowedCommands.FirstOrDefault())
                    sb.Append(command.Name);
                else
                    sb.Append($", {command.Name}");
            }
            if (_youTubeEnabled) _ytclient.SendMessageAsync(sb.ToString());
            if (_discordEnabled && _dcChannel != null) _dcChannel.SendMessageAsync(sb.ToString());
        }
        else
        { // Proxy commands from Youtube to Twitch
            foreach (var command in allowedCommands)
            {
                if (message.StartsWith(command.Name) && command.Enabled)
                {
                    _twClient.SendMessage(_twChannel, message);
                    break;
                }
            }
        }
    }

    public static void SetupConfig(Config newConfig)
    {
        var configs = MongoDbContext.Instance.GetDocuments<Config>(MongoDbCollections.ConfigCollection);
        if (configs == null || !configs.Any())
        {
            newConfig.DocumentId = Guid.NewGuid().ToString();
            newConfig.ModifiedDate = DateTime.Now;
            MongoDbContext.Instance.CreateDocumentIfNotExists<Config>(MongoDbCollections.ConfigCollection, newConfig);
        }
        else
        {
            var config = configs.First();
            if (string.IsNullOrEmpty(config.TwitchClientId))
            {
                config.TwitchClientId = newConfig.TwitchClientId;
                config.ModifiedDate = DateTime.Now;
                MongoDbContext.Instance.ReplaceDocument(MongoDbCollections.ConfigCollection, config.DocumentId, config);
            }
        }
    }
}

