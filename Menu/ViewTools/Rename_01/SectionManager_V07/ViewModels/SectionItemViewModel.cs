using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionManager_V07.ViewModels
{
    public class SectionItemViewModel : BaseViewModel
    {
        public int Index { get; set; }

        public ElementId ElementId { get; }
        public string OriginalName { get; private set; }

        public string SheetNumber { get; set; }
        public string DetailNumber { get; set; }

        private string _editableName;
        public string EditableName
        {
            get => _editableName;
            set
            {
                if (_editableName == value) return;
                _editableName = value;
                RaisePropertyChanged();
            }
        }

        private string _previewName;
        public string PreviewName
        {
            get => _previewName;
            set
            {
                if (_previewName == value) return;
                _previewName = value;
                RaisePropertyChanged();
            }
        }

        public SectionItemViewModel(ElementId id, string name)
        {
            ElementId = id;
            OriginalName = name;

            EditableName = name;
            PreviewName = name;
        }

        /// <summary>
        /// Call after rename succeeds
        /// </summary>
        public void CommitRename()
        {
            OriginalName = PreviewName;
            RaisePropertyChanged(nameof(OriginalName));
        }
    }
}
