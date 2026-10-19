using Autodesk.Revit.DB;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V002.Revit
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
