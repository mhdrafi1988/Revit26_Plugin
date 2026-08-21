using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models
{
    /// <summary>
    /// Section 5 settings: controls how far processing extends and how clipped
    /// Profile-group boundaries are finished off. Persisted to settings.json.
    ///
    /// Overlap/collinear cleanup (RemoveEngulfedOnly / MergePartialOverlaps /
    /// JoinCollinearLines) and loop-closing (OuterLoopClosing / InnerLoopClosing)
    /// are entirely SAME-SOURCE-ELEMENT scoped, per Rafi's explicit instruction —
    /// no cross-element/cross-mapping comparison anywhere in this tool. See
    /// LineJoiningService, which does all three overlap/collinear behaviors plus
    /// same-element intersection trim/extend in one pass per element's own curve list.
    /// </summary>
    public partial class ProcessingScope : ObservableObject
    {
        [ObservableProperty]
        private bool _limitToActiveView = true;

        [ObservableProperty]
        private bool _trimToBoundary = true;

        /// <summary>Closing behavior for OUTER Profile-group boundary loops, clipped
        /// independently from inner loops. See LoopClosingSettings.</summary>
        public LoopClosingSettings OuterLoopClosing { get; } = new();

        /// <summary>Closing behavior for INNER Profile-group boundary loops (openings),
        /// clipped independently from outer loops. See LoopClosingSettings.</summary>
        public LoopClosingSettings InnerLoopClosing { get; } = new();

        /// <summary>Fully deletes a shorter straight segment when it is completely
        /// engulfed/swallowed by a longer overlapping segment on the same axis, within
        /// the same source element's own curve list. Never touches partial overlaps —
        /// see MergePartialOverlaps for that case.</summary>
        [ObservableProperty]
        private bool _removeEngulfedOnly = false;

        /// <summary>When two segments on the same axis (within the same source
        /// element) partially overlap — neither fully contains the other — combines
        /// them into a single new line covering the full combined extent.</summary>
        [ObservableProperty]
        private bool _mergePartialOverlaps = false;

        /// <summary>Joins adjacent, NON-overlapping straight segments (from the same
        /// source element) that either share an endpoint exactly or fall within
        /// <see cref="LineJoinToleranceMm"/> perpendicular tolerance of the same axis.
        /// Also gates same-element intersection trim/extend: two non-parallel segments
        /// from the same element that fall short of their true geometric intersection
        /// by no more than the tolerance are extended/trimmed to meet there exactly
        /// (never fabricating a closed loop on its own).</summary>
        [ObservableProperty]
        private bool _joinCollinearLines = false;

        /// <summary>Shared alignment tolerance (millimeters) for all overlap/collinear/
        /// intersection-trim checks above — perpendicular offset for axis matching,
        /// gap distance for collinear joining, and extension distance for intersection
        /// trim/extend.</summary>
        [ObservableProperty]
        private double _lineJoinToleranceMm = 1.0;
    }

    /// <summary>
    /// CloseOpenLoops and CapOpenEnds are mutually exclusive finishing behaviors for
    /// a trimmed Profile boundary loop — enabling one turns the other off. Handles its
    /// own exclusivity internally (rather than MainViewModel cascading it) so it can be
    /// instantiated twice (Outer/Inner) without duplicating that logic.
    /// </summary>
    public partial class LoopClosingSettings : ObservableObject
    {
        /// <summary>When true, an open chain produced by clipping a closed Profile
        /// boundary loop is stitched back into a closed loop by walking along the clip
        /// boundary between the two open ends (shorter-path direction).</summary>
        [ObservableProperty]
        private bool _closeOpenLoops = false;

        /// <summary>When true (and CloseOpenLoops is false), a single straight segment
        /// caps each open end pair instead of leaving the boundary open.</summary>
        [ObservableProperty]
        private bool _capOpenEnds = false;

        private bool _suppressCascade;

        public LoopClosingSettings()
        {
            PropertyChanged += OnSelfChanged;
        }

        private void OnSelfChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressCascade) return;

            if (e.PropertyName == nameof(CloseOpenLoops) && CloseOpenLoops)
            {
                _suppressCascade = true;
                CapOpenEnds = false;
                _suppressCascade = false;
            }
            else if (e.PropertyName == nameof(CapOpenEnds) && CapOpenEnds)
            {
                _suppressCascade = true;
                CloseOpenLoops = false;
                _suppressCascade = false;
            }
        }
    }
}
