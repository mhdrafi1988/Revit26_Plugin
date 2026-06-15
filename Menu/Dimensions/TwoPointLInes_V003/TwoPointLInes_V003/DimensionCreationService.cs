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
            DimensionType dimTypeElem =
                doc.GetElement(dimType.ElementId) as DimensionType;

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

            if (!instances.Any())
            {
                log.Insert(0, "No matching line-based detail items found.");
                return;
            }

            int created = 0;
            int failed = 0;

            using Transaction tx =
                new Transaction(doc, "Line-Based Detail Length Dimension");
            tx.Start();

            foreach (var fi in instances)
            {
                try
                {
                    // 1️⃣ Validate straight line-based detail item
                    if (fi.Location is not LocationCurve lc)
                        throw new Exception("Detail item has no LocationCurve.");

                    if (lc.Curve is not Line baseLine)
                        throw new Exception("Detail item is not a straight line.");

                    XYZ p0 = baseLine.GetEndPoint(0);
                    XYZ p1 = baseLine.GetEndPoint(1);

                    XYZ lineDir = baseLine.Direction.Normalize();
                    XYZ perpDir = lineDir.CrossProduct(XYZ.BasisZ).Normalize();

                    if (perpDir.IsZeroLength())
                        throw new Exception("Invalid perpendicular direction.");

                    // 2️⃣ Helper tick size (reference geometry)
                    double tickSize =
                        UnitUtils.ConvertToInternalUnits(50, UnitTypeId.Millimeters);

                    // START tick
                    Line startTick = Line.CreateBound(
                        p0 - perpDir * tickSize,
                        p0 + perpDir * tickSize);

                    DetailCurve startRef =
                        doc.Create.NewDetailCurve(view, startTick);

                    // END tick
                    Line endTick = Line.CreateBound(
                        p1 - perpDir * tickSize,
                        p1 + perpDir * tickSize);

                    DetailCurve endRef =
                        doc.Create.NewDetailCurve(view, endTick);

                    ReferenceArray ra = new ReferenceArray();
                    ra.Append(startRef.GeometryCurve.Reference);
                    ra.Append(endRef.GeometryCurve.Reference);

                    // 3️⃣ Dimension line (PARALLEL to detail line)
                    double offset =
                        UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters);

                    Line dimLine = Line.CreateBound(
                        p0 + perpDir * offset,
                        p1 + perpDir * offset);

                    // 4️⃣ Create dimension (this measures start → end length)
                    doc.Create.NewDimension(view, dimLine, ra, dimTypeElem);

                    created++;
                }
                catch (Exception ex)
                {
                    failed++;
                    log.Insert(0, $"Skipped {fi.Id.Value}: {ex.Message}");
                }
            }

            if (created > 0)
                tx.Commit();
            else
                tx.RollBack();

            log.Insert(0, $"Created: {created}, Failed: {failed}");
        }
    }
}
