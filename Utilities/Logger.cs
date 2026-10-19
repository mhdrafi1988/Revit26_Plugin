using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Revit26_Plugin.Utilities
{
    /// <summary>
    /// Appends structured log lines to %APPDATA%\SloperPro\sloper_pro.log.
    /// Thread-safe via lock. Rolls the file over 5 MB so it never fills disk.
    /// Usage:
    ///   Logger.Info("PlaceSections", "placed 3 sections");
    ///   Logger.Error("PlaceSections", ex);
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath;
        private static readonly object _lock = new object();
        private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

        static Logger()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SloperPro");
            Directory.CreateDirectory(dir);
            LogPath = Path.Combine(dir, "sloper_pro.log");
        }

        public static void Info(string tool, string message)
            => Write("INFO ", tool, message, null);

        public static void Warning(string tool, string message)
            => Write("WARN ", tool, message, null);

        public static void Error(string tool, Exception ex)
            => Write("ERROR", tool, ex.Message, ex.ToString());

        public static void Error(string tool, string message, Exception ex = null)
            => Write("ERROR", tool, message, ex?.ToString());

        private static void Write(string level, string tool, string message, string detail)
        {
            try
            {
                lock (_lock)
                {
                    RollIfNeeded();

                    string version = Assembly
                        .GetExecutingAssembly()
                        .GetName()
                        .Version?.ToString() ?? "?";

                    var sb = new StringBuilder();
                    sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [v{version}] [{tool}] {message}");
                    if (detail != null)
                    {
                        sb.AppendLine();
                        sb.Append("  ").Append(detail.Replace(Environment.NewLine, Environment.NewLine + "  "));
                    }

                    File.AppendAllText(LogPath, sb.ToString() + Environment.NewLine);
                }
            }
            catch
            {
                // Never let logging crash the host command.
            }
        }

        private static void RollIfNeeded()
        {
            if (!File.Exists(LogPath)) return;
            if (new FileInfo(LogPath).Length < MaxBytes) return;

            string archive = LogPath.Replace(".log", $"_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.Move(LogPath, archive);

            // Keep only the two most recent archives.
            string dir = Path.GetDirectoryName(LogPath)!;
            var archives = Directory.GetFiles(dir, "sloper_pro_*.log");
            Array.Sort(archives);
            for (int i = 0; i < archives.Length - 2; i++)
                File.Delete(archives[i]);
        }
    }
}
