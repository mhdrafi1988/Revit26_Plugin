using Autodesk.Revit.UI;
using System.Collections.Generic;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>
    /// Asks Create / Skip for a layer whose line style does not exist. Besides the per-layer
    /// choices the prompt offers "Create all remaining" and "Skip all remaining", which answer
    /// every further missing layer in the same run without asking again.
    /// </summary>
    public class LineStyleResolutionService
    {
        private readonly Dictionary<string, MissingLineStyleDecision> _cache = new();
        private MissingLineStyleDecision? _forAll;

        /// <summary>Returns the Create / Skip decision for <paramref name="layerName"/>, asking at most once per layer.</summary>
        public MissingLineStyleDecision Resolve(string layerName)
        {
            if (_cache.TryGetValue(layerName, out var decision))
                return decision;

            if (_forAll.HasValue)
                return _forAll.Value;

            TaskDialog dialog = new TaskDialog("Missing Line Style")
            {
                MainInstruction = $"Line style \"{layerName}\" not found.",
                MainContent = "Choose how to proceed."
            };

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink1,
                "Create line style");

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink2,
                "Skip this layer");

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink3,
                "Create all remaining missing line styles");

            dialog.AddCommandLink(
                TaskDialogCommandLinkId.CommandLink4,
                "Skip all remaining missing line styles");

            TaskDialogResult result = dialog.Show();

            if (result == TaskDialogResult.CommandLink3)
                _forAll = MissingLineStyleDecision.Create;
            else if (result == TaskDialogResult.CommandLink4)
                _forAll = MissingLineStyleDecision.Skip;

            decision = result is TaskDialogResult.CommandLink1 or TaskDialogResult.CommandLink3
                ? MissingLineStyleDecision.Create
                : MissingLineStyleDecision.Skip;

            _cache[layerName] = decision;
            return decision;
        }
    }
}
