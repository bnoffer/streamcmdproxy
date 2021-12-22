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

namespace streamcmdproxy;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private YouTubeChatClient _ytclient;
    private TwitchClient _twClient;
    private JoinedChannel _twChannel;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // -- SETUP YOUTUBE --
        var googleCredentialsFile = _configuration["GoogleCredentialsFile"];
        var youtubeUserEmail = _configuration["YoutubeUserEmail"];

        _ytclient = new YouTubeChatClient(new YoutubeAuthService(googleCredentialsFile, youtubeUserEmail));
        _ytclient.OnConnected += OnYTConnected;
        _ytclient.OnDisconnected += OnYTDisconnected;
        _ytclient.OnMessageReceived += OnYTMessageReceivedAsync;

        var twitchUserName = _configuration["TwitchUserName"];
        var twitchAccessToken = _configuration["TwitchAccessToken"];
        var twitchChannel = _configuration["TwitchChannel"];

        // -- SETUP TWITCH --
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
        await _ytclient.ConnectAsync();
        _twClient.Connect();

        while (!stoppingToken.IsCancellationRequested)
        {
            //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
        
        _ytclient.Disconnect();
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
                _twClient.SendMessage(_twChannel, liveChatMessage.Snippet.DisplayMessage);
        }
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
}

