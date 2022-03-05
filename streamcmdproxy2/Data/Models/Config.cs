using System;
using Newtonsoft.Json;

namespace streamcmdproxy2.Data.Models
{
	public class Config : BaseModel
	{
		[JsonProperty(PropertyName = "enableYoutube")]
		public bool EnableYoutube { get; set; }

		[JsonProperty(PropertyName = "enableDiscord")]
		public bool EnableDiscord { get; set; }

		[JsonProperty(PropertyName = "googleCredentialsFile")]
		public string GoogleCredentialsFile { get; set; }

		[JsonProperty(PropertyName = "youtubeUserEmail")]
		public string YoutubeUserEmail { get; set; }

		[JsonProperty(PropertyName = "discordToken")]
		public string DiscordToken { get; set; }

		[JsonProperty(PropertyName = "discordChannelId")]
		public string DiscordChannelID { get; set; }

		[JsonProperty(PropertyName = "twitchUserName")]
		public string TwitchUserName { get; set; }

		[JsonProperty(PropertyName = "twitchAccessToken")]
		public string TwitchAccessToken { get; set; }

		[JsonProperty(PropertyName = "twitchChannel")]
		public string TwitchChannel { get; set; }

		[JsonProperty(PropertyName = "twitchClientId")]
		public string TwitchClientId { get; set; }

		[JsonProperty(PropertyName = "modifiedDate")]
		public DateTime ModifiedDate { get; set; }
	}
}

