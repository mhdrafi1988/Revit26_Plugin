// File: Commands/ShowAutoSlopeWindowCommand.cs
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByDrain_21.Views;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowAutoSlopeWindowCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // Get Revit context
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Get selected roof or prompt user to select one
            ElementId roofId = null;
            List<XYZ> drains = new List<XYZ>();

            // Option A: Use currently selected roof
            var selectedElements = uidoc.Selection.GetElementIds();
            var roof = selectedElements
                .Select(id => doc.GetElement(id))
                .FirstOrDefault(e => e is RoofBase);

            if (roof == null)
            {
                // Option B: Let user pick a roof
                try
                {
                    var reference = uidoc.Selection.PickObject(
                        Autodesk.Revit.UI.Selection.ObjectType.Element,
                        new RoofSelectionFilter(),
                        "Select a roof to apply auto slope"
                    );
                    roof = doc.GetElement(reference.ElementId);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
            }

            if (roof == null)
            {
                TaskDialog.Show("Error", "No roof selected. Please select a roof and try again.");
                return Result.Failed;
            }

            roofId = roof.Id;

            // Get drains if needed (or pass empty list)
            drains = GetDrainsFromSelection(uidoc);

            // Show the AutoSlopeWindow
            var window = new AutoSlopeWindow(uidoc, uiapp, roofId, drains);
            window.ShowDialog();

            return Result.Succeeded;
        }

        private List<XYZ> GetDrainsFromSelection(UIDocument uidoc)
        {
            var drains = new List<XYZ>();

            // Option: Get selected plumbing fixtures
            var selectedIds = uidoc.Selection.GetElementIds();
            foreach (var id in selectedIds)
            {
                var element = uidoc.Document.GetElement(id);
                if (element is FamilyInstance instance)
                {
                    // Check if it's a drain or plumbing fixture
                    var category = instance.Category;
                    if (category != null &&
                        (category.Name.Contains("Plumbing") ||
                         category.Name.Contains("Drain") ||
                         category.Id.Value == (int)BuiltInCategory.OST_PlumbingFixtures))
                    {
                        var location = instance.Location as LocationPoint;
                        if (location != null)
                        {
                            drains.Add(location.Point);
                        }
                    }
                }
            }

            return drains;
        }
    }

    // Selection filter for roofs only
    public class RoofSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
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