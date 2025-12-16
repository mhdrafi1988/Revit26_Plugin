using Autodesk.Revit.DB;
using System;
using System.IO;

namespace Revit22_Plugin.RoofTagV4.Helpers
{
    public static class LoggerV4
    {
        private static readonly string LogPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "RoofTagV4_Log.txt");

        /// <summary>
        /// Writes a timestamped line into the log file.
        /// </summary>
        public static void Write(string message)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line);
            }
            catch
            {
                // Swallow errors — logging must not interrupt plugin
            }
        }

        /// <summary>
        /// Formats an XYZ for debugging.
        /// </summary>
        public static string P(XYZ p)
        {
            if (p == null) return "(null)";
            return $"({p.X:0.###}, {p.Y:0.###}, {p.Z:0.###})";
        }
    }
}
