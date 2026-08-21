using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models
{
    /// <summary>
    /// Section 6 settings: how non-analytic (spline/NURBS) boundary edges are handled.
    /// Default (ReplaceWithFallback = false): tessellate via Curve.Tessellate() and
    /// rebuild as a single HermiteSpline Detail Line — closest to true shape.
    /// When enabled: replace with the selected SplineFallbackShape instead.
    /// Persisted to settings.json.
    /// </summary>
    public partial class ComplexCurveSettings : ObservableObject
    {
        [ObservableProperty]
        private bool _replaceWithFallback = false;

        [ObservableProperty]
        private SplineFallbackShape _fallbackShape = SplineFallbackShape.StraightChord;
    }
}
