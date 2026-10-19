namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Models
{
    /// <summary>
    /// How line styles and hatch (filled region) types are chosen for the converted layers.
    /// </summary>
    public enum MappingMode
    {
        /// <summary>One global line style and one global hatch type apply to every selected layer.</summary>
        Single,

        /// <summary>Each layer row has its own line style / hatch type; untouched rows follow the global values.</summary>
        Multiple
    }
}
