using WpfColor = System.Windows.Media.Color;

namespace Revit26_Plugin.LinesFromMechanical.V010.Models;

public class ColorOption
{
    public string   Name  { get; set; } = string.Empty;
    public WpfColor Color { get; set; }
    public override string ToString() => Name;
}
