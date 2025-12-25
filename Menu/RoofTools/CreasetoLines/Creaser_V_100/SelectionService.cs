using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.Creaser_V100.Services
{
    public class SelectionService
    {
        private readonly UIDocument _uiDoc;
        private readonly ILogService _log;

        public SelectionService(UIDocument uiDoc, ILogService log)
        {
            _uiDoc = uiDoc;
            _log = log;
        }

        public RoofBase PickSingleRoof()
        {
            using (_log.Scope(nameof(SelectionService), "PickSingleRoof"))
            {
                try
                {
                    Reference reference = _uiDoc.Selection.PickObject(
                        ObjectType.Element,
                        new RoofSelectionFilter(),
                        "Select a roof");

                    Element element = _uiDoc.Document.GetElement(reference);

                    if (element is RoofBase roof)
                    {
                        _log.Info(nameof(SelectionService),
                            $"Roof selected: Id={roof.Id.Value}");
                        return roof;
                    }

                    _log.Error(nameof(SelectionService),
                        "Selected element is not a RoofBase.");
                    return null;
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    _log.Warning(nameof(SelectionService),
                        "Roof selection cancelled by user.");
                    return null;
                }
            }
        }

        private class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is RoofBase;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
    }
}
