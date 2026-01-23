using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.WSFL_008.Models
{
    public partial class WorksetItem : ObservableObject
    {
        public string LinkName { get; }

        [ObservableProperty]
        private string previewName;

        [ObservableProperty]
        private bool isSelected;

        public WorksetItem(string linkName)
        {
            LinkName = linkName;
        }
    }
}
