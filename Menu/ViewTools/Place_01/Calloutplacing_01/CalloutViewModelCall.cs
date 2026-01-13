using Autodesk.Revit.DB;
using Revit22_Plugin.callout.Commands;
using Revit22_Plugin.callout.Helpers;
using Revit22_Plugin.callout.Models;

namespace Revit22_Plugin.callout.Models
{
    public class CalloutViewModelCall
    {
        public string SectionName { get; set; }
        public string SheetName { get; set; }
        public string SheetNumber { get; set; }
        public string DetailNumber { get; set; }
        public ElementId ViewId { get; set; }
        public bool IsPlaced { get; set; }
        public bool IsSelected { get; set; }
    }
}
