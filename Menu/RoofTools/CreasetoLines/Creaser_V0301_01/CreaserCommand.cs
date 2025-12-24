using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.AutoLiner_V04.Services;
using Revit26_Plugin.Creaser_V31.ViewModels;
using Revit26_Plugin.Creaser_V32.Helpers;
using Revit26_Plugin.Creaser_V32.Services;
using Revit26_Plugin.Creaser_V32.ViewModels;
using Revit26_Plugin.Creaser_V32.Views;
using System.Linq;

namespace Revit26_Plugin.Creaser_V32.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreaserCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
                return Result.Failed;

            Document doc = uiDoc.Document;

            // 1. Validate active view
            if (!(doc.ActiveView is ViewPlan))
            {
                TaskDialog.Show("Creaser", "This tool only works in plan views.");
                return Result.Cancelled;
            }

            // 2. Select roof
            Reference pickedRef;
            try
            {
                pickedRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select one roof");
            }
            catch
            {
                return Result.Cancelled;
            }

            RoofBase roof = doc.GetElement(pickedRef) as RoofBase;
            if (roof == null)
            {
                TaskDialog.Show("Creaser", "Selected element is not a roof.");
                return Result.Cancelled;
            }

            // Initialize UI log
            UiLogService logService = new UiLogService();

            // Launch UI
            CreaserViewModel vm = new CreaserViewModel(
                doc,
                roof,
                logService);

            CreaserWindow window = new CreaserWindow
            {
                DataContext = vm
            };

            window.ShowDialog();
            return Result.Succeeded;
        }
    }

    internal class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
