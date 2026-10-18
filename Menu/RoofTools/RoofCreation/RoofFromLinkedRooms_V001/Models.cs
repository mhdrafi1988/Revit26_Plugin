using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RoofFromLinkedRooms.V001
{
    /// <summary>A linked RVT document that contains at least one Room element.</summary>
    public class LinkedDocumentOption
    {
        /// <summary>Link document title shown in the dropdown.</summary>
        public string DisplayName { get; set; }

        /// <summary>The linked document.</summary>
        public Document LinkDocument { get; set; }

        /// <summary>All placed instances of this same link document in the host model.</summary>
        public List<LinkInstanceOption> Instances { get; set; } = new();
    }

    /// <summary>One placed instance (transform) of a linked document.</summary>
    public class LinkInstanceOption
    {
        /// <summary>Instance label shown in the dropdown.</summary>
        public string DisplayName { get; set; }

        /// <summary>Host element id of the RevitLinkInstance.</summary>
        public ElementId InstanceId { get; set; }

        /// <summary>Link-to-host transform.</summary>
        public Transform Transform { get; set; }
    }

    /// <summary>A selectable host-model Level.</summary>
    public class HostLevelOption
    {
        /// <summary>Level name.</summary>
        public string Name { get; set; }

        /// <summary>The host Level element.</summary>
        public Level LevelElement { get; set; }

        /// <summary>The level's element id.</summary>
        public ElementId Id => LevelElement.Id;
    }

    /// <summary>
    /// A room found in the link, shown as one grid row. The host level is auto-matched by
    /// elevation; the status pill reflects the pre-run state, then the run outcome.
    /// </summary>
    public partial class RoomCandidate : ObservableObject
    {
        public int SerialNumber { get; set; }
        public ElementId RoomId { get; set; }
        public Room RoomElement { get; set; }
        public string DisplayName { get; set; }
        public string RoomName { get; set; }
        public string RoomNumber { get; set; }
        public double AreaDisplay { get; set; }
        public string AreaUnitSymbol { get; set; }

        /// <summary>Level name as read from the room's own Level in the linked document.</summary>
        public string LinkedLevelName { get; set; }

        /// <summary>Elevation of the room's Level in the link's own coordinates (feet); null when the room has no Level.</summary>
        public double? LinkedLevelElevation { get; set; }

        /// <summary>Row checkbox — default unchecked, the user opts in per room.</summary>
        [ObservableProperty] private bool isSelected;

        /// <summary>Auto-matched host level; null means unmapped (room is skipped).</summary>
        [ObservableProperty] private HostLevelOption selectedHostLevel;

        /// <summary>Pill kind: Ready | Unmapped | Created | Failed.</summary>
        [ObservableProperty] private string statusKind = "Ready";

        /// <summary>Pill text / result detail.</summary>
        [ObservableProperty] private string statusLabel = "Ready";

        /// <summary>Number of opening loops cut into the roof (known after a run).</summary>
        [ObservableProperty] private string openingsText = "—";

        /// <summary>Host level name for the grid ("(none)" when unmapped).</summary>
        public string HostLevelName => SelectedHostLevel?.Name ?? "(none)";

        /// <summary>True when a host level was matched.</summary>
        public bool IsMapped => SelectedHostLevel != null;

        partial void OnSelectedHostLevelChanged(HostLevelOption value)
        {
            OnPropertyChanged(nameof(IsMapped));
            OnPropertyChanged(nameof(HostLevelName));
            ResetStatus();
        }

        /// <summary>Resets the pill to the pre-run state for the current mapping.</summary>
        public void ResetStatus()
        {
            StatusKind = IsMapped ? "Ready" : "Unmapped";
            StatusLabel = IsMapped ? "Ready" : "Unmapped";
            OpeningsText = "—";
        }
    }

    /// <summary>Per-run tallies shown on the summary line and logged.</summary>
    public class RunSummary
    {
        public int RoofsCreatedCount { get; set; }
        public int TrimmedFixedCount { get; set; }
        public int OpeningsCutCount { get; set; }
        public int FailedCount { get; set; }
        public int UnmappedSkippedCount { get; set; }
    }

    /// <summary>Everything the ExternalEvent handler needs to run one pass.</summary>
    public class CreateRoofsRunRequest
    {
        public List<RoomCandidate> Rooms { get; set; }
        public Transform LinkTransform { get; set; }
        public ElementId RoofTypeId { get; set; }
    }

    /// <summary>Per-tool persisted settings, stored at
    /// %AppData%\Revit26_Plugin\RoofFromLinkedRooms\settings.json.</summary>
    public class ToolSettings
    {
        /// <summary>Settings format version (bump when fields change; old files still load).</summary>
        public int Version { get; set; } = 1;
        public string LastLinkName { get; set; }
        public string LastRoofTypeName { get; set; }

        /// <summary>Elevation match tolerance in millimetres.</summary>
        public double ToleranceMm { get; set; } = 1.0;
    }
}
