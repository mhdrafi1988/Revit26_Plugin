using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Models;

namespace Revit26_Plugin.ScheduleExportImport.V001.Core.Services
{
    /// <summary>Writes a full, tab-separated record of an import next to the Excel file,
    /// so every applied, failed and skipped value can be audited after the fact.</summary>
    public static class ImportReportService
    {
        public static string Write(string excelPath, string scheduleName, ImportAnalysis analysis, IEnumerable<ImportChange> items)
        {
            var folder = Path.GetDirectoryName(excelPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var name = Path.GetFileNameWithoutExtension(excelPath);
            var path = Path.Combine(folder, $"{name}_ImportReport_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

            var list = items.ToList();
            var sb = new StringBuilder();
            sb.AppendLine("Schedule Export / Import V001 — Import Report");
            sb.AppendLine($"Date:      {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Schedule:  {scheduleName}");
            sb.AppendLine($"File:      {excelPath}");
            sb.AppendLine();
            sb.AppendLine($"Rows in file:            {analysis.RowsInFile}");
            sb.AppendLine($"Elements matched:        {analysis.ElementsMatched}");
            sb.AppendLine($"Unchanged values:        {analysis.UnchangedValues}");
            sb.AppendLine($"Duplicate rows merged:   {analysis.DuplicateRowsMerged}");
            foreach (var g in list.GroupBy(i => i.Status).OrderBy(g => g.Key))
                sb.AppendLine($"{g.Key + ":",-25}{g.Count()}");
            sb.AppendLine();
            sb.AppendLine("Status\tElement ID\tParameter\tOld value\tNew value\tNote");
            foreach (var i in list.OrderBy(i => i.Status).ThenBy(i => i.ElementId))
                sb.AppendLine($"{i.Status}\t{i.ElementId}\t{i.ParameterName}\t{i.OldValue}\t{i.NewValue}\t{i.Message}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }
    }
}
