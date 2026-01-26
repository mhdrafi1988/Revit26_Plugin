using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofFromFloor.ViewModels;
using System;

namespace Revit26_Plugin.RoofFromFloor.ExternalEvents
{
    public class LinkSelectionHandler : IExternalEventHandler
    {
        public RoofFromFloorViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                ViewModel.LogFromExternal("Select a linked model instance...");

                Reference r = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new LinkSelectionFilter(),
                    "Select a Revit link");

                if (r == null) return;

                RevitLinkInstance link = doc.GetElement(r) as RevitLinkInstance;
                if (link == null)
                {
                    ViewModel.LogFromExternal("? Selected element is not a Revit Link.");
                    return;
                }

                ViewModel.SetSelectedLink(link);
                ViewModel.LogFromExternal($"? Link selected: {link.Name}");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                ViewModel.LogFromExternal("Link selection cancelled by user.");
            }
            catch (Exception ex)
            {
                ViewModel.LogFromExternal($"? Link selection failed: {ex.Message}");
            }
        }

        public string GetName() => "Link Selection Handler";
    }

    internal class LinkSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is RevitLinkInstance;
        }

        public bool AllowReference(Reference reference, XYZ position) => true;
    }
}
