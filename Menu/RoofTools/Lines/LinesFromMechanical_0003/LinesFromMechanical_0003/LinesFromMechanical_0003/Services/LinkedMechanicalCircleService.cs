using Autodesk.Revit.DB;
using Revit26_Plugin.LinesFromMechanical.V003.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.LinesFromMechanical.V003.Services;

public sealed class LinkedMechanicalCircleService
{
    public delegate void LogMessageHandler(string message);
    public event LogMessageHandler? OnLogMessage;

    private Dictionary<string, List<Element>> _cachedEquipment = new Dictionary<string, List<Element>>();

    private void Log(string message)
    {
        OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public OperationSummary CreateDetailLines(
        Document hostDoc,
        ViewPlan activePlanView,
        RevitLinkInstance selectedLink,
        string selectedFamilyName,
        double radiusMm,
        Color circleColor)
    {
        if (radiusMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMm), "Circle radius must be greater than zero.");

        if (radiusMm > 10000)
            Log($"Warning: Radius of {radiusMm}mm is very large (>10m). This may cause performance issues.");

        double radiusFeet = UnitHelper.MillimetersToFeet(radiusMm);
        var summary = new OperationSummary();
        var processedSourceKeys = new HashSet<string>();
        var processedRoundedCenters = new HashSet<string>();

        Log($"Starting detail line creation with radius: {radiusMm} mm");
        Log($"Selected color: R={circleColor.Red}, G={circleColor.Green}, B={circleColor.Blue}");

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

            // Read-only duplicate check
            if (CircleIdentityStorage.DetailCurveExistsForSource(hostDoc, activePlanView, sourceKey))
            {
                Log($"Skipping element {linkedElement.Id} (circle already exists)");
                summary.ExistingElementsSkipped++;
                continue;
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
            Log("No new detail lines to create.");
            return summary;
        }

        Log($"Ready to create {elementsToCreate.Count} detail circles");

        // ── Transaction 1: Create detail curves and attach identity data ────────
        var createdCurves = new List<(DetailCurve curve, Color color)>();

        try
        {
            using var createTx = new Transaction(hostDoc, "Create Detail Circles at Linked Mechanical Equipment");
            createTx.Start();

            int progress = 0;
            foreach (var (linkedElement, circleCenter, sourceKey) in elementsToCreate)
            {
                progress++;
                if (progress % 10 == 0)
                    Log($"Progress: {progress}/{elementsToCreate.Count} circles created");

                List<DetailCurve> curves = CreateDetailCurves(hostDoc, activePlanView, circleCenter, radiusFeet, sourceKey);

                if (curves.Count > 0)
                {
                    foreach (var curve in curves)
                        createdCurves.Add((curve, circleColor));

                    summary.DetailLinesCreated++;
                    Log($"Created detail lines at element {linkedElement.Id}");
                }
                else
                {
                    summary.SkippedElements++;
                }
            }

            createTx.Commit();
            Log($"Creation transaction committed. Detail lines created: {summary.DetailLinesCreated}");
        }
        catch (Exception ex)
        {
            Log($"ERROR during creation transaction: {ex.Message}");
            throw;
        }

        // ── Transaction 2: Apply color overrides ────────────────────────────────
        if (createdCurves.Count > 0)
        {
            try
            {
                using var overrideTx = new Transaction(hostDoc, "Apply Color Overrides to Detail Circles");
                overrideTx.Start();

                foreach (var (curve, color) in createdCurves)
                {
                    if (curve != null && !curve.IsTransient)
                    {
                        var overrideSettings = new OverrideGraphicSettings();
                        overrideSettings.SetProjectionLineColor(color);
                        activePlanView.SetElementOverrides(curve.Id, overrideSettings);
                    }
                }

                overrideTx.Commit();
                Log("Override transaction committed.");
            }
            catch (Exception ex)
            {
                Log($"WARNING: Could not apply color overrides: {ex.Message}");
            }
        }

        // Clear cache
        _cachedEquipment.Clear();

        return summary;
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

    public int GetPreviewCount(Document hostDoc, ViewPlan activePlanView, RevitLinkInstance selectedLink, string selectedFamilyName)
    {
        if (selectedLink == null || string.IsNullOrEmpty(selectedFamilyName))
            return 0;

        Document? linkDoc = selectedLink.GetLinkDocument();
        if (linkDoc == null)
            return 0;

        IList<Element> visibleLinkedMechanicalEquipment = CollectVisibleLinkedMechanicalEquipment(
            hostDoc, activePlanView, selectedLink);

        var filteredElements = visibleLinkedMechanicalEquipment
            .Where(e => GetFamilyName(e) == selectedFamilyName)
            .Where(e => e.Location is LocationPoint)
            .ToList();

        // Count only those that don't already have circles
        int count = 0;
        foreach (var element in filteredElements)
        {
            string sourceKey = CircleIdentityStorage.BuildSourceKey(selectedLink, element);
            if (!CircleIdentityStorage.DetailCurveExistsForSource(hostDoc, activePlanView, sourceKey))
            {
                count++;
            }
        }

        return count;
    }

    public List<Element> GetPreviewElements(Document hostDoc, ViewPlan activePlanView, RevitLinkInstance selectedLink, string selectedFamilyName)
    {
        var result = new List<Element>();

        if (selectedLink == null || string.IsNullOrEmpty(selectedFamilyName))
            return result;

        Document? linkDoc = selectedLink.GetLinkDocument();
        if (linkDoc == null)
            return result;

        IList<Element> visibleLinkedMechanicalEquipment = CollectVisibleLinkedMechanicalEquipment(
            hostDoc, activePlanView, selectedLink);

        return visibleLinkedMechanicalEquipment
            .Where(e => GetFamilyName(e) == selectedFamilyName)
            .Where(e => e.Location is LocationPoint)
            .ToList();
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

    private static List<DetailCurve> CreateDetailCurves(
        Document doc,
        View view,
        XYZ center,
        double radius,
        string sourceKey)
    {
        XYZ xDirection = view.RightDirection.Normalize();
        XYZ yDirection = view.UpDirection.Normalize();

        double q0 = 0.0;
        double q1 = Math.PI / 2.0;
        double q2 = Math.PI;
        double q3 = Math.PI * 1.5;
        double q4 = Math.PI * 2.0;

        Arc[] arcs =
        [
            Arc.Create(center, radius, q0, q1, xDirection, yDirection),
            Arc.Create(center, radius, q1, q2, xDirection, yDirection),
            Arc.Create(center, radius, q2, q3, xDirection, yDirection),
            Arc.Create(center, radius, q3, q4, xDirection, yDirection)
        ];

        var created = new List<DetailCurve>();

        foreach (Arc arc in arcs)
        {
            DetailCurve detailCurve = doc.Create.NewDetailCurve(view, arc);
            CircleIdentityStorage.AttachSourceKey(detailCurve, sourceKey, CircleIdentityStorage.LinkedElementType.DetailLine);
            created.Add(detailCurve);
        }

        return created;
    }

    private static string BuildRoundedPointKey(XYZ point)
    {
        const double tolerance = 0001; // Increased precision from 0.00V001 to 0.0V001 (1mm)

        long x = (long)Math.Round(point.X / tolerance);
        long y = (long)Math.Round(point.Y / tolerance);
        long z = (long)Math.Round(point.Z / tolerance);

        return $"{x}|{y}|{z}";
    }
}