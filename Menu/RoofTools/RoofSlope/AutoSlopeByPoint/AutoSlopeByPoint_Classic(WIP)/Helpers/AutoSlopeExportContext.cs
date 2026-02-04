using Revit26_Plugin.AutoSlopeByPoint_WIP2.Export;
using System;
using System.Collections.Generic;
using System.IO;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Services
{
    public class AutoSlopeExportContext
    {
        public List<AutoSlopeVertexExportDto> Rows { get; } = new();

        public string ExportFolder { get; }

        public AutoSlopeExportContext(string exportFolder)
        {
            ExportFolder = string.IsNullOrWhiteSpace(exportFolder)
                ? GetDefaultFolder()
                : exportFolder;
        }

        public void Commit()
        {
            AutoSlopeCsvExporter.Export(Rows, ExportFolder);
        }

        public static string GetDefaultFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AutoSlope_Reports");
        }
    }
}