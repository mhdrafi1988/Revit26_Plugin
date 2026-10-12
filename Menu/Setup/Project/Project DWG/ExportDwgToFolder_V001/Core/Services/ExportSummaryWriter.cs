using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.ExportDwgToFolder.V001.Core.Services
{
    /// <summary>Outcome of one DWG in an export run, as listed in the summary file.</summary>
    public class ExportSummaryItem
    {
        public string FileName { get; set; }
        public string Source { get; set; }
        public string ViewType { get; set; }

        /// <summary>One of "Exported", "Skipped" or "Failed".</summary>
        public string Result { get; set; }

        /// <summary>Reason for a skip or failure; empty for a success.</summary>
        public string Detail { get; set; }
    }

    /// <summary>Writes the plain-text summary of an export run into the output root folder.</summary>
    public static class ExportSummaryWriter
    {
        /// <summary>
        /// Writes <c>Export_Summary_yyyyMMdd_HHmmss.txt</c> into <paramref name="rootFolder"/>
        /// and returns its full path. Throws on I/O failure; callers must handle it.
        /// </summary>
        public static string Write(string rootFolder, string documentTitle, string toolVersion,
                                   string optionsSummary, IReadOnlyList<ExportSummaryItem> items)
        {
            Directory.CreateDirectory(rootFolder);
            var now = DateTime.Now;
            string path = Path.Combine(rootFolder, $"Export_Summary_{now:yyyyMMdd_HHmmss}.txt");

            var exported = items.Where(i => i.Result == "Exported").ToList();
            var skipped = items.Where(i => i.Result == "Skipped").ToList();
            var failed = items.Where(i => i.Result == "Failed").ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Export DWG to Folder {toolVersion} - Summary");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine($"Date     : {now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Model    : {documentTitle}");
            sb.AppendLine($"Folder   : {rootFolder}");
            sb.AppendLine($"Options  : {optionsSummary}");
            sb.AppendLine();
            sb.AppendLine($"Total {items.Count} | Exported {exported.Count} | Warnings (skipped) {skipped.Count} | Failed {failed.Count}");
            sb.AppendLine();

            AppendSection(sb, "SUCCESSFUL EXPORTS", exported, false);
            AppendSection(sb, "WARNINGS (SKIPPED)", skipped, true);
            AppendSection(sb, "FAILURES", failed, true);

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return path;
        }

        private static void AppendSection(StringBuilder sb, string title, List<ExportSummaryItem> items, bool withDetail)
        {
            sb.AppendLine($"{title} ({items.Count})");
            sb.AppendLine(new string('-', 60));
            if (items.Count == 0) sb.AppendLine("  none");
            foreach (var i in items)
            {
                sb.AppendLine($"  {i.FileName}   [{i.Source} / {i.ViewType}]");
                if (withDetail && !string.IsNullOrEmpty(i.Detail))
                    sb.AppendLine($"      Reason: {i.Detail}");
            }
            sb.AppendLine();
        }
    }
}
