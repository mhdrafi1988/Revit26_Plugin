using System;
using System.Collections.ObjectModel;
using System.IO;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V008.Services
{
    /// <summary>
    /// Writes the live log to a .txt file on completion.
    ///
    /// V008 changes from V07:
    /// - Uses shared LogEntry (LogEntry.ToString() already formats as
    ///   "HH:mm:ss  Level  Message").
    /// - Filename now follows the suite-standard convention:
    ///   {ToolName}_Logs_{yyyy-MM-dd}_{HH-mm}.txt
    /// - SaveFolder is asked once per session by the caller (ViewModel/
    ///   orchestrator) and passed in, rather than this service hardcoding
    ///   "My Documents" — this service just accepts the folder now.
    /// - V07 never actually called Write(); V008 wires this into
    ///   the orchestrator's completion step.
    /// </summary>
    public class LogFileService
    {
        public string LastWrittenPath { get; private set; }

        public string Write(ObservableCollection<LogEntry> entries, string saveFolder)
        {
            Directory.CreateDirectory(saveFolder);

            string fileName =
                $"CreateSectionsFromDetailLines_Logs_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HH-mm}.txt";

            string path = Path.Combine(saveFolder, fileName);

            using (StreamWriter sw = new(path))
            {
                foreach (var e in entries)
                    sw.WriteLine(e.ToString());
            }

            LastWrittenPath = path;
            return path;
        }
    }
}
