using System;
namespace streamcmdproxy2.Helpers.Events
{
    public class TrackingEventArgs : EventArgs
    {
        public string Source { get; set; }

        public TrackingType EventType { get; set; }

        public string Message { get; set; }

        public TrackingEventArgs()
        {
            Source = "";
            EventType = TrackingType.Info;
            Message = "";
        }

        public TrackingEventArgs(string source, TrackingType type, string message)
        {
            Source = source;
            EventType = type;
            Message = message;
        }

        public enum TrackingType
        {
            Info,
            Warning,
            Error
        }
    }
}

