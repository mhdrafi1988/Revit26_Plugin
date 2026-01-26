using Autodesk.Revit.DB;
using BatchDwgFamilyLinker.Models;
using System;
using System.IO;
using System.Linq;

namespace BatchDwgFamilyLinker.Services
{
    public static class DwgLinkService
    {
        public static void LoadDwg(
            Document doc,
            View view,
            string dwgPath,
            DwgPlacementMode placementMode,
            DwgLoadMode loadMode)
        {
            if (!File.Exists(dwgPath))
                throw new FileNotFoundException(dwgPath);

            string baseName = Path.GetFileNameWithoutExtension(dwgPath);

            // LINK MODE = DELETE + REIMPORT
            if (loadMode == DwgLoadMode.Link)
            {
                var existing = new FilteredElementCollector(doc)
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .Where(i =>
                    {
                        var t = doc.GetElement(i.GetTypeId()) as ElementType;
                        return t != null &&
                               t.Name.IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0;
                    });

                foreach (var inst in existing)
                    doc.Delete(inst.Id);
            }

            var options = new DWGImportOptions
            {
                Unit = ImportUnit.Millimeter,
                Placement = placementMode == DwgPlacementMode.OriginToOrigin
                    ? ImportPlacement.Origin
                    : ImportPlacement.Centered,
                ColorMode = ImportColorMode.Preserved,
                VisibleLayersOnly = false
            };

            if (!doc.Import(dwgPath, options, view, out _))
                throw new InvalidOperationException("DWG load failed.");
        }
    }
}
