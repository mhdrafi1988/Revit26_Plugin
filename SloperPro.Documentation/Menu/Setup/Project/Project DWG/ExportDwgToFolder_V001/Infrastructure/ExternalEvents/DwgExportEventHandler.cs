using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Revit26_Plugin.ExportDwgToFolder.V001.Core.Models;
using Revit26_Plugin.ExportDwgToFolder.V001.Core.Services;

namespace Revit26_Plugin.ExportDwgToFolder.V001.Infrastructure.ExternalEvents
{
    public enum DwgExportRequest
    {
        Scan,
        Export
    }

    /// <summary>
    /// Performs all Revit-API work for the Export DWG to Folder tool.
    /// Scan reads the document; Export performs the file operations.
    /// </summary>
    public class DwgExportEventHandler : IExternalEventHandler
    {
        // ---- Input properties set by the ViewModel before Raise() ----
        public DwgExportRequest Request { get; set; }
        public ExportDwgSettings Settings { get; set; }
        public string OutputFolder { get; set; }

        // ---- Output properties read by the ViewModel after completion ----
        public List<DwgInstanceInfo> ScannedInstances { get; private set; } = new List<DwgInstanceInfo>();

        // Per-run export results (index matches ScannedInstances order)
        public List<string> ExportErrors { get; private set; } = new List<string>();
        public int ExportedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int FailedCount { get; private set; }

        public bool LastRunSucceeded { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;

        public event Action<int, int> ProgressChanged; // (current, total)
        public event Action RequestCompleted;

        public void Execute(UIApplication app)
        {
            ErrorMessage = string.Empty;
            LastRunSucceeded = false;

            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) throw new InvalidOperationException("No active document.");

                switch (Request)
                {
                    case DwgExportRequest.Scan:
                        ExecuteScan(doc);
                        break;
                    case DwgExportRequest.Export:
                        ExecuteExport(doc);
                        break;
                }

                LastRunSucceeded = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                RequestCompleted?.Invoke();
            }
        }

        public string GetName() => "Export DWG to Folder";

        // ── Scan ────────────────────────────────────────────────────────────

        private void ExecuteScan(Autodesk.Revit.DB.Document doc)
        {
            var collector = new DwgCollectorService(doc);
            ScannedInstances = collector.Collect();
        }

        // ── Export ──────────────────────────────────────────────────────────

        private void ExecuteExport(Autodesk.Revit.DB.Document doc)
        {
            ExportErrors = new List<string>();
            ExportedCount = 0;
            SkippedCount = 0;
            FailedCount = 0;

            var exporter = new DwgExportService(doc);
            var settings = Settings;
            int total = ScannedInstances.Count;

            for (int i = 0; i < total; i++)
            {
                var info = ScannedInstances[i];
                ProgressChanged?.Invoke(i + 1, total);

                // Source type filter
                if (info.SourceType == DwgSourceType.Link && !settings.ExportLinked)
                {
                    ExportErrors.Add("SKIP_FILTER");
                    SkippedCount++;
                    continue;
                }
                if (info.SourceType == DwgSourceType.Import && !settings.ExportImported)
                {
                    ExportErrors.Add("SKIP_FILTER");
                    SkippedCount++;
                    continue;
                }

                // View-type filter
                if (!IsViewCategoryEnabled(info.ViewCategory, settings))
                {
                    ExportErrors.Add("SKIP_FILTER");
                    SkippedCount++;
                    continue;
                }

                string destPath = BuildDestPath(OutputFolder, info, settings);
                string error = exporter.Export(info, destPath, settings.Overwrite);

                if (error == "SKIP")
                {
                    ExportErrors.Add("SKIP");
                    SkippedCount++;
                }
                else if (error != null)
                {
                    ExportErrors.Add(error);
                    FailedCount++;
                }
                else
                {
                    ExportErrors.Add(null); // success
                    ExportedCount++;

                    if (settings.GenerateSidecar)
                    {
                        try { exporter.WriteSidecar(info, destPath); }
                        catch { /* sidecar failure doesn't count as export failure */ }
                    }
                }
            }
        }

        private static bool IsViewCategoryEnabled(DwgViewCategory cat, ExportDwgSettings s)
        {
            return cat switch
            {
                DwgViewCategory.Plan => s.IncludePlan,
                DwgViewCategory.Section => s.IncludeSection,
                DwgViewCategory.Drafting => s.IncludeDrafting,
                DwgViewCategory.NoViews => s.IncludeNoViews,
                _ => true
            };
        }

        /// <summary>
        /// Builds the full destination path for one DWG.
        /// Structure: OutputFolder \ {source} \ [{viewtype} \] filename.dwg
        /// </summary>
        private static string BuildDestPath(string root, DwgInstanceInfo info, ExportDwgSettings settings)
        {
            string sourceFolder = info.SourceType == DwgSourceType.Link ? "link" : "import";

            if (settings.SubfoldersByViewType)
            {
                string viewFolder = info.ViewCategory switch
                {
                    DwgViewCategory.Plan => "plan",
                    DwgViewCategory.Section => "section",
                    DwgViewCategory.Drafting => "drafting",
                    _ => "no_views"
                };
                return System.IO.Path.Combine(root, sourceFolder, viewFolder, info.OutputFileName);
            }

            return System.IO.Path.Combine(root, sourceFolder, info.OutputFileName);
        }
    }
}
