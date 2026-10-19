using System;
using System.Collections.Generic;

namespace Revit26_Plugin_Revised.DwgStatistics.V005.Core.Models
{
    // Plain facts read from the Revit model by Infrastructure/Revit/DwgStatsScanner. No Revit API types
    // here (element IDs are stored as long), so Core/Services can aggregate them without Revit.

    /// <summary>One CAD type (one DWG/DXF/DGN file), linked or imported, with or without instances.</summary>
    public sealed class CadTypeFact
    {
        /// <summary>Element ID of the CADLinkType.</summary>
        public long TypeId { get; init; }

        /// <summary>File name as Revit shows it (with its real extension).</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>True for a CAD link (external file reference), false for an import.</summary>
        public bool IsLinked { get; init; }

        /// <summary>Full path of a linked file, or empty for imports and unresolved links.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>Revit's link status (Loaded, NotFound, Unloaded…), or "Imported".</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>True when the link is loaded (always true for imports).</summary>
        public bool IsLoaded { get; init; }

        /// <summary>Absolute / Relative / Server…, or empty for imports.</summary>
        public string PathType { get; init; } = string.Empty;

        /// <summary>Size of the file on disk, when the path exists.</summary>
        public long? FileSizeBytes { get; init; }

        /// <summary>Last write time of the file on disk, when the path exists.</summary>
        public DateTime? FileModified { get; init; }
    }

    /// <summary>One placed CAD instance (ImportInstance).</summary>
    public sealed class CadInstanceFact
    {
        /// <summary>Element ID of the instance.</summary>
        public long Id { get; init; }

        /// <summary>Element ID of its CAD type.</summary>
        public long TypeId { get; init; }

        /// <summary>True when placed "Current view only" (owned by one view).</summary>
        public bool IsViewSpecific { get; init; }

        /// <summary>Owner view ID for view-specific instances, 0 for model instances.</summary>
        public long OwnerViewId { get; init; }

        /// <summary>True when the instance is pinned.</summary>
        public bool Pinned { get; init; }

        /// <summary>Workset name (empty when the model is not workshared).</summary>
        public string Workset { get; init; } = string.Empty;

        /// <summary>Level name, or empty.</summary>
        public string Level { get; init; } = string.Empty;

        /// <summary>User who created the instance (workshared models only), or empty.</summary>
        public string CreatedBy { get; init; } = string.Empty;

        /// <summary>Number of CAD layers (sub-categories of the instance's import category).</summary>
        public int LayerCount { get; init; }

        /// <summary>V005: element ID of the instance's import category (one per CAD file; its sub-categories are the layers).</summary>
        public long CategoryId { get; init; }

        /// <summary>V005: distance of the insertion point from the internal origin, in feet.</summary>
        public double OriginDistanceFt { get; init; }

        /// <summary>V005: distance of the farthest point of the DWG geometry (bounding box) from the internal origin, in feet.</summary>
        public double ExtentDistanceFt { get; init; }

        /// <summary>V005: workset ID (integer value), or 0 when the model is not workshared.</summary>
        public int WorksetId { get; init; }

        /// <summary>V005: true when the workset can be changed (model-placed and not read-only).</summary>
        public bool CanChangeWorkset { get; init; }
    }

    /// <summary>V005: one DWG layer (a sub-category of a CAD file's import category).</summary>
    public sealed class CadLayerFact
    {
        /// <summary>Sub-category element ID.</summary>
        public long LayerId { get; init; }

        /// <summary>Layer name.</summary>
        public string Name { get; init; } = string.Empty;
    }

    /// <summary>V005: layer visibility of one CAD file in one view.</summary>
    public sealed class LayerViewState
    {
        /// <summary>Layers hidden in the view (Visibility/Graphics unticked).</summary>
        public HashSet<long> Hidden { get; } = new HashSet<long>();

        /// <summary>Layers with graphic overrides in the view (colour, weight, pattern, halftone…).</summary>
        public HashSet<long> Overridden { get; } = new HashSet<long>();
    }

    /// <summary>V005: a user workset of a workshared model.</summary>
    public sealed class WorksetFact
    {
        /// <summary>WorksetId.IntegerValue.</summary>
        public int Id { get; init; }

        /// <summary>Workset name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>One view that owns or draws at least one CAD instance.</summary>
    public sealed class ViewFact
    {
        /// <summary>Element ID of the view.</summary>
        public long Id { get; init; }

        /// <summary>View name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>View type label (Floor Plans, Sections, 3D Views…).</summary>
        public string TypeLabel { get; init; } = string.Empty;

        /// <summary>Numbers of the sheets the view is placed on (a sheet lists itself).</summary>
        public List<string> SheetNumbers { get; init; } = new List<string>();

        /// <summary>V004: element IDs of those sheets, same order as <see cref="SheetNumbers"/> (for "Open sheet").</summary>
        public List<long> SheetIds { get; init; } = new List<long>();
    }

    /// <summary>Everything one scan read from the model.</summary>
    public sealed class DwgScanResult
    {
        /// <summary>Every CAD type in the project, including types with no instance.</summary>
        public List<CadTypeFact> Types { get; } = new List<CadTypeFact>();

        /// <summary>Every CAD instance in the project.</summary>
        public List<CadInstanceFact> Instances { get; } = new List<CadInstanceFact>();

        /// <summary>Views that own or draw CAD, by view ID.</summary>
        public Dictionary<long, ViewFact> Views { get; } = new Dictionary<long, ViewFact>();

        /// <summary>For each instance ID, the IDs of the views that draw it (from the deep scan).</summary>
        public Dictionary<long, HashSet<long>> VisibleIn { get; } = new Dictionary<long, HashSet<long>>();

        /// <summary>
        /// V004: for each model instance ID, the views where it is hidden with "Hide in View → Elements"
        /// (so Unhide knows where to act).
        /// </summary>
        public Dictionary<long, HashSet<long>> HiddenIn { get; } = new Dictionary<long, HashSet<long>>();

        /// <summary>V004: stable key of the project file for the last-scan snapshot (central path or title).</summary>
        public string ProjectKey { get; set; } = string.Empty;

        /// <summary>
        /// V005: folder of the project file (central model folder for workshared models) where the shared
        /// scan history is kept; empty for cloud / server / unsaved models (history then stays on this computer).
        /// </summary>
        public string ProjectFolder { get; set; } = string.Empty;

        /// <summary>V005: layers of each CAD import category, by category ID.</summary>
        public Dictionary<long, List<CadLayerFact>> LayersByCategory { get; } = new Dictionary<long, List<CadLayerFact>>();

        /// <summary>V005: per view ID, per CAD category ID, which layers are hidden / overridden.</summary>
        public Dictionary<long, Dictionary<long, LayerViewState>> LayerStates { get; } = new Dictionary<long, Dictionary<long, LayerViewState>>();

        /// <summary>V005: true for a workshared model.</summary>
        public bool IsWorkshared { get; set; }

        /// <summary>V005: user worksets (for the workset rule and Move to Workset).</summary>
        public List<WorksetFact> Worksets { get; } = new List<WorksetFact>();

        /// <summary>Views checked by the deep scan.</summary>
        public int ViewsScanned { get; set; }

        /// <summary>Views the deep scan could not read (skipped).</summary>
        public int ViewsSkipped { get; set; }

        /// <summary>How long the scan took.</summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>Project title, used for the export file name.</summary>
        public string ProjectTitle { get; set; } = string.Empty;
    }
}
