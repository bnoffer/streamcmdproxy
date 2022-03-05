using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using streamcmdproxy2.Data;

namespace streamcmdproxy2.Data.Models
{
	public class Command : BaseModel
	{
		[JsonProperty(PropertyName = "name")]
		public string Name { get; set; }

		[JsonProperty(PropertyName = "enabled")]
		public bool Enabled { get; set; }

		public static void InitialSetup()
        {
			// default command list
			var commands = new string[] { "!bsr", "!bshelp", "!queue", "!link", "!request", "!bomb" };

			var query = MongoDbContext.Instance.GetQueryableCollection<Command>(MongoDbCollections.CommandCollection);
			if (query != null)
            {
				foreach (var cmd in commands)
                {
					var dbcmd = query.Where(c => c.Name.Equals(cmd));
					if (dbcmd == null || !dbcmd.Any())
						MongoDbContext.Instance.CreateDocumentIfNotExists<Command>(MongoDbCollections.CommandCollection, new Command { Name = cmd, Enabled = true, DocumentId = Guid.NewGuid().ToString() });
                }
            }
		}
	}
}

