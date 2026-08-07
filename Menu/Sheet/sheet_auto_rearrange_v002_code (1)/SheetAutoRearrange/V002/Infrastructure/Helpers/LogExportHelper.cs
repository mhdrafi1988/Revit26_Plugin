using Microsoft.Win32;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.SheetAutoRearrange.V002.Infrastructure.Helpers
{
    /// <summary>
    /// Writes the log panel to a .txt file. Filename pattern:
    /// SheetAutoRearrange_Logs_{yyyy-MM-dd}_{HH-mm}.txt. Asks for a save
    /// folder once per session (via the ViewModel caching the chosen path)
    /// and reuses it for subsequent auto-saves and exports.
    /// </summary>
    public static class LogExportHelper
    {
        public static string BuildFileName()
        {
            var now = DateTime.Now;
            return $"SheetAutoRearrange_Logs_{now:yyyy-MM-dd}_{now:HH-mm}.txt";
        }

        /// <summary>Writes the log directly to <paramref name="folderPath"/> using the standard filename. Used for auto-save on completion.</summary>
        public static void SaveToFolder(ObservableCollection<LogEntry> log, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return;

            string fullPath = Path.Combine(folderPath, BuildFileName());
            File.WriteAllText(fullPath, BuildLogText(log), Encoding.UTF8);
        }

        /// <summary>
        /// Prompts the user for a save folder via a folder picker (SaveFileDialog
        /// used as a folder-select workaround is avoided — uses OpenFolderDialog
        /// where available). Returns the chosen folder, or null if cancelled.
        /// </summary>
        public static string? PromptForFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select folder to save Sheet Auto Rearrange logs"
            };

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }

        private static string BuildLogText(ObservableCollection<LogEntry> log)
            => string.Join(Environment.NewLine, log.Select(l => l.ToString()));
    }
}
