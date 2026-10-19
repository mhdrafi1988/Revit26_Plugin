namespace Revit26_Plugin.BubbleAutoRenumber.V008.Models
{
    public class SectionRowViewModel
    {
        public string CurrentNumber { get; init; } = string.Empty;
        public string ViewName      { get; init; } = string.Empty;
        public bool   IsReadOnly    { get; init; }
    }
}
