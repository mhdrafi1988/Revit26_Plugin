using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Services
{
    public class CadImportCollectorService
    {
        private readonly Document _doc;

        public CadImportCollectorService(UIApplication uiApp)
        {
            _doc = uiApp.ActiveUIDocument.Document;
        }

        public List<CadImportItem> GetAllCadImports()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Select(i => new CadImportItem(i))
                .ToList();
        }
    }
}
