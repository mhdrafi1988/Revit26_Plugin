using Autodesk.Revit.DB;
using Revit26_Plugin.LinesFromMechanical.V003.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.LinesFromMechanical.V003.Services;

public sealed class LinkedMechanicalFloorService
{
    public delegate void LogMessageHandler(string message);
    public event LogMessageHandler? OnLogMessage;

    private void Log(string message)
    {
        OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public OperationSummary CreateFloors(
        Document hostDoc,
        ViewPlan activePlanView,
        RevitLinkInstance selectedLink,
        string selectedFamilyName,
        double radiusMm,
        FloorType floorType,
        double offsetMm)
    {
        if (radiusMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMm), "Circle radius must be greater than zero.");

        if (radiusMm > 10000)
            Log($"Warning: Radius of {radiusMm}mm is very large (>10m). This may cause performance issues.");

        double radiusFeet = UnitHelper.MillimetersToFeet(radiusMm);
        double offsetFeet = UnitHelper.MillimetersToFeet(offsetMm);

        var summary = new OperationSummary();
        var processedSourceKeys = new HashSet<string>();
        var processedRoundedCenters = new HashSet<string>();

        Log($"Starting floor creation with radius: {radiusMm} mm");

        // Validate floor type
        if (floorType == null)
        {
            Log("ERROR: No floor type selected.");
            return summary;
        }

        // FloorType does not have IsActive; remove this check
        // If you need to ensure the type is loaded/active, you may need to check other properties or logic

        Log($"Floor Type: {floorType.Name} (ID: {floorType.Id})");
        Log($"Offset from level: {offsetMm} mm");

        Document? linkDoc = selectedLink.GetLinkDocument();

        if (linkDoc == null)
        {
            Log("ERROR: Selected link is unloaded or inaccessible.");
            summary.UnloadedLinksSkipped++;
            return summary;
        }

        summary.LinkedModelsProcessed++;
        Log($"Processing link: {selectedLink.Name}");

        Transform linkTransform = selectedLink.GetTotalTransform();
        Level targetLevel = activePlanView.GenLevel;

        if (targetLevel == null)
        {
            Log("ERROR: Active view has no associated level.");
            return summary;
        }

        Log($"Target level: {targetLevel.Name} (Elevation: {UnitHelper.FeetToMillimeters(targetLevel.Elevation)} mm)");

        IList<Element> visibleLinkedMechanicalEquipment =
            CollectVisibleLinkedMechanicalEquipment(hostDoc, activePlanView, selectedLink);

        summary.MechanicalEquipmentFound = visibleLinkedMechanicalEquipment.Count;
        Log($"Found {summary.MechanicalEquipmentFound} Mechanical Equipment elements in link");

        // Filter by family name
        var filteredElements = visibleLinkedMechanicalEquipment
            .Where(e => GetFamilyName(e) == selectedFamilyName)
            .ToList();

        Log($"Filtered to family '{selectedFamilyName}': {filteredElements.Count} elements");

        // ── Pre-transaction pass ────────────────────────────────────────────────
        var elementsToCreate = new List<(Element element, XYZ center, string sourceKey)>();

        foreach (Element linkedElement in filteredElements)
        {
            if (!IsMechanicalEquipment(linkedElement))
            {
                summary.SkippedElements++;
                continue;
            }

            if (linkedElement.Location is not LocationPoint locationPoint)
            {
                Log($"Skipping element {linkedElement.Id} (no LocationPoint)");
                summary.SkippedElements++;
                continue;
            }

            summary.ValidPointBasedFamilies++;

            string sourceKey = CircleIdentityStorage.BuildSourceKey(selectedLink, linkedElement);

            if (!processedSourceKeys.Add(sourceKey))
            {
                summary.DuplicateElementsSkipped++;
                continue;
            }

            // Check for existing floor at this location
            if (CircleIdentityStorage.FloorExistsForSource(hostDoc, sourceKey))
            {
                Log($"Skipping element {linkedElement.Id} (floor already exists for this source)");
                summary.ExistingElementsSkipped++;
                continue;
            }

            // Also check if detail line exists to prevent overlapping (optional)
            if (CircleIdentityStorage.DetailCurveExistsForSource(hostDoc, activePlanView, sourceKey))
            {
                Log($"Warning: Element {linkedElement.Id} has detail lines - may overlap with floor");
            }

            XYZ hostPoint = linkTransform.OfPoint(locationPoint.Point);
            XYZ circleCenter = ProjectPointToViewPlane(hostPoint, activePlanView);

            string roundedCenterKey = BuildRoundedPointKey(circleCenter);

            if (!processedRoundedCenters.Add(roundedCenterKey))
            {
                Log($"Skipping element {linkedElement.Id} (duplicate center point)");
                summary.DuplicateElementsSkipped++;
                continue;
            }

            elementsToCreate.Add((linkedElement, circleCenter, sourceKey));
        }

        if (elementsToCreate.Count == 0)
        {
            Log("No new floors to create.");
            return summary;
        }

        Log($"Ready to create {elementsToCreate.Count} floors");

        // ── Single Transaction: Create all floors ───────────────────────────────
        try
        {
            using var createTx = new Transaction(hostDoc, "Create Floors at Linked Mechanical Equipment");
            createTx.Start();

            int progress = 0;
            foreach (var (linkedElement, circleCenter, sourceKey) in elementsToCreate)
            {
                progress++;
                if (progress % 10 == 0)
                    Log($"Progress: {progress}/{elementsToCreate.Count} floors created");

                try
                {
                    Floor floor = CreateCircularFloor(
                        hostDoc,
                        targetLevel,
                        circleCenter,
                        radiusFeet,
                        offsetFeet,
                        floorType);

                    if (floor != null)
                    {
                        // Attach source key to the floor for future duplicate detection
                        CircleIdentityStorage.AttachSourceKey(floor, sourceKey, CircleIdentityStorage.LinkedElementType.Floor);
                        summary.FloorsCreated++;
                        Log($"Created floor at element {linkedElement.Id}");
                    }
                    else
                    {
                        summary.SkippedElements++;
                        Log($"Failed to create floor at element {linkedElement.Id}");
                    }
                }
                catch (Exception ex)
                {
                    summary.SkippedElements++;
                    Log($"Error creating floor at element {linkedElement.Id}: {ex.Message}");
                }
            }

            createTx.Commit();
            Log($"Floor creation transaction committed. Floors created: {summary.FloorsCreated}");
        }
        catch (Exception ex)
        {
            Log($"ERROR during floor creation transaction: {ex.Message}");
            throw;
        }

        return summary;
    }

