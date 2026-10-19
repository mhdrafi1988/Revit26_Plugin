// IslandRoofSplitter V001 — persisted options (JSON). Roof type / level / offset / Mark prefix are
// deliberately NOT persisted: they default from the source roof on every launch.
#nullable enable
namespace Revit26_Plugin_Revised.IslandRoofSplitter.V001.Core.Models
{
    public sealed class IslandSplitterSettings
    {
        public SplitMode Mode { get; set; } = SplitMode.Sloped;
        public NestingMode Nesting { get; set; } = NestingMode.Ring;
        public CrossingMode Crossings { get; set; } = CrossingMode.Split;
        public ArcOutput Arcs { get; set; } = ArcOutput.TrueArcs;
        public InteriorPoints Interior { get; set; } = InteriorPoints.RidgeValleyPoints;

        /// <summary>Chord (sagitta) tolerance for arc tessellation. Per Anas (2026-10-14): default 5 mm, range 1–50.</summary>
        public double ChordToleranceMm { get; set; } = 5.0;

        public const double MinChordToleranceMm = 1.0;
        public const double MaxChordToleranceMm = 50.0;

        /// <summary>Endpoint gap healing. Fixed by spec at 1 mm (not user-editable).</summary>
        public const double HealToleranceMm = 1.0;

        public IslandSplitterSettings Clamp()
        {
            if (double.IsNaN(ChordToleranceMm)) ChordToleranceMm = 5.0;
            if (ChordToleranceMm < MinChordToleranceMm) ChordToleranceMm = MinChordToleranceMm;
            if (ChordToleranceMm > MaxChordToleranceMm) ChordToleranceMm = MaxChordToleranceMm;
            return this;
        }

        public IslandSplitterSettings Clone() => (IslandSplitterSettings)MemberwiseClone();
    }
}
