using Autodesk.Revit.DB;

namespace Revit26_Plugin.ParaManager.V003.Models
{
    /// <summary>
    /// Lightweight wrapper around a Revit Category for display/selection in the UI.
    /// Keeps the BuiltInCategory enum value alongside the display name so binding
    /// logic (CategorySet construction) doesn't need to re-resolve names later.
    /// </summary>
    public class CategoryInfo
    {
        /// <summary>Display name shown to the user (e.g. "Roofs").</summary>
        public string Name { get; }

        /// <summary>Underlying Revit BuiltInCategory enum value.</summary>
        public BuiltInCategory BuiltInCategory { get; }

        public CategoryInfo(string name, BuiltInCategory builtInCategory)
        {
            Name = name;
            BuiltInCategory = builtInCategory;
        }

        public override string ToString() => Name;
    }
}
