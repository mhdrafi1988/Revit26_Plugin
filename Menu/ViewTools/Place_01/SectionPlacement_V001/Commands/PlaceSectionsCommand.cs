using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionManager_V07.Helpers;
using Revit26_Plugin.SectionManager_V07.Services;

namespace Revit26_Plugin.SectionManager_V07.Commands
{
    /// <summary>
    /// Places selected section views onto selected sheets.
    /// Uses a single transaction (single undo).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class PlaceSectionsCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;

            if (uiDoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            Document doc = uiDoc.Document;

            // ---------------------------------------------
            // Get placement context (from ViewModel / UI)
            // ---------------------------------------------
            SectionPlacementContext context =
                SectionPlacementContext.Current;

            if (context == null || !context.HasValidData)
            {
                TaskDialog.Show(
                    "Place Sections",
                    "No sections or sheets selected.");
                return Result.Cancelled;
            }

            IList<ElementId> sectionIds = context.SectionViewIds;
            IList<ElementId> sheetIds = context.SheetIds;

            int placedCount = 0;
            int skippedCount = 0;

            using (Transaction tx = new Transaction(doc, "Place Sections on Sheets"))
            {
                tx.Start();

                foreach (ElementId sheetId in sheetIds)
                {
                    ViewSheet sheet = doc.GetElement(sheetId) as ViewSheet;
                    if (sheet == null)
                        continue;

                    XYZ basePoint = GetNextViewportLocation(sheet);

                    foreach (ElementId viewId in sectionIds)
                    {
                        ViewSection view = doc.GetElement(viewId) as ViewSection;
                        if (view == null)
                            continue;

                        // Skip if already placed on this sheet
                        if (Viewport.CanAddViewToSheet(doc, sheetId, viewId))
                        {
                            Viewport.Create(doc, sheetId, viewId, basePoint);
                            placedCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }

                        // Offset next viewport position
                        basePoint = basePoint.Add(new XYZ(0, -200, 0));
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show(
                "Place Sections",
                $"Placed: {placedCount}\nSkipped: {skippedCount}");

            return Result.Succeeded;
        }

        /// <summary>
        /// Finds a safe starting point for placing viewports.
        /// Simple vertical stacking strategy.
        /// </summary>
        private XYZ GetNextViewportLocation(ViewSheet sheet)
        {
            // Revit sheet coordinates are in feet
            return new XYZ(1.0, 1.0, 0);
        }
    }
}
