using System;
using System.Collections.Generic;

namespace streamcmdproxy
{
	public class AllowedCommands
	{
		private static object _syncRoot;
		private static AllowedCommands _instance;

		public static AllowedCommands Instance
        {
			get
            {
				lock (_syncRoot)
					if (_instance == null)
						_instance = new AllowedCommands();
				return _instance;
            }
        }

		private List<string> _commandList;
		public List<string> CommandList
        {
			get { return _commandList; }
        }

		private AllowedCommands()
		{
			_commandList = new List<string>();

			// Add commands you want to be proxyed from Youtube to Twitch below

			// BEAT SABER COMMANDS
			_commandList.Add("!bsr");
			_commandList.Add("!bshelp");
			_commandList.Add("!queue");
			_commandList.Add("!link");
			_commandList.Add("!request");
		}
	}
}

