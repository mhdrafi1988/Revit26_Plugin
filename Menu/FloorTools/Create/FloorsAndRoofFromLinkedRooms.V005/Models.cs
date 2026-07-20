using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005
{
    /// <summary>A linked RVT document that contains at least one Room element.</summary>
    public class LinkedDocumentOption
    {
        public string DisplayName { get; set; }
        public Document LinkDocument { get; set; }

        /// <summary>All placed instances of this same link document in the host model.</summary>
        public List<LinkInstanceOption> Instances { get; set; } = new();
    }

    /// <summary>One placed instance (transform) of a linked document.</summary>
    public class LinkInstanceOption
    {
        public string DisplayName { get; set; }
        public ElementId InstanceId { get; set; }
        public Transform Transform { get; set; }
    }

    /// <summary>A selectable host-model Level, used as the source list for the
    /// "New Level" live-filter dropdown on each grid row.</summary>
    public class HostLevelOption
    {
        public string Name { get; set; }
        public Level LevelElement { get; set; }
        public ElementId Id => LevelElement.Id;
    }

    /// <summary>
    /// A room found anywhere in the link. Each row carries its own host-level mapping so
    /// a single run can create floors/roofs targeting different host levels per room.
    /// V005: the per-room Transform property was removed — it was dead data; the run
    /// uses CreateRunRequest.LinkTransform (the selected instance's transform) only.
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

        /// <summary>Level name as read from the room's own Level property in the linked
        /// document (Room.Level.Name).</summary>
        public string LinkedLevelName { get; set; }

        [ObservableProperty]
        private bool isSelected = false; // default unchecked — user opts in per room

        /// <summary>Host level this room's floor/roof will be created on. Null means
        /// "unmapped" — seeded from a case-insensitive/trimmed name match against
        /// LinkedLevelName; left null when no match is found, per confirmed spec.
        /// The grid's live-filter ComboBox binds here TwoWay.</summary>
        [ObservableProperty]
        private HostLevelOption selectedHostLevel;

        public bool IsMapped => SelectedHostLevel != null;

        partial void OnSelectedHostLevelChanged(HostLevelOption value) => OnPropertyChanged(nameof(IsMapped));
    }

    /// <summary>Per-run tallies shown on the summary line and logged.</summary>
    public class RunSummary
    {
        public int SuccessCount { get; set; }
        public int TrimmedFixedCount { get; set; }
        public int FailedCount { get; set; }
        public int InnerLoopsSkippedCount { get; set; }
        public int UnmappedSkippedCount { get; set; }

        /// <summary>V005: rooms never reached because the user pressed Cancel mid-run.
        /// Already-created elements are kept (transaction still commits), per confirmed spec.</summary>
        public int NotProcessedCount { get; set; }
    }

    /// <summary>Simple mutable flag shared between the ViewModel (Cancel button) and the
    /// ExternalEvent handler (checked between rooms) — a class so it passes by reference.</summary>
    public class CancelFlag
    {
        public bool IsCancelled { get; set; }
    }

    public enum CreationMode { Floor, Roof }

    /// <summary>Everything the ExternalEvent handler needs to run one pass — either
    /// creating floors or creating roofs from the same selected room boundaries.
    /// Each RoomCandidate carries its own SelectedHostLevel, since rooms can map to
    /// different host levels in one run.</summary>
    public class CreateRunRequest
    {
        public CreationMode Mode { get; set; }
        public List<RoomCandidate> Rooms { get; set; }
        public Transform LinkTransform { get; set; }
        public ElementId TypeId { get; set; } // FloorType.Id or RoofType.Id depending on Mode
        public CancelFlag Cancel { get; set; }
    }
}
