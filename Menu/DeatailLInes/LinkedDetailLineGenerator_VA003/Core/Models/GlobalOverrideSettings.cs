using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models
{
    /// <summary>
    /// Master override for the Mapping Grid (Section 3): when IsEnabled, LineStyleName
    /// and ColorName here are used for EVERY enabled mapping's generated Detail Lines,
    /// in place of that mapping's own DetailLineStyleName/ColorName — regardless of what
    /// each row has individually selected. Per-row values are never overwritten by this;
    /// the override is applied only at generation time (CreateDetailLinesEventHandler),
    /// so turning it back off restores each row's own choice exactly as it was.
    /// Persisted to settings.json.
    /// </summary>
    public partial class GlobalOverrideSettings : ObservableObject
    {
        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private string _lineStyleName = string.Empty;

        [ObservableProperty]
        private string _colorName = "None";
    }
}
