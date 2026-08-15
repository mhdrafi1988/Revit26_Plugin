using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.DwgToDetailLines.V010.Models;

namespace Revit26_Plugin.DwgToDetailLines.V010.Services
{
    public class CadImportCollectorService
    {
        private readonly Document _doc;
        private readonly View _activeView;

        public CadImportCollectorService(UIApplication uiApp)
        {
            _doc = uiApp.ActiveUIDocument.Document;
            _activeView = uiApp.ActiveUIDocument.ActiveView;
        }

        /// <summary>
        /// Returns CAD imports scoped to the active Drafting View only:
        /// view-specific imports owned by this view, plus model-wide imports
        /// (OwnerViewId invalid — visible in every view) that this view can
        /// still see. Imports that are view-specific to a DIFFERENT view are
        /// excluded — their geometry/placement is only meaningful in that
        /// other view's own 2D space, and converting them into the active
        /// view produced mismatched detail-line locations.
        /// </summary>
        public List<CadImportItem> GetAllCadImports()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Where(i => i.OwnerViewId == ElementId.InvalidElementId
                         || i.OwnerViewId == _activeView.Id)
                .Select(i => new CadImportItem(i))
                .ToList();
        }
    }
}
