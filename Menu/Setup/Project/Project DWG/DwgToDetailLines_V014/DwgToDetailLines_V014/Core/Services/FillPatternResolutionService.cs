using Autodesk.Revit.UI;
using System.Collections.Generic;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>
    /// Resolves the Create/Skip decision for a hatch layer with no existing
    /// FilledRegionType match. Mirrors LineStyleResolutionService.
    /// ASSUMPTION (flagged, unconfirmed): the global "Default Fill Pattern"
    /// dropdown pre-fills the suggested pattern shown in this prompt but does
    /// NOT bypass it — per-layer Create/Skip is still asked. If the intent
    /// was for the global default to skip this prompt entirely, this needs
    /// to change.
    /// </summary>
    public class FillPatternResolutionService
    {
        private readonly Dictionary<string, MissingFillPatternDecision> _cache = new();
        private MissingFillPatternDecision? _forAll;

        /// <summary>
        /// Returns the Create / Skip decision for a hatch layer, asking at most once per layer.
        /// "Create all remaining" / "Skip all remaining" answer every further missing layer in the run.
        /// </summary>
        public MissingFillPatternDecision Resolve(string layerName, string defaultPatternName)
        {
            if (_cache.TryGetValue(layerName, out var decision))
                return decision;

            if (_forAll.HasValue)
                return _forAll.Value;

            TaskDialog dialog = new TaskDialog("Missing Fill Pattern")
            {
                MainInstruction = $"Fill pattern for hatch layer \"{layerName}\" not found.",
                MainContent = $"Choose how to proceed. Default pattern: \"{defaultPatternName}\"."
            };

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink1,
                $"Create using \"{defaultPatternName}\"");

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink2,
                "Skip this layer");

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink3,
                "Create all remaining missing hatch types");

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink4,
                "Skip all remaining missing hatch types");

            TaskDialogResult result = dialog.Show();

            if (result == TaskDialogResult.CommandLink3)
                _forAll = MissingFillPatternDecision.Create;
            else if (result == TaskDialogResult.CommandLink4)
                _forAll = MissingFillPatternDecision.Skip;

            decision = result is TaskDialogResult.CommandLink1 or TaskDialogResult.CommandLink3
                ? MissingFillPatternDecision.Create
                : MissingFillPatternDecision.Skip;

            _cache[layerName] = decision;
            return decision;
        }
    }
}
