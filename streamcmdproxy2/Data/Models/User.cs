using System;
using Newtonsoft.Json;

namespace streamcmdproxy2.Data.Models
{
	public class User : BaseModel
	{
		[JsonProperty(PropertyName = "username")]
		public string Username { get; set; }

		[JsonProperty(PropertyName = "password")]
		public string PasswordHash { get; set; }
	}
}

