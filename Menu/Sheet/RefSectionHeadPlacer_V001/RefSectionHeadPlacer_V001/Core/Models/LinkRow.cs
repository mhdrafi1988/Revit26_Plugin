using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Models
{
    /// <summary>
    /// Bindable row for the new "Linked Files" checkbox list (mirrors PlanViewRow's
    /// pattern). Multiple links can now be checked at once — elements are
    /// collected from every checked link, kept separate per link (never merged),
    /// since two links can each have a type named e.g. "Floor Drain 100mm" that
    /// are NOT the same element set.
    /// </summary>
    public partial class LinkRow : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        /// <summary>Display name — the link instance's name, which Revit keeps
        /// unique even if the same file is linked in more than once.</summary>
        public string Name { get; }

        public RevitLinkInstance Instance { get; }
        public Document LinkDocument { get; }

        public LinkRow(RevitLinkInstance instance)
        {
            Instance = instance;
            LinkDocument = instance.GetLinkDocument();
            Name = instance.Name;
        }
    }
}
