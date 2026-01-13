using System.Collections.Generic;

namespace Revit26_Plugin.SDRV4.Renumber
{
    public class RenumberSummaryV4
    {
        public string SheetName { get; set; }
        public string SheetNumber { get; set; }

        public int Total { get; set; }
        public int Success { get; set; }

        public List<string> Failed { get; } = new List<string>();
        public string LogMessage { get; set; } = "";
    }
}
