using System;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Revit26_Plugin.RoofEdgeElementSections.V001
{
    /// <summary>
    /// UI-bindable wrapper around a single roof picked into the "1. Roofs" list via
    /// the PickRoofs command. Each row carries its own RemoveCommand, invoking
    /// RemoveAction (wired by the owning ViewModel) so this class doesn't need a
    /// back-reference to the ViewModel itself.
    /// </summary>
    public partial class SelectedRoofItem : ObservableObject
    {
        public ElementId RoofId { get; }
        public RoofBase RoofElement { get; }
        public string DisplayName { get; }

        /// <summary>Invoked by the RemoveCommand; wired by the owning ViewModel.</summary>
        public Action<SelectedRoofItem> RemoveAction { get; set; }

        public SelectedRoofItem(RoofBase roof, string displayName)
        {
            RoofElement = roof;
            RoofId = roof.Id;
            DisplayName = displayName;
        }

        [RelayCommand]
        private void Remove() => RemoveAction?.Invoke(this);
    }
}
