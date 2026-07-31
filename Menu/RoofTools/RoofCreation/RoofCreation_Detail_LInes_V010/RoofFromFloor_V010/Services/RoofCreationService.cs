using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RoofFromFloor_V010.Services
{
    public class RoofCreationService
    {
        public ElementId CreateRoof(Document doc, IList<Curve> profileCurves, Level targetLevel, RoofType roofType)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (profileCurves == null || profileCurves.Count == 0) throw new ArgumentException("Profile curves cannot be null or empty.", nameof(profileCurves));
            if (targetLevel == null) throw new ArgumentNullException(nameof(targetLevel), "Target level must be specified for roof creation.");
            if (roofType == null) throw new ArgumentNullException(nameof(roofType));

            ElementId createdRoofId = ElementId.InvalidElementId;

            // Ensure we are inside a valid transaction
            bool isInternalTransaction = false;
            Transaction tx = null;

            try
            {
                if (!doc.IsModifiable)
                {
                    tx = new Transaction(doc, "Create Roof from Floor");
                    tx.Start();
                    isInternalTransaction = true;
                }

                // 1. Flatten and validate curves onto the target level elevation
                CurveArray curveArray = new CurveArray();
                double targetElevation = targetLevel.Elevation;

                foreach (var curve in profileCurves)
                {
                    if (curve == null) continue;

                    XYZ p1 = curve.GetEndPoint(0);
                    XYZ p2 = curve.GetEndPoint(1);

                    // Force curves to sit strictly on the target level's Z-elevation plane
                    XYZ flatP1 = new XYZ(p1.X, p1.Y, targetElevation);
                    XYZ flatP2 = new XYZ(p2.X, p2.Y, targetElevation);

                    // Skip zero-length segments to prevent Revit geometry failure
                    if (flatP1.IsAlmostEqualTo(flatP2)) continue;

                    Curve flatLine = Line.CreateBound(flatP1, flatP2);
                    curveArray.Append(flatLine);
                }

                if (curveArray.IsEmpty)
                {
                    throw new InvalidOperationException("No valid non-zero curves remained after flattening for roof creation.");
                }

                // 2. Create the Footprint Roof using NewFootPrintRoof
                // ModelCurveArray is populated to track boundary lines if needed
                ModelCurveArray modelCurveArray = new ModelCurveArray();
                FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(curveArray, targetLevel, roofType, out modelCurveArray);

                if (footprintRoof != null)
                {
                    // 3. Set default slope or ensure multi-plane flags if required
                    // By default, make boundary lines non-sloping if flat roof, or adjust per requirements
                    foreach (ModelCurve modelCurve in modelCurveArray)
                    {
                        // Example: Set slope angle to 0 if it's a flat roof base, or keep default
                        // footprintRoof.SetSloped(modelCurve, false); 
                    }

                    doc.Regenerate();
                    createdRoofId = footprintRoof.Id;
                }

                if (isInternalTransaction && tx != null && tx.GetStatus() == TransactionStatus.Started)
                {
                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                if (isInternalTransaction && tx != null && tx.GetStatus() == TransactionStatus.Started)
                {
                    tx.RollBack();
                }
                throw new InvalidOperationException($"Failed to create roof on level '{targetLevel.Name}': {ex.Message}", ex);
            }
            finally
            {
                tx?.Dispose();
            }

            return createdRoofId;
        }
    }
}