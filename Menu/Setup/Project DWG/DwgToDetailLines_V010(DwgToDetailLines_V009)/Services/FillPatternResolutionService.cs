using Autodesk.Revit.UI;
using System.Collections.Generic;
using Revit26_Plugin.DwgToDetailLines.V010.Models;

namespace Revit26_Plugin.DwgToDetailLines.V010.Services
{
    /// <summary>
    /// Resolves the Create/Skip decision for a hatch layer with no existing
    /// FilledRegionType match. Mirrors LineStyleResolutionService.
    /// ASSUMPTION (flagged, unconfirmed): the global "Default Fill Pattern"
    /// dropdown pre-fills the suggested pattern shown in this prompt but does
    /// NOT bypass it — per-layer Create/Skip is still asked, same as V002's
    /// line-style flow. If the intent was for the global default to skip this
    /// prompt entirely, this needs to change.
    /// </summary>
    public class FillPatternResolutionService
    {
        private readonly Dictionary<string, MissingFillPatternDecision> _cache = new();

        public MissingFillPatternDecision Resolve(string layerName, string defaultPatternName)
        {
            if (_cache.TryGetValue(layerName, out var decision))
                return decision;

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

            TaskDialogResult result = dialog.Show();

            decision = result == TaskDialogResult.CommandLink1
                ? MissingFillPatternDecision.Create
                : MissingFillPatternDecision.Skip;

            _cache[layerName] = decision;
            return decision;
        }
    }
}
