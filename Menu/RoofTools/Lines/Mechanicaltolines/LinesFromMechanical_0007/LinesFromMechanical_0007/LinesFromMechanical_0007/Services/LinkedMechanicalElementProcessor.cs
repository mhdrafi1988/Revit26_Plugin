using Autodesk.Revit.DB;
using Revit26_Plugin.LinesFromMechanical.V007.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.LinesFromMechanical.V007.Services;

/// <summary>
/// Shared pipeline for both detail-line and floor creation.
///
/// Handles the identical pre-transaction work that V003 duplicated across the
/// two services: collect visible linked mechanical equipment, filter by family,
/// validate LocationPoint, deduplicate by source key and by rounded center, and
/// skip elements that already have geometry. Subclasses supply only the
/// mode-specific bits: the existence check and the actual creation.
///
/// Revit-safety: all transaction work stays synchronous on the API thread.
/// Subclasses open their own transactions inside <see cref="ExecuteCreation"/>.
/// </summary>
public abstract class LinkedMechanicalElementProcessor
{
    public delegate void TypedLogHandler(LogLevel level, string message);
    public event TypedLogHandler? OnLog;

    protected void Log(LogLevel level, string message) => OnLog?.Invoke(level, message);

    protected readonly struct CreationItem(Element element, XYZ center, string sourceKey)
    {
        public Element Element  { get; } = element;
        public XYZ     Center   { get; } = center;
        public string  SourceKey { get; } = sourceKey;
    }

    // ── Abstract hooks ─────────────────────────────────────────────────────────

    /// <summary>True if geometry already exists in the document for this source key.</summary>
    protected abstract bool ExistsForSource(Document hostDoc, ViewPlan view, string sourceKey);

    /// <summary>Mode-specific creation. Opens its own transaction(s). Updates summary.</summary>
    protected abstract void ExecuteCreation(
        Document hostDoc,
        ViewPlan view,
        IReadOnlyList<CreationItem> items,
        OperationSummary summary);

    // ── Shared template method ─────────────────────────────────────────────────

    protected OperationSummary Run(
        Document hostDoc,
        ViewPlan view,
        RevitLinkInstance selectedLink,
        string selectedFamilyName,
        double radiusMm)
    {
        if (radiusMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMm), "Radius must be greater than zero.");
        if (radiusMm > 10000)
            Log(LogLevel.Warning, $"Radius of {radiusMm}mm is very large (>10m); may affect performance.");

        var summary = new OperationSummary();

        if (selectedLink.GetLinkDocument() == null)
        {
            Log(LogLevel.Error, "Selected link is unloaded or inaccessible.");
            summary.UnloadedLinksSkipped++;
            return summary;
        }

        summary.LinkedModelsProcessed++;
        Log(LogLevel.Info, $"Processing link: {selectedLink.Name}");

        var items = BuildCreationList(hostDoc, view, selectedLink, selectedFamilyName, summary);
        if (items.Count == 0)
        {
            Log(LogLevel.Info, "No new elements to create.");
            return summary;
        }