    private Floor CreateCircularFloor(
        Document doc,
        Level level,
        XYZ center,
        double radiusFeet,
        double offsetFeet,
        FloorType floorType)
    {
        try
        {
            // Create a CurveLoop with a full circle
            CurveLoop curveLoop = new CurveLoop();

            // Create coordinate system for the circle (using X and Y directions)
            XYZ xAxis = XYZ.BasisX;
            XYZ yAxis = XYZ.BasisY;

            // Create full circle arc (0 to 2π)
            Arc circleArc = Arc.Create(center, radiusFeet, 0, 2 * Math.PI, xAxis, yAxis);
            curveLoop.Append(circleArc);

            // Validate curve loop is closed and valid
            if (!curveLoop.IsOpen() && curveLoop.HasPlane())
            {
                // Create the floor
                bool isStructural = false;  // Architectural floor
                Line? slopeArrow = null;    // No slope arrow

                Floor floor = Floor.Create(
                    doc,
                    new List<CurveLoop> { curveLoop },
                    floorType.Id,
                    level.Id,
                    isStructural,
                    slopeArrow,
                    offsetFeet);

                return floor;
            }
            else
            {
                throw new InvalidOperationException("Curve loop is not valid for floor creation");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create circular floor: {ex.Message}", ex);
        }
    }

    private string GetFamilyName(Element element)
    {
        if (element is FamilyInstance instance && instance.Symbol?.Family != null)
            return instance.Symbol.Family.Name;

        var familyNameProp = element.GetType().GetProperty("FamilyName");
        if (familyNameProp != null)
            return familyNameProp.GetValue(element)?.ToString() ?? string.Empty;

        return string.Empty;
    }

    private static IList<Element> CollectVisibleLinkedMechanicalEquipment(
        Document hostDoc,
        View hostView,
        RevitLinkInstance linkInstance)
    {
        return new FilteredElementCollector(hostDoc, hostView.Id, linkInstance.Id)
            .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
            .WhereElementIsNotElementType()
            .ToElements();
    }

    private static bool IsMechanicalEquipment(Element element)
    {
        return element.Category != null
               && element.Category.Id.Value == (long)BuiltInCategory.OST_MechanicalEquipment;
    }

    private static XYZ ProjectPointToViewPlane(XYZ point, ViewPlan view)
    {
        Plane plane = Plane.CreateByNormalAndOrigin(view.ViewDirection, view.Origin);
        double signedDistance = plane.Normal.DotProduct(point - plane.Origin);
        return point - signedDistance * plane.Normal;
    }

    private static string BuildRoundedPointKey(XYZ point)
    {
        const double tolerance = 0.0001; // 1mm precision

        long x = (long)Math.Round(point.X / tolerance);
        long y = (long)Math.Round(point.Y / tolerance);
        long z = (long)Math.Round(point.Z / tolerance);

        return $"{x}|{y}|{z}";
    }
}