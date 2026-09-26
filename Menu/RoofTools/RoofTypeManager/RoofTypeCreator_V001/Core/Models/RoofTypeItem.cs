using CommunityToolkit.Mvvm.ComponentModel;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTypeCreator.V001.Core.Models
{
    public partial class RoofTypeItem : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        public ElementId TypeId  { get; set; }
        public string TypeName   { get; set; }
        public string TypeMark   { get; set; }
        public string Function   { get; set; }
        public int    LayerCount { get; set; }
        public double TotalThicknessMm { get; set; }
    }
}
