using System.Collections.Generic;
using System.Text;
using streamcmdproxy.Youtube;
using Google.Apis.YouTube.v3.Data;
using ChatWell.YouTube;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using Discord;
using Discord.WebSocket;

namespace streamcmdproxy;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;

    private YouTubeChatClient _ytclient;

    private DiscordSocketClient _dcClient;
    private string _dcToken;

    private TwitchClient _twClient;
    private JoinedChannel _twChannel;

    private bool _youTubeEnabled;
    private bool _discordEnabled;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // -- CONFIG --
        _youTubeEnabled = _configuration["EnableYoutube"].Equals("True");
        _discordEnabled = _configuration["EnableDiscord"].Equals("True");

        // -- SETUP YOUTUBE --
        if (_youTubeEnabled)
        {
            var googleCredentialsFile = _configuration["GoogleCredentialsFile"];
            var youtubeUserEmail = _configuration["YoutubeUserEmail"];

            _ytclient = new YouTubeChatClient(new YoutubeAuthService(googleCredentialsFile, youtubeUserEmail));
            _ytclient.OnConnected += OnYTConnected;
            _ytclient.OnDisconnected += OnYTDisconnected;
            _ytclient.OnMessageReceived += OnYTMessageReceivedAsync;
        }

        // -- SETUP DISCORD --
        if (_discordEnabled)
        {
            _dcToken = _configuration["DiscordToken"];
            _dcClient = new DiscordSocketClient();

            _dcClient.Log += DcLogAsync;
            _dcClient.Ready += DcReadyAsync;
            _dcClient.MessageReceived += DcMessageReceivedAsync;
        }

        // -- SETUP TWITCH --
        var twitchUserName = _configuration["TwitchUserName"];
        var twitchAccessToken = _configuration["TwitchAccessToken"];
        var twitchChannel = _configuration["TwitchChannel"];

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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_youTubeEnabled) await _ytclient.ConnectAsync();
        if (_discordEnabled)
        {
            await _dcClient.LoginAsync(TokenType.Bot, _dcToken);
            await _dcClient.StartAsync();
        }
        _twClient.Connect();

        while (!stoppingToken.IsCancellationRequested)
        {
            //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }

        if (_youTubeEnabled) _ytclient.Disconnect();
        _twClient.Disconnect();
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

    #endregion

    private void HandleProxyMessage(string message)
    {
        if (message.StartsWith("!proxycommands"))
        { // Display list of supported commands on Youtube
            var sb = new StringBuilder();
            sb.Append("Proxy commands: ");
            foreach (var command in AllowedCommands.Instance.CommandList)
            {
                if (command == AllowedCommands.Instance.CommandList.FirstOrDefault())
                    sb.Append(command);
                else
                    sb.Append($", {command}");
            }
            _ytclient.SendMessageAsync(sb.ToString());
        }
        else
        { // Proxy commands from Youtube to Twitch
            foreach (var command in AllowedCommands.Instance.CommandList)
            {
                if (message.StartsWith(command))
                {
                    _twClient.SendMessage(_twChannel, message);
                    break;
                }
            }
        }
    }
}

