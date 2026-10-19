using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.TaperedEpsPattern2D.V004.ViewModels
{
    /// <summary>One header metric tile (label + value).</summary>
    public sealed partial class MetricTileViewModel : ObservableObject
    {
        public MetricTileViewModel(string label)
        {
            Label = label;
        }

        public string Label { get; }

        [ObservableProperty]
        private string _value = "—";
    }
}
