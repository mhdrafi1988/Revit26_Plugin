// ==================================
// File: DetailItemPlacementService.cs
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Enhanced to handle placement of detail items along any curve type
    /// </summary>
    public class DetailItemPlacementService
    {
        private readonly Document _doc;
        private readonly ViewPlan _view;
        private const double MIN_CURVE_LENGTH = 0.0416667; // 6 inches in feet
        private const double PLACEMENT_SEGMENT_LENGTH = 0.5; // 6 inches for placement line

        public DetailItemPlacementService(
            Document doc,
            ViewPlan view)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        /// <summary>
        /// Places detail items along a collection of curves (handles any curve type)
        /// </summary>
        public void PlaceAlongCurves(
            IList<Curve> curves,
            FamilySymbol symbol,
            LoggingService log)
        {
            if (curves == null || curves.Count == 0)
            {
                log.Warning("No curves provided for placement.");
                return;
            }

            if (symbol == null)
            {
                log.Warning("Detail item symbol is null.");
                return;
            }

            // Activate symbol if needed
            if (!symbol.IsActive)
            {
                try
                {
                    symbol.Activate();
                    _doc.Regenerate();
                    log.Info($"Activated symbol: {symbol.Name}");
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to activate symbol: {ex.Message}");
                    return;
                }
            }

            int placed = 0;
            int failed = 0;
            double geometryTolerance = _doc.Application.ShortCurveTolerance;

            foreach (var curve in curves)
            {
                if (curve == null || curve.Length < geometryTolerance)
                {
                    failed++;
                    continue;
                }

                try
                {
                    bool success = PlaceDetailItemOnCurve(curve, symbol);
                    if (success)
                        placed++;
                    else
                        failed++;
                }
                catch (Exception ex)
                {
                    log.Warning($"Failed to place item on curve: {ex.Message}");
                    failed++;
                }
            }

            log.Info($"Detail items placed: {placed} successful, {failed} failed");
        }

        /// <summary>
        /// Places a detail item on a specific curve
        /// </summary>
        private bool PlaceDetailItemOnCurve(Curve curve, FamilySymbol symbol)
        {
            FamilyInstance instance = null;

            // Strategy 1: For long curves, place at midpoint with orientation
            if (curve.Length >= MIN_CURVE_LENGTH)
            {
                XYZ midPoint = curve.Evaluate(0.5, true);
                XYZ tangent = GetCurveTangent(curve, 0.5);

                // Create a small line segment at midpoint oriented along curve
                XYZ p1 = midPoint - tangent * (PLACEMENT_SEGMENT_LENGTH / 2);
                XYZ p2 = midPoint + tangent * (PLACEMENT_SEGMENT_LENGTH / 2);
                Line placementLine = Line.CreateBound(p1, p2);

                instance = _doc.Create.NewFamilyInstance(
                    placementLine,
                    symbol,
                    _view);
            }
            // Strategy 2: For very short curves, use the curve itself
            else if (curve is Line line)
            {
                instance = _doc.Create.NewFamilyInstance(
                    line,
                    symbol,
                    _view);
            }
            // Strategy 3: For other short curves, use line between endpoints
            else
            {
                XYZ p1 = curve.GetEndPoint(0);
                XYZ p2 = curve.GetEndPoint(1);
                Line fallbackLine = Line.CreateBound(p1, p2);

                instance = _doc.Create.NewFamilyInstance(
                    fallbackLine,
                    symbol,
                    _view);
            }

            return instance != null;
        }

        /// <summary>
        /// Gets the tangent vector at a parameter along the curve
        /// </summary>
        private XYZ GetCurveTangent(Curve curve, double parameter)
        {
            try
            {
                Transform derivative = curve.ComputeDerivatives(parameter, true);
                XYZ tangent = derivative.BasisX.Normalize();

                // Project to horizontal plane for plan view
                return new XYZ(tangent.X, tangent.Y, 0).Normalize();
            }
            catch
            {
                // Fallback: use direction between endpoints
                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                return (end - start).Normalize();
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public void PlaceAlongLines(
            IList<Line> lines,
            FamilySymbol symbol,
            LoggingService log)
        {
            var curves = new List<Curve>();
            foreach (var line in lines)
            {
                curves.Add(line);
            }
            PlaceAlongCurves(curves, symbol, log);
        }
    }
}