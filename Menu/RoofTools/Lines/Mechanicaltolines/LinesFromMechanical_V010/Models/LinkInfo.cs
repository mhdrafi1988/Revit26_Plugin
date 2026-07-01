using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinesFromMechanical.V010.Models;

public class LinkInfo
{
    public ElementId         Id       { get; set; } = ElementId.InvalidElementId;
    public string            Name     { get; set; } = string.Empty;
    public RevitLinkInstance Instance { get; set; } = null!;
    public override string ToString() => Name;
}
