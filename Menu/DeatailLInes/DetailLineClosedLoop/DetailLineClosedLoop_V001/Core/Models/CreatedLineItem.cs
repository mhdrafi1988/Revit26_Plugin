using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Models
{
    /// <summary>One row in the Created Lines grid — a single detail curve drawn by the most recent run.</summary>
    public partial class CreatedLineItem : ObservableObject
    {
        public ElementId Id { get; }
        public int Index { get; }
        public string TypeName { get; }
        public double LengthMm { get; }

        [ObservableProperty] private bool isChecked = true;

        public CreatedLineItem(ElementId id, int index, string typeName, double lengthMm)
        {
            Id = id;
            Index = index;
            TypeName = typeName;
            LengthMm = lengthMm;
        }
    }
}
