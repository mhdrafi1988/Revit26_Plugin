using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    /// <summary>
    /// Safely collects Detail Item FamilySymbols that support
    /// two-point (line-based) placement.
    /// </summary>
    public class LineBasedDetailItemCollectorService
    {
        public IList<FamilySymbol> Collect(Document doc, View view)
        {
            List<FamilySymbol> result = new();

            if (doc == null || view == null)
                return result;

            if (view is View3D || view is ViewSheet)
                return result;

            // ------------------------------------------------------------
            // 1. SNAPSHOT SYMBOL IDS FIRST (NO DOC MODIFICATION)
            // ------------------------------------------------------------
            List<ElementId> symbolIds = new();

            var collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .OfClass(typeof(FamilySymbol));

            foreach (FamilySymbol symbol in collector)
            {
                symbolIds.Add(symbol.Id);
            }

            if (symbolIds.Count == 0)
                return result;

            // ------------------------------------------------------------
            // 2. TEST LINE-BASED PLACEMENT SAFELY
            // ------------------------------------------------------------
            Line testLine = Line.CreateBound(
                new XYZ(0, 0, 0),
                new XYZ(10, 0, 0));

            using (Transaction t = new Transaction(doc, "Test Line-Based Detail Items"))
            {
                t.Start();

                foreach (ElementId id in symbolIds)
                {
                    FamilySymbol symbol = doc.GetElement(id) as FamilySymbol;
                    if (symbol == null)
                        continue;

                    try
                    {
                        if (!symbol.IsActive)
                            symbol.Activate();

                        FamilyInstance fi =
                            doc.Create.NewFamilyInstance(
                                testLine,
                                symbol,
                                view);

                        if (fi != null)
                        {
                            result.Add(symbol);

                            // Clean up test instance
                            doc.Delete(fi.Id);
                        }
                    }
                    catch
                    {
                        // Not line-based or not valid in this view
                        continue;
                    }
                }

                // IMPORTANT: Roll back so NOTHING persists
                t.RollBack();
            }

            return result;
        }
    }
}
