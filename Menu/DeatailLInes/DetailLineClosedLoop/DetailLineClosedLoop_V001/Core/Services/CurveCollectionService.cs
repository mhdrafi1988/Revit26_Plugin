using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Services
{
    /// <summary>Step 1 — pulls geometric Curves out of the selected Detail Curve elements.</summary>
    public static class CurveCollectionService
    {
        public static List<Curve> CollectCurves(Document doc, ICollection<ElementId> ids, ObservableCollection<LogEntry> log)
        {
            var curves = new List<Curve>();
            int lineCount = 0, arcCount = 0, otherCount = 0, skipped = 0;
            double shortCurveTolerance = doc.Application.ShortCurveTolerance;

            foreach (ElementId id in ids)
            {
                if (doc.GetElement(id) is DetailCurve dc && dc.CurveElementType == CurveElementType.DetailCurve)
                {
                    Curve c = dc.GeometryCurve;
                    if (c == null || c.Length < shortCurveTolerance)
                    {
                        skipped++;
                        continue;
                    }

                    curves.Add(c);
                    if (c is Line) lineCount++;
                    else if (c is Arc) arcCount++;
                    else otherCount++;
                }
                else
                {
                    skipped++;
                }
            }

            string otherPart = otherCount > 0 ? $", {otherCount} Other" : string.Empty;
            string skipPart = skipped > 0 ? $" — {skipped} skipped (not detail lines/arcs)" : string.Empty;
            log.Add(new LogEntry(LogLevel.Info,
                $"Collected {curves.Count} curves from selection ({lineCount} Line, {arcCount} Arc{otherPart}){skipPart}"));

            return curves;
        }
    }
}
