using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.DwgSymbolicConverter_V02.Models;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Services
{
    /// <summary>
    /// Collects all CAD ImportInstances (DWG/DXF/etc.) in the current family document.
    /// </summary>
    public class CadImportCollectorService
    {
        private readonly Document _doc;

        public CadImportCollectorService(UIApplication uiApp)
        {
            _doc = uiApp.ActiveUIDocument.Document;
        }

        public List<CadImportItem> GetAllCadImports()
        {
            var imports = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .ToList();

            var result = new List<CadImportItem>();

            foreach (ImportInstance imp in imports)
            {
                // Revit 2026 official API: ImportInstance.IsLinked
                string importType = imp.IsLinked ? "Link" : "Import";

                result.Add(new CadImportItem(imp, importType));
            }

            return result;
        }
    }
}
