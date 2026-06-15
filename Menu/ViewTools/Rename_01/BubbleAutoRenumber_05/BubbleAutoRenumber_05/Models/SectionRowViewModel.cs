namespace Revit26_Plugin.SectionAutoRenumber.Models
{
    public class SectionRowViewModel
    {
        public string CurrentNumber { get; init; } = string.Empty;
        public string ViewName      { get; init; } = string.Empty;
        public bool   IsReadOnly    { get; init; }
    }
}
