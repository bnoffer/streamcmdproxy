using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using AspNet.Security.OAuth.Twitch;
using streamcmdproxy2;
using streamcmdproxy2.Data;
using streamcmdproxy2.Data.Models;

var builder = WebApplication.CreateBuilder(args);

// Setup the MongoDB Database context
MongoDbContext.Init(builder.Configuration["MongoDB:ConnectionString"], builder.Configuration["MongoDB:DatabaseName"]);

// Load configuration
Worker.SetupConfig(new Config
{
    EnableYoutube = builder.Configuration["EnableYoutube"].Equals("True"),
    EnableDiscord = builder.Configuration["EnableDiscord"].Equals("True"),
    GoogleCredentialsFile = builder.Configuration["GoogleCredentialsFile"],
    YoutubeUserEmail = builder.Configuration["YoutubeUserEmail"],
    DiscordToken = builder.Configuration["DiscordToken"],
    DiscordChannelID = builder.Configuration["DiscordChannelID"],
    TwitchUserName = builder.Configuration["TwitchUserName"],
    TwitchAccessToken = builder.Configuration["TwitchAccessToken"],
    TwitchChannel = builder.Configuration["TwitchChannel"],
    TwitchClientId = builder.Configuration["TwitchClientId"]
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/signin";
    options.LogoutPath = "/signout";
})
.AddTwitch(options =>
{
    options.ClientId = builder.Configuration["TwitchClientId"];
    options.ClientSecret = builder.Configuration["TwitchClientSecret"];
    options.SaveTokens = true;
    options.Scope.Add("channel:moderate");
    options.Scope.Add("chat:edit");
    options.Scope.Add("chat:read");
    options.Scope.Add("whispers:read");
    options.Scope.Add("whispers:edit");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();