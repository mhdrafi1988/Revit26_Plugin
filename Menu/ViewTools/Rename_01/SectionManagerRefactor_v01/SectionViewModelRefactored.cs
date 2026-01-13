using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;

namespace Revit22_Plugin.SectionManagerMVVM_Refactored
{
    public class SectionViewModelRefactored : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        internal readonly ViewSection _section;
        private readonly Document _doc;

        // ✅ Added for RenameSectionHandlerRefactored
        public ElementId ElementId => _section.Id;

        public int Serial { get; set; }
        public string DetailNum { get; }
        public string OriginalName => _section.Name;

        // ✅ Added to track renamed values
        public string OldName { get; set; }

        private string _editableName;
        public string EditableName
        {
            get => _editableName;
            set
            {
                if (_editableName != value)
                {
                    _editableName = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _previewName;
        public string PreviewName
        {
            get => _previewName;
            set
            {
                if (_previewName != value)
                {
                    _previewName = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand CombineCommand { get; }

        public string SheetName { get; }
        public string SheetNumber { get; }

        private bool _isDuplicate;
        public bool IsDuplicate
        {
            get => _isDuplicate;
            set
            {
                if (_isDuplicate != value)
                {
                    _isDuplicate = value;
                    OnPropertyChanged();
                }
            }
        }

        public SectionViewModelRefactored(
            int serial,
            ViewSection section,
            string sheetName,
            string sheetNumber,
            string detailNum
        )
        {
            _section = section ?? throw new ArgumentNullException(nameof(section));
            _doc = section.Document ?? throw new ArgumentNullException(nameof(section.Document));

            Serial = serial;
            DetailNum = detailNum;
            SheetName = sheetName;
            SheetNumber = sheetNumber;

            EditableName = section.Name;
            PreviewName = section.Name;

            CombineCommand = new RelayCommandRefactored(_ =>
            {
                PreviewName = $"{DetailNum} {EditableName}";
            });
        }
    }
}
