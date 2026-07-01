using Autodesk.Revit.DB;
using Revit26_Plugin.LinesFromMechanical.V010.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.LinesFromMechanical.V010.Services;

public sealed class LinkedMechanicalFloorService : LinkedMechanicalElementProcessor
{
    private double    _radiusFeet;
    private double    _offsetFeet;
    private FloorType _floorType = null!;
    private Level     _targetLevel = null!;

    public OperationSummary CreateFloors(
        Document hostDoc,
        ViewPlan view,
        RevitLinkInstance selectedLink,
        string selectedFamilyName,
        double radiusMm,
        FloorType floorType,
        double offsetMm)
    {
        if (floorType == null)
        {
            Log(LogLevel.Error, "No floor type selected.");
            return new OperationSummary();
        }

        if (view.GenLevel == null)
        {
            Log(LogLevel.Error, "Active view has no associated level.");
            return new OperationSummary();
        }

        _radiusFeet  = UnitHelper.MillimetersToFeet(radiusMm);
        _offsetFeet  = UnitHelper.MillimetersToFeet(offsetMm);
        _floorType   = floorType;
        _targetLevel = view.GenLevel;

        Log(LogLevel.Info, $"Mode: Floors, radius {radiusMm} mm, offset {offsetMm} mm");
        Log(LogLevel.Info, $"Floor type: {floorType.Name}");
        Log(LogLevel.Info, $"Target level: {_targetLevel.Name}");

        return Run(hostDoc, view, selectedLink, selectedFamilyName, radiusMm);
    }

    protected override HashSet<string> LoadExistingSourceKeys(Document hostDoc, ViewPlan view)
        => CircleIdentityStorage.LoadExistingFloorSourceKeys(hostDoc);

    protected override void ExecuteCreation(
        Document hostDoc, ViewPlan view, IReadOnlyList<CreationItem> items, OperationSummary summary)
    {
        using var tx = new Transaction(hostDoc, "Create Floors at Linked Mechanical Equipment");
        tx.Start();

        int progress = 0;
        foreach (var item in items)
        {
            progress++;
            if (progress % 10 == 0) Log(LogLevel.Info, $"Progress: {progress}/{items.Count} floors created");

            try
            {
                Floor floor = CreateCircularFloor(hostDoc, item.Center);
                if (floor != null)
                {
                    CircleIdentityStorage.AttachSourceKey(floor, item.SourceKey, CircleIdentityStorage.LinkedElementType.Floor);
                    summary.FloorsCreated++;
                    Log(LogLevel.Success, $"Created floor at element {item.Element.Id}");
                }
                else
                {
                    summary.SkippedElements++;
                    Log(LogLevel.Warning, $"Failed to create floor at element {item.Element.Id}");
                }
            }
            catch (Exception ex)
            {
                summary.SkippedElements++;
                Log(LogLevel.Warning, $"Error creating floor at element {item.Element.Id}: {ex.Message}");
            }
        }

        tx.Commit();
        Log(LogLevel.Success, $"Floor creation transaction committed. Floors created: {summary.FloorsCreated}");
    }

    private Floor CreateCircularFloor(Document doc, XYZ center)
    {
        var loop = new CurveLoop();
        loop.Append(Arc.Create(center, _radiusFeet, 0, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY));

        if (loop.IsOpen() || !loop.HasPlane())
            throw new InvalidOperationException("Curve loop is not valid for floor creation");

        return Floor.Create(
            doc,
            new List<CurveLoop> { loop },
            _floorType.Id,
            _targetLevel.Id,
            isStructural: false,
            slopeArrow: null,
            _offsetFeet);
    }
}
