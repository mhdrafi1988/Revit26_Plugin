using Autodesk.Revit.DB;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.DtlLineDim_V03.Models;

namespace Revit26_Plugin.DtlLineDim_V03.Services
{
    public static class DimensionCreationService
    {
        public static void CreateDimensions(
            Document doc,
            View view,
            ComboItem detailType,
            ComboItem dimType,
            ObservableCollection<string> log)
        {
            var dimTypeElem = doc.GetElement(dimType.ElementId) as DimensionType;
            if (dimTypeElem == null)
            {
                log.Insert(0, "Invalid Dimension Type.");
                return;
            }

            var instances = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Id == detailType.ElementId)
                .ToList();

            int created = 0;
            int failed = 0;

            using Transaction tx = new Transaction(doc, "Two-Point Detail Dimension (No Helpers)");
            tx.Start();

            foreach (var fi in instances)
            {
                try
                {
                    if (fi.Location is not LocationCurve lc || lc.Curve is not Line line)
                        throw new Exception("Not a straight two-point detail item.");

                    // 🔑 THIS is the key
                    var strongRefs = fi
                        .GetReferences(FamilyInstanceReferenceType.StrongReference)
                        .ToList();

                    if (strongRefs.Count < 2)
                        throw new Exception("Strong references not found. Fix family.");

                    ReferenceArray ra = new ReferenceArray();
                    ra.Append(strongRefs[0]);
                    ra.Append(strongRefs[1]);

                    XYZ dir = line.Direction.Normalize();
                    XYZ perp = dir.CrossProduct(XYZ.BasisZ).Normalize();

                    double offset =
                        UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters);

                    Line dimLine = Line.CreateBound(
                        line.GetEndPoint(0) + perp * offset,
                        line.GetEndPoint(1) + perp * offset);

                    doc.Create.NewDimension(view, dimLine, ra, dimTypeElem);

                    created++;
                }
                catch (Exception ex)
                {
                    failed++;
                    log.Insert(0, $"Skipped {fi.Id.Value}: {ex.Message}");
                }
            }

            if (created > 0) tx.Commit();
            else tx.RollBack();

            log.Insert(0, $"Created: {created}, Failed: {failed}");
        }
    }
}
