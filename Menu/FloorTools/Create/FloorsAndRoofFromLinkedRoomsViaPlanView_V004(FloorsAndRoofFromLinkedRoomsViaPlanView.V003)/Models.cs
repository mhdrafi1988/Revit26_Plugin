using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004
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

    /// <summary>A room found in the link, at the active view's level, shown as a checkbox row.</summary>
    public partial class RoomCandidate : ObservableObject
    {
        public ElementId RoomId { get; set; }
        public Room RoomElement { get; set; }
        public Transform Transform { get; set; }
        public string DisplayName { get; set; }
        public double AreaDisplay { get; set; }
        public string AreaUnitSymbol { get; set; }

        [ObservableProperty]
        private bool isSelected = false; // default unchecked — user opts in per room
    }

    /// <summary>Per-run tallies shown on the summary cards.</summary>
    public class RunSummary
    {
        public int SuccessCount { get; set; }
        public int TrimmedFixedCount { get; set; }
        public int FailedCount { get; set; }
        public int InnerLoopsSkippedCount { get; set; }
    }

    /// <summary>Simple mutable flag shared between the ViewModel (Cancel button) and the
    /// ExternalEvent handler (checked between rooms) — a class so it passes by reference.</summary>
    public class CancelFlag
    {
        public bool IsCancelled { get; set; }
    }

    public enum CreationMode { Floor, Roof }

    /// <summary>Everything the ExternalEvent handler needs to run one pass — either
    /// creating floors or creating roofs from the same selected room boundaries.</summary>
    public class CreateRunRequest
    {
        public CreationMode Mode { get; set; }
        public List<RoomCandidate> Rooms { get; set; }
        public Transform LinkTransform { get; set; }
        public ElementId TypeId { get; set; } // FloorType.Id or RoofType.Id depending on Mode
        public Level TargetLevel { get; set; }
        public CancelFlag Cancel { get; set; }
    }
}
