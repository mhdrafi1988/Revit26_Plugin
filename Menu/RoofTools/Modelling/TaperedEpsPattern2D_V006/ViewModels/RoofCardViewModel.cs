using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin_Revised.TaperedEpsPattern2D.V006.Revit;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V006.ViewModels
{
    /// <summary>Roof card: view, roof, level, edges, shape badge and any validation error.</summary>
    public sealed partial class RoofCardViewModel : ObservableObject
    {
        [ObservableProperty] private string _viewName = string.Empty;
        [ObservableProperty] private string _roofName = string.Empty;
        [ObservableProperty] private string _levelName = string.Empty;
        [ObservableProperty] private string _edges = string.Empty;
        [ObservableProperty] private string _shapeBadge = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string? _errorText;

        public bool HasError => !string.IsNullOrEmpty(ErrorText);

        public void Update(RoofContext context)
        {
            ViewName = context.ViewName;
            RoofName = context.RoofName;
            LevelName = context.LevelName;
            ShapeBadge = context.Outline.ShapeName;
            Edges = context.Outline.Outline != null
                ? string.Format(CultureInfo.CurrentCulture, "{0} (sketch: {1})", context.Outline.Outline.Count, context.SketchEdgeCount)
                : context.SketchEdgeCount.ToString(CultureInfo.CurrentCulture);
            ErrorText = context.Outline.Errors.Count > 0 ? string.Join("\n", context.Outline.Errors) : null;
        }
    }
}
