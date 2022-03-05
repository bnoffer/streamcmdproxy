using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;

namespace streamcmdproxy2.Pages
{
	public class TwitchAuthModel : PageModel
    {
        public async Task OnGet()
        {
            await HttpContext.ChallengeAsync("Twitch", new AuthenticationProperties
            {
                RedirectUri = "/TwitchSignIn"
            });
        }
    }
}
