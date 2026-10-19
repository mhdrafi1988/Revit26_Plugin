namespace Revit26_Plugin.SheetViewArrange.V001.Core.Models
{
    /// <summary>
    /// How the Views grid is grouped. Display only — it never changes the layout order, which
    /// always follows the detail numbers.
    /// </summary>
    public enum ArrangeGroupBy
    {
        /// <summary>One flat list.</summary>
        None = 0,

        /// <summary>One group per view type (Section, Legend, ...).</summary>
        ViewType = 1,

        /// <summary>One group per status (Move, Doesn't fit, Skipped, ...).</summary>
        Status = 2,

        /// <summary>One group per layout row on the sheet.</summary>
        Row = 3
    }

    /// <summary>A grid column the list can be sorted by. Display only.</summary>
    public enum ArrangeSortColumn
    {
        /// <summary>The "#" column — position in the arrangement.</summary>
        Order,

        /// <summary>Detail number (natural order: 1, 2, 10, A1).</summary>
        DetailNumber,

        /// <summary>View name.</summary>
        ViewName,

        /// <summary>View type.</summary>
        ViewType,

        /// <summary>Footprint area.</summary>
        Size,

        /// <summary>Layout row.</summary>
        Row,

        /// <summary>Planned status.</summary>
        Status
    }

    /// <summary>One column in the sort, with its direction.</summary>
    /// <param name="Column">The column.</param>
    /// <param name="Descending">True for Z–A / large–small.</param>
    public readonly record struct ArrangeSortKey(ArrangeSortColumn Column, bool Descending);

    /// <summary>Which rows the grid shows by tick state.</summary>
    public enum ArrangeShowMode
    {
        /// <summary>Ticked and unticked rows.</summary>
        All = 0,

        /// <summary>Only ticked rows.</summary>
        Ticked = 1,

        /// <summary>Only unticked rows.</summary>
        Unticked = 2
    }
}
