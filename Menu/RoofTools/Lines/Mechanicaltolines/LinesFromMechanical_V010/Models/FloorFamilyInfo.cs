using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.LinesFromMechanical.V010.Models;

public class FloorFamilyInfo
{
    public string          Name       { get; set; } = string.Empty;
    public List<FloorType> FloorTypes { get; set; } = [];
    public override string ToString() => Name;
}
