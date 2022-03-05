using System;
using ChatWell.YouTube;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace streamcmdproxy2.Youtube
{
	public class YoutubeAuthService : IYouTubeAuthService
	{
        private string _googleCredentialsFile;
        private string _youtubeUserEmail;

		public YoutubeAuthService(string GoogleCredentialsFile, string YoutubeUserEmail)
		{
            _googleCredentialsFile = GoogleCredentialsFile;
            _youtubeUserEmail = YoutubeUserEmail;
		}

        public async Task<UserCredential> GetUserCredentialAsync()
        {
            using (var stream = new FileStream(_googleCredentialsFile, FileMode.Open))
            {
                var loadedSecrets = GoogleClientSecrets.Load(stream);
                var clientSecrets = loadedSecrets.Secrets;
                var scopes = new[] { YouTubeService.Scope.YoutubeForceSsl };
                var dataStore = new FileDataStore(this.GetType().ToString());
                var user = await GoogleWebAuthorizationBroker.AuthorizeAsync(clientSecrets, scopes, _youtubeUserEmail, CancellationToken.None, dataStore).ConfigureAwait(false);
                return user;
            }
        }
    }
}

