namespace Revit26_Plugin.SheetViewArrange.V001.Core.Models
{
    /// <summary>Status of one view in the planned arrangement.</summary>
    public enum ArrangeStatus
    {
        /// <summary>Will be moved to its slot.</summary>
        Move,

        /// <summary>Already in its slot — nothing to do.</summary>
        InPlace,

        /// <summary>Its slot falls outside the usable area.</summary>
        DoesNotFit,

        /// <summary>Left where it is (pinned, or owned by another user).</summary>
        Skipped
    }

    /// <summary>One line in the Views grid: a viewport and where the plan puts it.</summary>
    public sealed class ArrangeRow
    {
        /// <summary>Reading-order position (1-based), or null when skipped.</summary>
        public int? Order { get; init; }

        /// <summary>The viewport's Detail Number.</summary>
        public string DetailNumber { get; init; }

        /// <summary>View name.</summary>
        public string ViewName { get; init; }

        /// <summary>View type display name.</summary>
        public string ViewTypeName { get; init; }

        /// <summary>"W × H" footprint in mm.</summary>
        public string SizeText { get; init; }

        /// <summary>1-based row on the sheet, or null when skipped.</summary>
        public int? Row { get; init; }

        /// <summary>Planned status.</summary>
        public ArrangeStatus Status { get; init; }

        /// <summary>Status as shown in the grid.</summary>
        public string StatusLabel => Status switch
        {
            ArrangeStatus.Move => "Move",
            ArrangeStatus.InPlace => "In place",
            ArrangeStatus.DoesNotFit => "Doesn't fit",
            _ => "Skipped"
        };

        /// <summary>Short explanation shown next to the status.</summary>
        public string Note { get; init; }
    }
}
