// IslandRoofSplitter V001 — option enums. Member names are matched by EnumToBoolConverter (ToString), keep them stable.
#nullable enable
namespace Revit26_Plugin.IslandRoofSplitter.V001.Core.Models
{
    public enum SplitMode { Flat, Sloped }

    /// <summary>Per Anas's decision (2026-10-14): both offered in the UI.</summary>
    public enum NestingMode
    {
        /// <summary>Outer region gets an opening where an inner island sits (no overlap).</summary>
        Ring,
        /// <summary>Outer region is the full loop; inner island overlaps it.</summary>
        Full
    }

    /// <summary>Per Anas's decision (2026-10-14): both offered in the UI.</summary>
    public enum CrossingMode
    {
        /// <summary>Lines crossing mid-span are split at the crossing (T-junctions too).</summary>
        Split,
        /// <summary>Only lines joined end-to-end form loops; crossings make a loop self-intersecting.</summary>
        ChainsOnly
    }

    /// <summary>Flat mode only. Sloped mode always tessellates (every vertex needs a Z).</summary>
    public enum ArcOutput { TrueArcs, Tessellate }

    /// <summary>Sloped mode only.</summary>
    public enum InteriorPoints
    {
        BoundaryOnly,
        /// <summary>Also add shape points where the parent's ridges/valleys cross or sit inside the island.</summary>
        RidgeValleyPoints
    }

    public enum IslandStatus { Ready, Skipped, Created, Failed }

    /// <summary>Quick-filter chips over the results grid.</summary>
    public enum IslandFilter { All, Ready, Skipped, Created, Failed }

    public enum MessageSeverity { Debug, Info, Warning, Error, Success }
}
