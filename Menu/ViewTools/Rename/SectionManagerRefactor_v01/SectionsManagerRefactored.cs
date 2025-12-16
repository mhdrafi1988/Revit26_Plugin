using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit22_Plugin.SectionManagerMVVM_Refactored
{
    public class SectionsListViewModelRefactored : INotifyPropertyChanged
    {
        public ObservableCollection<SectionViewModelRefactored> Sections { get; set; }
        private List<SectionViewModelRefactored> _allSections;

        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public string PrefixText { get; set; } = "";
        public string PostfixText { get; set; } = "";
        public string FindText { get; set; } = "";
        public string ReplaceText { get; set; } = "";
        public string SerialFormat { get; set; } = "00";
        public bool AddSerial { get; set; } = false;
        public bool IncludeDetailNumber { get; set; } = false;

        private string _commonEditName;
        public string CommonEditName
        {
            get => _commonEditName;
            set
            {
                _commonEditName = value;
                ApplyCommonEditName();
                OnPropertyChanged();
            }
        }

        private string _selectedSheetFilter;
        public string SelectedSheetFilter
        {
            get => _selectedSheetFilter;
            set
            {
                _selectedSheetFilter = value;
                ApplyFilter();
                OnPropertyChanged();
            }
        }

        public ListCollectionView SheetFilters { get; set; }

        public SectionsListViewModelRefactored(List<SectionViewModelRefactored> sections, UIDocument uidoc)
        {
            _allSections = sections ?? throw new ArgumentNullException(nameof(sections));
            _uidoc = uidoc ?? throw new ArgumentNullException(nameof(uidoc));
            _doc = _uidoc.Document ?? throw new ArgumentNullException(nameof(uidoc.Document));

            Sections = new ObservableCollection<SectionViewModelRefactored>(sections);

            var uniqueSheets = sections.Select(s => s.SheetNumber).Distinct().ToList();
            uniqueSheets.Insert(0, "All");

            SheetFilters = new ListCollectionView(uniqueSheets);
            SelectedSheetFilter = "All";

            UpdatePreview();
        }

        public void ApplyCommonEditName()
        {
            foreach (var vm in Sections)
            {
                vm.EditableName = CommonEditName;
            }
            UpdatePreview();
        }

        public void ApplyFilter()
        {
            if (SelectedSheetFilter == "All")
            {
                Sections = new ObservableCollection<SectionViewModelRefactored>(_allSections);
            }
            else
            {
                Sections = new ObservableCollection<SectionViewModelRefactored>(
                    _allSections.Where(s => s.SheetNumber == SelectedSheetFilter));
            }
            OnPropertyChanged(nameof(Sections));
        }

        public void UpdatePreview()
        {
            int idx = 1;

            // Collect all existing section view names from the Revit document (excluding templates)
            var allViewNames = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var vm in _allSections)
            {
                var baseName = vm.EditableName ?? vm.OriginalName;

                if (!string.IsNullOrEmpty(FindText))
                    baseName = baseName.Replace(FindText, ReplaceText ?? "");

                var detail = IncludeDetailNumber ? $" {vm.DetailNum}" : string.Empty;
                var serial = AddSerial ? $" {idx.ToString(SerialFormat)}" : string.Empty;

                var raw = $"{PrefixText}{baseName}{PostfixText}{detail}{serial}";
                var clean = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", " ").Trim();
                var titleCase = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                    .ToTitleCase(clean.ToLower(System.Globalization.CultureInfo.CurrentCulture));

                string finalName = titleCase;

                // Check if this name already exists in the Revit document (excluding the same section)
                if (!string.Equals(vm.OriginalName, finalName, StringComparison.OrdinalIgnoreCase)
                    && allViewNames.Contains(finalName))
                {
                    int dupIndex = 0;
                    string dupCandidate;

                    do
                    {
                        dupCandidate = dupIndex == 0
                            ? $"{finalName} (dup)"
                            : $"{finalName} (dup{dupIndex})";
                        dupIndex++;
                    }
                    while (allViewNames.Contains(dupCandidate));

                    finalName = dupCandidate;
                }

                vm.PreviewName = finalName;
                idx++;
            }

            ApplyFilter();
            CheckForDuplicateNames(); // still marks internal UI duplicates
        }

        private void CheckForDuplicateNames()
        {
            var nameGroups = Sections
                .GroupBy(s => s.EditableName?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Key != string.Empty)
                .ToDictionary(g => g.Key, g => g.Count());

            int duplicateCount = 0;

            foreach (var vm in Sections)
            {
                bool isDup = vm.EditableName != null &&
                             nameGroups.TryGetValue(vm.EditableName.Trim(), out int count) &&
                             count > 1;

                vm.IsDuplicate = isDup;
                if (isDup) duplicateCount++;
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var window = Application.Current.Windows.OfType<SectionsListWindowRefactored>().FirstOrDefault();
                if (window?.DuplicatesBox != null)
                {
                    window.DuplicatesBox.Visibility = duplicateCount > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                    window.DuplicatesBox.Text = duplicateCount > 0
                        ? $"{duplicateCount} duplicate name(s) found in the list. 'dup' will be auto-added if needed."
                        : string.Empty;
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
