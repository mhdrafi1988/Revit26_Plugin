// =======================================================
// File: RoofTagCommand.cs
// Project: Revit26_Plugin.RoofTag_V73
// Layer: Commands
// Purpose: Entry point for Roof Tagging tool
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;
using Revit26_Plugin.RoofTag_V73.Models;
using Revit26_Plugin.RoofTag_V73.Services;
using System;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V73.Commands
{
    /// <summary>
    /// External command entry point for roof tagging.
    /// Responsible ONLY for:
    /// - Context validation
    /// - Element selection
    /// - Calling TaggingService
    /// </summary>
    public class RoofTagCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View view = doc.ActiveView;

            try
            {
                // --------------------------------------------------
                // 1. Validate active view
                // --------------------------------------------------
                if (view == null || view.IsTemplate)
                {
                    TaskDialog.Show(
                        "Roof Tag",
                        "Please run the command in a valid plan, section, or elevation view.");
                    return Result.Cancelled;
                }

                // --------------------------------------------------
                // 2. Ask user to select a roof
                // --------------------------------------------------
                Reference pickedRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof to tag");

                if (pickedRef == null)
                    return Result.Cancelled;

                RoofBase roof =
                    doc.GetElement(pickedRef) as RoofBase;

                if (roof == null)
                {
                    TaskDialog.Show(
                        "Roof Tag",
                        "Selected element is not a roof.");
                    return Result.Failed;
                }

                // --------------------------------------------------
                // 3. Resolve tag type (first available roof tag)
                // --------------------------------------------------
                ElementId roofTagTypeId =
                    new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .OfCategory(BuiltInCategory.OST_RoofTags)
                        .Cast<FamilySymbol>()
                        .FirstOrDefault()
                        ?.Id;

                if (roofTagTypeId == null)
                {
                    TaskDialog.Show(
                        "Roof Tag",
                        "No roof tag family loaded in this project.");
                    return Result.Failed;
                }

                // --------------------------------------------------
                // 4. Define placement intent (TEMP DEFAULTS)
                // (Later this comes from ViewModel / UI)
                // --------------------------------------------------
                TagPlacementCorner corner =
                    TagPlacementCorner.TopRight;

                TagPlacementDirection direction =
                    TagPlacementDirection.Outward;

                bool useLeader = true;

                // --------------------------------------------------
                // 5. Call tagging service
                // --------------------------------------------------
                TaggingService service =
                    new TaggingService(uiApp);

                service.PlaceRoofTag(
                    roof,
                    view,
                    roofTagTypeId,
                    corner,
                    direction,
                    useLeader);

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User pressed ESC
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        // ==================================================
        // Selection Filter
        // ==================================================

        private class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is RoofBase;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
