using Autodesk.Revit.DB;
using Revit26_Plugin.LinesFromMechanical.V009.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.LinesFromMechanical.V009.Services;

public sealed class LinkedMechanicalCircleService : LinkedMechanicalElementProcessor
{
    private double _radiusFeet;
    private Color  _color = new(255, 0, 0);

    public OperationSummary CreateDetailLines(
        Document hostDoc,
        ViewPlan view,
        RevitLinkInstance selectedLink,
        string selectedFamilyName,
        double radiusMm,
        Color circleColor)
    {
        _radiusFeet = UnitHelper.MillimetersToFeet(radiusMm);
        _color      = circleColor;

        Log(LogLevel.Info, $"Mode: Detail Lines, radius {radiusMm} mm");
        Log(LogLevel.Info, $"Colour: R={circleColor.Red}, G={circleColor.Green}, B={circleColor.Blue}");

        return Run(hostDoc, view, selectedLink, selectedFamilyName, radiusMm);
    }

    protected override bool ExistsForSource(Document hostDoc, ViewPlan view, string sourceKey)
        => CircleIdentityStorage.DetailCurveExistsForSource(hostDoc, view, sourceKey);

    protected override void ExecuteCreation(
        Document hostDoc, ViewPlan view, IReadOnlyList<CreationItem> items, OperationSummary summary)
    {
        var createdCurveIds = new List<ElementId>();

        // TX 1 — create curves
        using (var tx = new Transaction(hostDoc, "Create Detail Circles at Linked Mechanical Equipment"))
        {
            tx.Start();
            int progress = 0;
            foreach (var item in items)
            {
                progress++;
                if (progress % 10 == 0) Log(LogLevel.Info, $"Progress: {progress}/{items.Count} circles created");

                var curves = CreateDetailCurves(hostDoc, view, item.Center, _radiusFeet, item.SourceKey);
                if (curves.Count > 0)
                {
                    foreach (var c in curves) createdCurveIds.Add(c.Id);
                    summary.DetailLinesCreated++;
                    Log(LogLevel.Success, $"Created detail lines at element {item.Element.Id}");
                }
                else summary.SkippedElements++;
            }
            tx.Commit();
            Log(LogLevel.Success, $"Creation transaction committed. Detail lines created: {summary.DetailLinesCreated}");
        }

        // TX 2 — colour overrides
        if (createdCurveIds.Count > 0)
        {
            try
            {
                using var tx = new Transaction(hostDoc, "Apply Color Overrides to Detail Circles");
                tx.Start();
                var ogs = new OverrideGraphicSettings();
                ogs.SetProjectionLineColor(_color);
                foreach (var id in createdCurveIds)
                    view.SetElementOverrides(id, ogs);
                tx.Commit();
                Log(LogLevel.Success, "Override transaction committed.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Warning, $"Could not apply color overrides: {ex.Message}");
            }
        }
    }

    private static List<DetailCurve> CreateDetailCurves(
        Document doc, View view, XYZ center, double radius, string sourceKey)
    {
        XYZ xDir = view.RightDirection.Normalize();
        XYZ yDir = view.UpDirection.Normalize();

        Arc[] arcs =
        [
            Arc.Create(center, radius, 0,               Math.PI / 2.0, xDir, yDir),
            Arc.Create(center, radius, Math.PI / 2.0,   Math.PI,       xDir, yDir),
            Arc.Create(center, radius, Math.PI,         Math.PI * 1.5, xDir, yDir),
            Arc.Create(center, radius, Math.PI * 1.5,   Math.PI * 2.0, xDir, yDir),
        ];

        var created = new List<DetailCurve>();
        foreach (Arc arc in arcs)
        {
            DetailCurve dc = doc.Create.NewDetailCurve(view, arc);
            CircleIdentityStorage.AttachSourceKey(dc, sourceKey, CircleIdentityStorage.LinkedElementType.DetailLine);
            created.Add(dc);
        }
        return created;
    }
}
