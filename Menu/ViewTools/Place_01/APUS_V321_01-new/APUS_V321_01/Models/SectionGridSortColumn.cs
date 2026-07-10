// File: Models/SectionGridSortColumn.cs
namespace Revit26_Plugin.APUS_V321_01.Models
{
    /// <summary>
    /// Columns the Sections DataGrid can be sorted by. Checkbox column is not
    /// sortable (there is nothing meaningful to sort by there beyond
    /// selected/unselected, which Select All / None / Invert already covers).
    /// </summary>
    public enum SectionGridSortColumn
    {
        Name,
        Size,
        Placed,
        DetailNumber
    }
}
