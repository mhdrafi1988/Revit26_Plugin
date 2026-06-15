using Autodesk.Revit.UI;
using System.Collections.Generic;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Services
{
    public class LineStyleResolutionService
    {
        private readonly Dictionary<string, MissingLineStyleDecision> _cache = new();

        public MissingLineStyleDecision Resolve(string layerName)
        {
            if (_cache.TryGetValue(layerName, out var decision))
                return decision;

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

            TaskDialogResult result = dialog.Show();

            decision = result == TaskDialogResult.CommandLink1
                ? MissingLineStyleDecision.Create
                : MissingLineStyleDecision.Skip;

            _cache[layerName] = decision;
            return decision;
        }
    }
}
