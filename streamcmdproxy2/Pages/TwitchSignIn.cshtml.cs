using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using streamcmdproxy2.Data;
using streamcmdproxy2.Data.Models;
using streamcmdproxy2.Helpers;
using streamcmdproxy2.Helpers.Events;

namespace streamcmdproxy2.Pages
{
	public class TwitchSignInModel : PageModel
    {
        private NavigationManager _navManager;

        public TwitchSignInModel(NavigationManager navManager)
        {
            _navManager = navManager;
        }

        public async void OnGet()
        {
            var tag = $"{this}.OnGet";
            try
            {
                // Get Access Token
                var code = await HttpContext.GetTokenAsync("access_token");

                // Save configuration to DB
                var config = MongoDbContext.Instance.GetDocuments<Config>(MongoDbCollections.ConfigCollection).First();
                config.TwitchAccessToken = code;
                config.ModifiedDate = DateTime.Now;
                MongoDbContext.Instance.ReplaceDocument(MongoDbCollections.ConfigCollection, config.DocumentId, config);

                EventManager.Instance.TwitchUpateReceived?.Invoke(this, new TwitchUpdateEventArgs());

                _navManager.NavigateTo("/");
            }
            catch (Exception ex)
            {
                Track.Exception(tag, ex);
            }
        }
    }
}
