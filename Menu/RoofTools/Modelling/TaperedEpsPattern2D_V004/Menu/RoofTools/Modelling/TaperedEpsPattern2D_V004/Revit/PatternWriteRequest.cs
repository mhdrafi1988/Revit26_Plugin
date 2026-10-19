using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.TaperedEpsPattern2D.V004.Core.Pattern;

namespace Revit26_Plugin.TaperedEpsPattern2D.V004.Revit
{
    /// <summary>Everything the writer needs, captured on the UI side before ExternalEvent.Raise().</summary>
    public sealed class PatternWriteRequest
    {
        public PatternWriteRequest(RoofContext roof, Pattern2DResult pattern)
        {
            Roof = roof;
            Pattern = pattern;
        }

        public RoofContext Roof { get; }
        public Pattern2DResult Pattern { get; }

        /// <summary>GraphicsStyle id per layer (projection style of an OST_Lines subcategory).</summary>
        public IReadOnlyDictionary<PatternLayer, ElementId> LineStyles { get; init; } =
            new Dictionary<PatternLayer, ElementId>();

        public ElementId? TextNoteTypeId { get; init; }
        public GroupingMode Grouping { get; init; } = GroupingMode.SingleGroup;

        /// <summary>Delete the previous grouped pattern for this roof and view first (needs a grouping mode other than NoGroup).</summary>
        public bool ReplacePrevious { get; init; } = true;
    }
}
