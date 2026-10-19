using Autodesk.Revit.DB;

namespace Revit26_Plugin.TaperedEpsPattern2D.V005.Revit
{
    /// <summary>A text note type for the label picker.</summary>
    public sealed class TextNoteTypeOption
    {
        public TextNoteTypeOption(string name, ElementId typeId)
        {
            Name = name;
            TypeId = typeId;
        }

        public string Name { get; }
        public ElementId TypeId { get; }

        public override string ToString() => Name;
    }
}
