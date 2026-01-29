// ==================================================
// File: RoofCreationService.cs
// ==================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.RoofFromFloor.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofFromFloor.Services
{
    /// <summary>
    /// Creates a new footprint roof using the original roof footprint curves.
    /// Refactored for robust error handling and geometry validation.
    /// </summary>
    public static class RoofCreationService
    {
        private const double ExtraOffsetMm = 300.0;
        private const double MmToFeet = 1.0 / 304.8;

        public static bool TryCreateFootprintRoof(
            Document doc,
            RoofMemoryContext roofContext,
            RoofType roofType,
            Level level,
            Action<string> log)
        {
            // Preliminary Input Check
            if (doc == null || roofContext == null || roofType == null || level == null)
            {
                log("? ABORT: One or more required inputs (Doc, Context, Type, or Level) are null.");
                return false;
            }

            // 1. Prepare and Validate Geometry
            List<Curve> footprint = roofContext.RoofFootprintCurves
                .Select(c => FlattenCurveToZ(c, level.Elevation))
                .ToList();

            if (footprint.Count < 3)
            {
                log("? Roof footprint has fewer than 3 curves.");
                return false;
            }

            // 2. Ensure CCW Orientation (Required by Revit)
            if (!IsCounterClockwise(footprint))
            {
                log("? Footprint is clockwise. Reversing to CCW.");
                footprint.Reverse();
                footprint = footprint.Select(c => c.CreateReversed()).ToList();
            }

            log($"Final ordered footprint curves: {footprint.Count}");

            double finalBaseOffset =
                roofContext.RoofBaseElevation + (ExtraOffsetMm * MmToFeet);

            try
            {
                using (Transaction tx = new Transaction(doc, "Create Roof From Floor"))
                {
                    tx.Start();

                    // Convert List to Revit's CurveArray
                    CurveArray curveArray = new CurveArray();
                    foreach (Curve c in footprint)
                    {
                        curveArray.Append(c);
                    }

                    log("Calling NewFootPrintRoof()");

                    // 3. Attempt Creation
                    ModelCurveArray modelCurves = new ModelCurveArray();
                    FootPrintRoof roof = doc.Create.NewFootPrintRoof(
                        curveArray,
                        level,
                        roofType,
                        out modelCurves);

                    // 4. Robust Null Check to prevent "Value cannot be null"
                    if (roof == null)
                    {
                        log("? Revit API failed to create roof object (returned null). Check curve continuity.");
                        tx.RollBack();
                        return false;
                    }

                    // 5. Parameter Validation
                    Parameter offsetParam = roof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM);
                    if (offsetParam != null && !offsetParam.IsReadOnly)
                    {
                        offsetParam.Set(finalBaseOffset);
                    }
                    else
                    {
                        log("! Warning: ROOF_LEVEL_OFFSET_PARAM is null or read-only on this RoofType.");
                    }

                    tx.Commit();
                }

                log("? ROOF CREATED SUCCESSFULLY");
                return true;
            }
            catch (Exception ex)
            {
                log($"? Roof creation failed: {ex.Message}");
                if (ex.InnerException != null) log($"-> Inner Detail: {ex.InnerException.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the footprint is CCW using the shoelace formula on the XY plane.
        /// </summary>
        private static bool IsCounterClockwise(List<Curve> curves)
        {
            double area = 0.0;
            for (int i = 0; i < curves.Count; i++)
            {
                XYZ p1 = curves[i].GetEndPoint(0);
                XYZ p2 = curves[i].GetEndPoint(1); // Standardizing on start/end of current curve
                area += (p2.X - p1.X) * (p2.Y + p1.Y);
            }
            return area < 0;
        }

        /// <summary>
        /// Projects curves onto a flat Z plane to ensure valid footprint creation.
        /// </summary>
        private static Curve FlattenCurveToZ(Curve c, double z)
        {
            XYZ p0 = c.GetEndPoint(0);
            XYZ p1 = c.GetEndPoint(1);

            return Line.CreateBound(
                new XYZ(p0.X, p0.Y, z),
                new XYZ(p1.X, p1.Y, z));
        }
    }
}