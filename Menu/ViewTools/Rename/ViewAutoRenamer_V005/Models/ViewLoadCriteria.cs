using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin_Revised.ViewAutoRenamer.V005.Models;

/// <summary>
/// What a single Load pass should pull out of the model.
///
/// The tool no longer loads every in-scope view when the window opens —
/// the user ticks types in the Filters popover and clicks Load, and only
/// matching views are read. These three axes are LOAD-time criteria;
/// everything else in the UI (name search, the Sheet combo, the quick
/// filter chips) still filters the already-loaded rows client-side.
/// </summary>
public sealed class ViewLoadCriteria
{
    /// <summary>Checked view types. Empty means nothing to load.</summary>
    public HashSet<ViewType> ViewTypes { get; init; } = new();

    public bool IncludePlaced    { get; init; } = true;
    public bool IncludeNotPlaced { get; init; } = true;

    /// <summary>
    /// Sheet-number substring. When set, only views placed on a matching
    /// sheet are loaded — which necessarily excludes unplaced views,
    /// matching how the old client-side filter behaved.
    /// </summary>
    public string SheetNumberContains { get; init; } = "";

    public bool HasAnyType => ViewTypes.Count > 0;
}
