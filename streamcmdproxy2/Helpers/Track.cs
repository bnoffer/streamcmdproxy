using System;
using streamcmdproxy2.Helpers.Events;

namespace streamcmdproxy2
{
    public static class Track
    {
        public static EventHandler<TrackingEventArgs> TrackEvent;

        public static void Info(string source, string message)
        {
            TrackEvent?.Invoke(new object(), new TrackingEventArgs(source, TrackingEventArgs.TrackingType.Info, message));
        }

        public static void Warning(string source, string message)
        {
            TrackEvent?.Invoke(new object(), new TrackingEventArgs(source, TrackingEventArgs.TrackingType.Warning, message));
        }

        public static void Error(string source, string message)
        {
            TrackEvent?.Invoke(new object(), new TrackingEventArgs(source, TrackingEventArgs.TrackingType.Error, message));
        }

        public static void Exception(string source, Exception ex)
        {
            try
            {
                if (ex.InnerException == null)
                    Error(source, $"{ex.Message}:\r\n{ex.StackTrace}");
                else
                    Error(source, $"{ex.Message}:\r\n{ex.StackTrace}\r\nInnerEx: {ex.InnerException.Message}:\r\n{ex.InnerException.StackTrace}");
            }
            catch (Exception tex)
            {
                Track.Error("Backend.Track.Exception", tex.Message);
            }
        }
    }
}

