using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using Revit26_Plugin.DwgSymbolicConverter_V01.Models;

namespace Revit26_Plugin.DwgSymbolicConverter_V01.Services
{
    public class CadSelectionService
    {
        private readonly UIApplication _uiApp;

        public CadSelectionService(UIApplication uiApp)
        {
            _uiApp = uiApp;
        }

        public CadFileInfo GetSelectedCad()
        {
            UIDocument uidoc = _uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            var selectedIds = uidoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
                throw new InvalidOperationException(
                    "No CAD file selected. Please select a DWG Import or Link.");

            ElementId id = selectedIds.First();
            Element element = doc.GetElement(id);

            if (element is not ImportInstance importInstance)
                throw new InvalidOperationException(
                    "Selected element is not a CAD Import or Link.");

            bool isLinked = importInstance.IsLinked;

            return new CadFileInfo
            {
                FileName = element.Name,
                ElementId = element.Id.ToString(),
                ImportType = isLinked ? "Link" : "Import"
            };
        }
    }
}
