namespace Revit26_Plugin.BubbleAutoRenumber.V006.Models
{
    public class SectionRowViewModel
    {
        public string CurrentNumber { get; init; } = string.Empty;
        public string ViewName      { get; init; } = string.Empty;
        public bool   IsReadOnly    { get; init; }
    }
}
