using System;
using streamcmdproxy2.Helpers.Events;

namespace streamcmdproxy2.Helpers
{
	public class EventManager
	{
		private static object _syncRoot = new object();
		private static EventManager _instance;

		public static EventManager Instance
        {
			get
            {
				lock (_syncRoot)
					if (_instance == null)
						_instance = new EventManager();
				return _instance;
			}
        }

		public EventHandler<TwitchUpdateEventArgs> TwitchUpateReceived;

		private EventManager()
		{
		}
	}
}