        Log(LogLevel.Info, $"Ready to create {items.Count} elements");
        ExecuteCreation(hostDoc, view, items, summary);
        return summary;
    }

    private List<CreationItem> BuildCreationList(
        Document hostDoc,
        ViewPlan view,
        RevitLinkInstance selectedLink,
        string selectedFamilyName,
        OperationSummary summary)
    {
        var processedSourceKeys     = new HashSet<string>();
        var processedRoundedCenters = new HashSet<string>();
        var result = new List<CreationItem>();

        Transform linkTransform = selectedLink.GetTotalTransform();

        IList<Element> visible = CollectVisibleLinkedMechanicalEquipment(hostDoc, view, selectedLink);
        summary.MechanicalEquipmentFound = visible.Count;
        Log(LogLevel.Info, $"Found {visible.Count} Mechanical Equipment elements in link");

        var filtered = visible.Where(e => GetFamilyName(e) == selectedFamilyName).ToList();
        Log(LogLevel.Info, $"Filtered to family '{selectedFamilyName}': {filtered.Count} elements");

        foreach (Element element in filtered)
        {
            if (!IsMechanicalEquipment(element)) { summary.SkippedElements++; continue; }

            if (element.Location is not LocationPoint lp)
            {
                Log(LogLevel.Warning, $"Skipping element {element.Id} (no LocationPoint)");
                summary.SkippedElements++;
                continue;
            }

            summary.ValidPointBasedFamilies++;

            string sourceKey = CircleIdentityStorage.BuildSourceKey(selectedLink, element);

            if (!processedSourceKeys.Add(sourceKey)) { summary.DuplicateElementsSkipped++; continue; }

            if (ExistsForSource(hostDoc, view, sourceKey))
            {
                Log(LogLevel.Warning, $"Skipping element {element.Id} (already exists)");
                summary.ExistingElementsSkipped++;
                continue;
            }

            XYZ hostPoint = linkTransform.OfPoint(lp.Point);
            XYZ center    = ProjectPointToViewPlane(hostPoint, view);

            if (!processedRoundedCenters.Add(BuildRoundedPointKey(center)))
            {
                Log(LogLevel.Warning, $"Skipping element {element.Id} (duplicate center point)");
                summary.DuplicateElementsSkipped++;
                continue;
            }

            result.Add(new CreationItem(element, center, sourceKey));
        }

        return result;
    }

    // ── Preview (non-API; safe to call off-transaction) ─────────────────────────

    public int GetPreviewCount(
        Document hostDoc, ViewPlan view, RevitLinkInstance selectedLink, string selectedFamilyName)
    {
        if (selectedLink == null || string.IsNullOrEmpty(selectedFamilyName)) return 0;
        if (selectedLink.GetLinkDocument() == null) return 0;

        return CollectVisibleLinkedMechanicalEquipment(hostDoc, view, selectedLink)
            .Where(e => GetFamilyName(e) == selectedFamilyName)
            .Where(e => e.Location is LocationPoint)
            .Count(e => !ExistsForSource(hostDoc, view,
                        CircleIdentityStorage.BuildSourceKey(selectedLink, e)));
    }

    public List<Element> GetPreviewElements(
        Document hostDoc, ViewPlan view, RevitLinkInstance selectedLink, string selectedFamilyName)
    {
        if (selectedLink == null || string.IsNullOrEmpty(selectedFamilyName)) return [];
        if (selectedLink.GetLinkDocument() == null) return [];

        return CollectVisibleLinkedMechanicalEquipment(hostDoc, view, selectedLink)
            .Where(e => GetFamilyName(e) == selectedFamilyName)
            .Where(e => e.Location is LocationPoint)
            .ToList();
    }

    // ── Shared static helpers ───────────────────────────────────────────────────

    /// <summary>All mechanical equipment are FamilyInstance — direct cast, no reflection.</summary>
    protected static string GetFamilyName(Element element)
        => element is FamilyInstance fi ? fi.Symbol?.Family?.Name ?? string.Empty : string.Empty;

    protected static IList<Element> CollectVisibleLinkedMechanicalEquipment(
        Document hostDoc, View hostView, RevitLinkInstance linkInstance)
        => new FilteredElementCollector(hostDoc, hostView.Id, linkInstance.Id)
            .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
            .WhereElementIsNotElementType()
            .ToElements();

    protected static bool IsMechanicalEquipment(Element element)
        => element.Category?.Id.Value == (long)BuiltInCategory.OST_MechanicalEquipment;

    protected static XYZ ProjectPointToViewPlane(XYZ point, ViewPlan view)
    {
        Plane plane = Plane.CreateByNormalAndOrigin(view.ViewDirection, view.Origin);
        double signed = plane.Normal.DotProduct(point - plane.Origin);
        return point - signed * plane.Normal;
    }

    protected static string BuildRoundedPointKey(XYZ point)
    {
        const double tol = UnitHelper.RoundingToleranceFt;
        long x = (long)Math.Round(point.X / tol);
        long y = (long)Math.Round(point.Y / tol);
        long z = (long)Math.Round(point.Z / tol);
        return $"{x}|{y}|{z}";
    }
}
