// IslandRoofSplitter V001 — Revit-free snapshot of the picked roof + detail lines, built by the Command
// (and again by the handler on Re-pick). The ViewModel and the preview only ever see this.
#nullable enable
using System.Collections.Generic;

namespace Revit26_Plugin.IslandRoofSplitter.V001.Core.Models
{
    public sealed record NamedId(long Id, string Name)
    {
        public override string ToString() => Name;
    }

    public sealed class SplitterContext
    {
        public long RoofId { get; init; }
        public string RoofTitle { get; init; } = "";
        public string RoofDetail { get; init; } = "";
        public long RoofTypeId { get; init; }
        public long LevelId { get; init; }
        public double BaseOffsetFt { get; init; }
        public RoofSurfaceMesh Mesh { get; init; } = new();

        public List<SourceSegment> Segments { get; init; } = new();
        public string LinesTitle { get; init; } = "";
        public string LinesDetail { get; init; } = "";

        public List<NamedId> RoofTypes { get; init; } = new();
        public List<NamedId> Levels { get; init; } = new();

        /// <summary>Application.ShortCurveTolerance (ft).</summary>
        public double ShortCurveTolFt { get; init; } = 0.00256;
    }
}
