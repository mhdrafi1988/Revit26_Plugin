using System.Collections.Generic;

namespace Revit26_Plugin.SectionAutoRenumber.Models
{
    public class RenumberSummary
    {
        public string SheetNumber { get; set; } = string.Empty;
        public string SheetName   { get; set; } = string.Empty;
        public int    Total       { get; set; }
        public int    Success     { get; set; }
        public List<string> Failed     { get; } = new();
        public List<string> LogLines   { get; } = new();
    }
}
