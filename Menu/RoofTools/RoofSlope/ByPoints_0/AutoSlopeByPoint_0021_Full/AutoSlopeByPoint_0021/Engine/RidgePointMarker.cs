// =======================================================
// File: RidgePointMarker.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V021
// New in V021 — Ridge Point Detection sub-feature (opt-in,
// on-by-default whenever Ridge Point Detection is enabled;
// independent toggle: MarkRidgePointsInView).
//
// Draws a circle (DetailCurve) at each resolved ridge point, in the
// ACTIVE VIEW only, per confirmed spec:
//   - Size: user-selected radius in mm (default 250mm / 500mm diameter).
//   - Style: user-selected, from line styles CURRENTLY IN USE somewhere
//     in the project (see GetUsedLineStyleNames). Its GraphicsStyle line
//     color is overridden to the user-selected color — a GLOBAL change,
//     confirmed acceptable (every other element using that same style
//     also picks up the new color).
//   - Color: user-selected from a small fixed palette (see
//     RidgeMarkerColorPalette), default Red.
//   - Position: ridge point's 3D location projected flat onto the
//     active view's own sketch/work plane (ignores roof slope —
//     standard flat plan-view marker, not a true 3D tilted circle).
//   - Runs in its own SubTransaction, non-fatal — logs and skips
//     individual failures rather than aborting the run.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint.V021.Core.Engine
{
    /// <summary>
    /// Fixed color palette for ridge-point markers — deliberately small
    /// ("pick box, easiest options" per Rafi) rather than a full custom
    /// color picker. Default is Red, matching the original hardcoded behavior.
    /// </summary>
    public static class RidgeMarkerColorPalette
    {
        public const string DefaultColorName = "Red";

        public static readonly Dictionary<string, Color> Colors = new()
        {
            ["Red"]    = new Color(255, 0, 0),
            ["Orange"] = new Color(255, 140, 0),
            ["Yellow"] = new Color(255, 220, 0),
            ["Green"]  = new Color(0, 160, 0),
            ["Blue"]   = new Color(0, 90, 255),
            ["Black"]  = new Color(0, 0, 0),
        };

        public static List<string> Names => Colors.Keys.ToList();

        public static Color Resolve(string name)
            => Colors.TryGetValue(name ?? string.Empty, out var c) ? c : Colors[DefaultColorName];
    }

    public static class RidgePointMarker
    {
        public const double DefaultRadiusMm = 250.0;

        /// <summary>
        /// Draws one circle per ridge point position in the active view.
        /// Must be called with an already-open host transaction (this method
        /// opens its own SubTransaction internally, per the confirmed
        /// "isolated, retryable" convention). Returns the number of circles
        /// successfully drawn. Never throws — all failures are logged and
        /// skipped so a bad view or missing style doesn't abort the whole run.
        /// </summary>
        /// <param name="lineStyleName">
        /// Name of an EXISTING line-style subcategory (under Lines) to reuse
        /// and recolor. Must be one already in use somewhere in the project —
        /// see GetUsedLineStyleNames. If not found at draw time (e.g. renamed
        /// or removed between window-open and Run), logs a warning and falls
        /// back to the default line style — never aborts the run.
        /// </param>
        /// <param name="colorName">Name from RidgeMarkerColorPalette (default "Red" if unrecognized).</param>
        /// <param name="radiusMm">Circle radius in mm (default 250mm if &lt;= 0).</param>
        public static int DrawRidgePointCircles(
            Document doc,
            View activeView,
            List<XYZ> ridgePointPositions,
            string lineStyleName,
            string colorName,
            double radiusMm,
            Action<LogEntry> log)
        {
            if (activeView == null)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    "RIDGE-MARK: No active view available. Skipped drawing ridge-point circles."));
                return 0;
            }

            if (!CanHostDetailCurves(activeView))
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    $"RIDGE-MARK: Active view '{activeView.Name}' cannot host detail lines (not a plan/section/drafting view). Skipped."));
                return 0;
            }

            if (ridgePointPositions == null || ridgePointPositions.Count == 0)
                return 0;

            if (string.IsNullOrWhiteSpace(lineStyleName))
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    "RIDGE-MARK: No line style selected (no used line styles found in project). Skipped drawing ridge-point circles."));
                return 0;
            }

            double effectiveRadiusMm = radiusMm > 0 ? radiusMm : DefaultRadiusMm;
            double radiusFt = UnitUtils.ConvertToInternalUnits(effectiveRadiusMm, UnitTypeId.Millimeters);
            Color markerColor = RidgeMarkerColorPalette.Resolve(colorName);
            int drawn = 0;

            using (SubTransaction subTx = new SubTransaction(doc))
            {
                subTx.Start();
                try
                {
                    // Derive the flat plane detail lines actually land on. For plan
                    // views (FloorPlan/CeilingPlan/EngineeringPlan/AreaPlan), that's
                    // the view's associated Level elevation, with a level (Z-up)
                    // normal — NOT activeView.Origin, which is a view-specific eye
                    // point and not guaranteed to sit at the plan's cut elevation.
                    // For non-level-based views (Section/Elevation/Detail/Drafting),
                    // the view's own direction/origin is the correct plane.
                    Plane viewPlane = GetFlatViewPlane(activeView);

                    GraphicsStyle markerStyle = GetOrRecolorLineStyle(doc, lineStyleName, markerColor, log);

                    foreach (XYZ pt in ridgePointPositions)
                    {
                        try
                        {
                            // Flat projection onto the active view's own plane —
                            // ignores roof slope at this point, per confirmed spec.
                            XYZ flatCenter = ProjectOntoPlane(pt, viewPlane);

                            // Build the circle as two half-arcs — Revit's Arc.Create
                            // does not support a full 360° single arc.
                            XYZ xDir = viewPlane.XVec.Normalize();
                            XYZ yDir = viewPlane.YVec.Normalize();
                            XYZ p0 = flatCenter + xDir * radiusFt;
                            XYZ p1 = flatCenter - xDir * radiusFt;

                            Arc half1 = Arc.Create(p0, p1, flatCenter + yDir * radiusFt);
                            Arc half2 = Arc.Create(p1, p0, flatCenter - yDir * radiusFt);

                            DetailCurve dc1 = doc.Create.NewDetailCurve(activeView, half1);
                            DetailCurve dc2 = doc.Create.NewDetailCurve(activeView, half2);

                            if (markerStyle != null)
                            {
                                dc1.LineStyle = markerStyle;
                                dc2.LineStyle = markerStyle;
                            }

                            drawn += 1; // one logical circle (2 DetailCurve halves)
                        }
                        catch (Exception exInner)
                        {
                            log?.Invoke(new LogEntry(LogLevel.Warning,
                                $"RIDGE-MARK: Failed to draw circle at ({pt.X:F3},{pt.Y:F3},{pt.Z:F3}): {exInner.Message}. Skipped."));
                        }
                    }

                    subTx.Commit();
                }
                catch (Exception ex)
                {
                    subTx.RollBack();
                    log?.Invoke(new LogEntry(LogLevel.Warning,
                        $"RIDGE-MARK: Sub-transaction failed, no circles drawn: {ex.Message}"));
                    return 0;
                }
            }

            return drawn;
        }

        /// <summary>
        /// Scans the document for every distinct line-style name currently
        /// referenced by an existing DetailCurve/CurveElement, per confirmed
        /// spec ("used" = actually referenced by a curve element right now,
        /// not just present as an unused subcategory). Returns a sorted,
        /// de-duplicated list of names. Never throws — returns an empty list
        /// on any failure, logged as a warning, so a bad scan never blocks
        /// the window from opening.
        /// </summary>
        public static List<string> GetUsedLineStyleNames(Document doc, Action<LogEntry> log = null)
        {
            try
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(CurveElement));

                foreach (Element el in collector)
                {
                    if (el is CurveElement ce && ce.LineStyle is GraphicsStyle gs && gs.GraphicsStyleCategory != null)
                    {
                        string name = gs.GraphicsStyleCategory.Name;
                        if (!string.IsNullOrWhiteSpace(name))
                            names.Add(name);
                    }
                }

                return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    $"RIDGE-MARK: Failed to scan used line styles: {ex.Message}"));
                return new List<string>();
            }
        }

        private static bool CanHostDetailCurves(View view)
        {
            // Detail lines require a view that supports 2D annotation geometry —
            // plan, section, elevation, drafting, or detail views. 3D views and
            // schedules do not support NewDetailCurve.
            switch (view.ViewType)
            {
                case ViewType.FloorPlan:
                case ViewType.CeilingPlan:
                case ViewType.Section:
                case ViewType.Elevation:
                case ViewType.Detail:
                case ViewType.DraftingView:
                case ViewType.EngineeringPlan:
                case ViewType.AreaPlan:
                    return true;
                default:
                    return false;
            }
        }

        private static Plane GetFlatViewPlane(View view)
        {
            bool isPlanBased =
                view.ViewType == ViewType.FloorPlan ||
                view.ViewType == ViewType.CeilingPlan ||
                view.ViewType == ViewType.EngineeringPlan ||
                view.ViewType == ViewType.AreaPlan;

            if (isPlanBased && view.GenLevel != null)
            {
                // Flat, level-based plane: normal = world Z, origin at the level's elevation.
                XYZ origin = new XYZ(0, 0, view.GenLevel.Elevation);
                return Plane.CreateByNormalAndOrigin(XYZ.BasisZ, origin);
            }

            // Section/Elevation/Detail/Drafting: the view's own direction/origin
            // is the meaningful "flat" plane for that view type.
            return Plane.CreateByNormalAndOrigin(view.ViewDirection, view.Origin);
        }

        private static XYZ ProjectOntoPlane(XYZ point, Plane plane)
        {
            XYZ toPoint = point - plane.Origin;
            double distAlongNormal = toPoint.DotProduct(plane.Normal);
            return point - plane.Normal * distAlongNormal;
        }

        /// <summary>
        /// Looks up the existing line-style GraphicsStyle by name (under the
        /// Lines category) and overrides its line color to the user-selected
        /// color. This is a GLOBAL change — every element already using this
        /// style will also render in the new color — confirmed acceptable per
        /// spec, in favor of creating a new dedicated style. Returns null
        /// (logged) if the style isn't found, in which case circles are still
        /// drawn using the default style.
        /// </summary>
        private static GraphicsStyle GetOrRecolorLineStyle(
            Document doc, string styleName, Color color, Action<LogEntry> log)
        {
            Category linesCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (linesCategory == null)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    "RIDGE-MARK: Lines category not found. Circles will use default style."));
                return null;
            }

            foreach (Category sub in linesCategory.SubCategories)
            {
                if (!string.Equals(sub.Name, styleName, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    sub.LineColor = color;
                }
                catch (Exception ex)
                {
                    log?.Invoke(new LogEntry(LogLevel.Warning,
                        $"RIDGE-MARK: Could not set color on '{styleName}': {ex.Message}. Using existing color."));
                }

                return sub.GetGraphicsStyle(GraphicsStyleType.Projection);
            }

            log?.Invoke(new LogEntry(LogLevel.Warning,
                $"RIDGE-MARK: Line style '{styleName}' not found in project. Circles will use default style."));
            return null;
        }
    }
}
