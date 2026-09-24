using System;
using System.IO;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB002.Infrastructure.ExternalEvents
{
    /// <summary>
    /// TEMPORARY diagnostic logger for tracking down why the isolate/select
    /// action chain wasn't producing a visible result. Remove once the root
    /// cause is found and fixed.
    /// </summary>
    internal static class WsebDebugLog
    {
        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "WSEB_debug.log");
        private static readonly object Lock = new();

        public static void Write(string message)
        {
            try
            {
                lock (Lock)
                {
                    File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // best-effort diagnostic only
            }
        }
    }
}
