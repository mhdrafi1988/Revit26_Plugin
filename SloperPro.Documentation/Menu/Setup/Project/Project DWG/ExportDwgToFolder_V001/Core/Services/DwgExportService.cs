using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using Revit26_Plugin.ExportDwgToFolder.V001.Core.Models;

namespace Revit26_Plugin.ExportDwgToFolder.V001.Core.Services
{
    /// <summary>
    /// Writes one DWG file (and optionally a sidecar .txt) for each <see cref="DwgInstanceInfo"/>.
    /// Linked DWGs are copied from their source path; imported DWGs are exported from
    /// the owner view using <see cref="Document.Export"/>.
    /// </summary>
    public class DwgExportService
    {
        private readonly Document _doc;

        public DwgExportService(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Exports one DWG to <paramref name="destPath"/>.
        /// Returns null on success, or an error message on failure.
        /// </summary>
        public string Export(DwgInstanceInfo info, string destPath, bool overwrite)
        {
            if (!overwrite && File.Exists(destPath))
                return "SKIP";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                if (info.SourceType == DwgSourceType.Link)
                    return ExportLinked(info, destPath, overwrite);
                else
                    return ExportImported(info, destPath, overwrite);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>Writes the sidecar .txt file alongside the exported DWG.</summary>
        public void WriteSidecar(DwgInstanceInfo info, string dwgDestPath)
        {
            string sidecarPath = Path.ChangeExtension(dwgDestPath, ".txt");
            string content =
                $"View Name: {info.ViewName ?? "(none)"}\r\n" +
                $"Associated Sheet: {info.SheetInfo}\r\n" +
                $"Element ID: {info.ElementId.Value}\r\n" +
                $"Symbol Name: {info.SymbolName}\r\n";
            File.WriteAllText(sidecarPath, content, System.Text.Encoding.UTF8);
        }

        // ── Linked ──────────────────────────────────────────────────────────

        private string ExportLinked(DwgInstanceInfo info, string destPath, bool overwrite)
        {
            if (string.IsNullOrEmpty(info.LinkedSourcePath))
                return "Source file path could not be resolved — the link may be unloaded or use a relative path that is not accessible from this machine.";

            if (!File.Exists(info.LinkedSourcePath))
                return $"Source file not found: {info.LinkedSourcePath}";

            File.Copy(info.LinkedSourcePath, destPath, overwrite);
            return null; // success
        }

        // ── Imported ─────────────────────────────────────────────────────────

        private string ExportImported(DwgInstanceInfo info, string destPath, bool overwrite)
        {
            if (info.OwnerViewId == null || info.OwnerViewId == ElementId.InvalidElementId)
                return "No host view found for this imported DWG — it may be a model-space import not visible in any scanned view.";

            string outputFolder = Path.GetDirectoryName(destPath);
            string fileNameNoExt = Path.GetFileNameWithoutExtension(destPath);

            // Revit appends the view name to the exported file, so we export via a temp folder
            // then rename the result to the desired output name.
            string tempFolder = Path.Combine(Path.GetTempPath(), $"ExportDwg_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempFolder);

            try
            {
                var opts = new DWGExportOptions
                {
                    FileVersion = ACADVersion.R2010,
                    HideScopeBox = true,
                    HideUnreferenceViewTags = true,
                    HideReferencePlane = true,
                    ExportingAreas = false
                };

                // Use the view's own name as the temp file stem; Revit adds .dwg automatically.
                var viewIds = new List<ElementId> { info.OwnerViewId };
                _doc.Export(tempFolder, fileNameNoExt, viewIds, opts);

                // Revit may produce "stem.dwg" or "stem - View Name.dwg"; find whatever it wrote.
                var produced = Directory.GetFiles(tempFolder, "*.dwg");
                if (produced.Length == 0)
                    return "Revit Export produced no DWG file.";

                // If more than one file, take the one whose name starts with our stem.
                string chosen = produced[0];
                foreach (var f in produced)
                {
                    if (Path.GetFileName(f).StartsWith(fileNameNoExt, StringComparison.OrdinalIgnoreCase))
                    {
                        chosen = f;
                        break;
                    }
                }

                if (overwrite && File.Exists(destPath))
                    File.Delete(destPath);

                File.Move(chosen, destPath);
                return null; // success
            }
            finally
            {
                try { Directory.Delete(tempFolder, recursive: true); } catch { }
            }
        }
    }
}
