using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.SectionManagerMVVMv4;
using System;
using System.Linq;
using System.Windows.Interop;

namespace Revit22_Plugin.SectionManagerMVVMv4
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceUnplacedSectionsv4 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var vm = new SectionPlacementViewModel(doc);
            var window = new SectionPlacementWindow
            {
                DataContext = vm
            };
            // hook window owner
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

            if (window.ShowDialog() == true)
            {
                // get title block symbol
                var tbSym = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault(s => s.Name == vm.SelectedTitleBlock);
                if (tbSym == null)
                {
                    TaskDialog.Show("Error", $"Title block '{vm.SelectedTitleBlock}' not found.");
                    return Result.Failed;
                }
                if (!tbSym.IsActive) tbSym.Activate();

                // convert mm → internal
                double hGap = UnitUtils.ConvertToInternalUnits(vm.HorizontalGapMM, UnitTypeId.Millimeters);
                double vGap = UnitUtils.ConvertToInternalUnits(vm.VerticalGapMM, UnitTypeId.Millimeters);

                var sections = window.SelectedSections;
                int perSheet = vm.Rows * vm.Columns;
                int sheetCount = 0;
                ElementId lastSheetId = ElementId.InvalidElementId;

                using (var tx = new Transaction(doc, "Place Sections"))
                {
                    tx.Start();

                    for (int i = 0; i < sections.Count; i += perSheet)
                    {
                        var batch = sections.Skip(i).Take(perSheet).ToList();

                        // create sheet
                        var sheet = ViewSheet.Create(doc, tbSym.Id);
                        sheet.Name = $"S-{++sheetCount:00}";
                        lastSheetId = sheet.Id;

                        // find top-left corner
                        XYZ origin = SectionPlacementHelpers.GetTopLeftCorner(sheet);

                        // place viewports
                        for (int j = 0; j < batch.Count; j++)
                        {
                            int row = j / vm.Columns;
                            int col = j % vm.Columns;
                            double x = origin.X + col * hGap;
                            double y = origin.Y - row * vGap;
                            var pt = new XYZ(x, y, 0);
                            Viewport.Create(doc, sheet.Id, batch[j].Id, pt);
                        }
                    }

                    tx.Commit();
                }

                // **Fix:** Get the View object before requesting change
                if (lastSheetId != ElementId.InvalidElementId)
                {
                    var lastView = doc.GetElement(lastSheetId) as View;
                    if (lastView != null)
                    {
                        uidoc.RequestViewChange(lastView);
                    }
                }
            }

            return Result.Succeeded;
        }
    }
}
