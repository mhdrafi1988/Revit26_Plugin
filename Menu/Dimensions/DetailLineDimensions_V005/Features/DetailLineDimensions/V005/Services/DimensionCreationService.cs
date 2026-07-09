using Autodesk.Revit.DB;
using System;
using System.Linq;
using Revit26_Plugin.DetailLIneDimensions.V005.Models;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DetailLIneDimensions.V005.Services
{
    public static class DimensionCreationService
    {
        public static DimensionResult CreateDimensions(
            Document doc,
            View view,
            ComboItem detailType,
            ComboItem dimType)
        {
            var result = new DimensionResult();

            var dimTypeElem = doc.GetElement(dimType.ElementId) as DimensionType;
            if (dimTypeElem == null)
            {
                result.Failed++;
                result.Entries.Add(new LogEntry(LogLevel.Error, "Invalid dimension type."));
                return result;
            }

            var instances = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Id == detailType.ElementId)
                .ToList();

            using Transaction tx = new Transaction(doc, "Two-Point Detail Dimension");
            var options = tx.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new DimensionFailuresPreprocessor());
            tx.SetFailureHandlingOptions(options);
            tx.Start();

            foreach (var fi in instances)
            {
                try
                {
                    if (fi.Location is not LocationCurve lc || lc.Curve is not Line line)
                        throw new Exception("Not a straight two-point detail item.");

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

                    result.Placed++;
                    result.Entries.Add(new LogEntry(LogLevel.Success,
                        $"Point #{fi.Id.Value} dimensioned."));
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    result.Entries.Add(new LogEntry(LogLevel.Warning,
                        $"Point #{fi.Id.Value} — {ex.Message} — silent skip."));
                }
            }

            if (result.Placed > 0)
            {
                tx.Commit();
            }
            else
            {
                tx.RollBack();

                // Nothing survived — reclassify the run as a hard failure, not silent skips.
                result.Failed += result.Skipped;
                result.Skipped = 0;
                for (int i = 0; i < result.Entries.Count; i++)
                {
                    if (result.Entries[i].Level == LogLevel.Warning)
                        result.Entries[i] = new LogEntry(LogLevel.Error,
                            result.Entries[i].Message + " (transaction rolled back)");
                }
            }

            result.Entries.Add(new LogEntry(LogLevel.Info,
                $"Run complete — placed: {result.Placed}, skipped: {result.Skipped}, failed: {result.Failed}."));

            return result;
        }
    }
}
