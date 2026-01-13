using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

// Explicit aliases to avoid conflict with Revit’s TextBox/ComboBox
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;

namespace Revit22_Plugin.SectionManagerMVVM_Refactored
{
    public partial class SectionsListWindowRefactored : Window
    {
        private readonly SectionsListViewModelRefactored _vm;
        public static UIDocument UiDoc;

        private List<string> AllSheets = new List<string>();

        private TextBox _sheetFilterTextBox;
        private bool _suppressFilter = false;

        public SectionsListWindowRefactored(UIDocument uiDoc)
        {
            InitializeComponent();

            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Topmost = true;


            UiDoc = uiDoc;

            // Collect all sections
            var sections = new FilteredElementCollector(uiDoc.Document)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate)
                .Select((v, i) => new SectionViewModelRefactored(
                    i + 1,
                    v,
                    "",
                    v.get_Parameter(BuiltInParameter.VIEWER_SHEET_NUMBER)?.AsString(),
                    v.get_Parameter(BuiltInParameter.VIEWER_DETAIL_NUMBER)?.AsString()
                ))
                .ToList();

            _vm = new SectionsListViewModelRefactored(sections, uiDoc);
            DataContext = _vm;

            LoadSheetList();
        }

        // ============================================================================
        // LOAD ALL SHEET NUMBERS + SET ACTIVE SHEET DEFAULT
        // ============================================================================
        private void LoadSheetList()
        {
            AllSheets = _vm.Sections
                .Select(s => s.SheetNumber)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            SheetFilterCombo.Items.Clear();
            SheetFilterCombo.Items.Add("All");
            SheetFilterCombo.Items.Add("None");

            foreach (var s in AllSheets)
                SheetFilterCombo.Items.Add(s);

            // --- Detect active sheet number safely ---
            string activeSheetNumber = null;
            if (UiDoc.ActiveView is ViewSheet vs)
                activeSheetNumber = vs.SheetNumber;

            // --- Select active sheet if available; otherwise fallback to All ---
            if (activeSheetNumber != null && SheetFilterCombo.Items.Contains(activeSheetNumber))
                SheetFilterCombo.SelectedItem = activeSheetNumber;
            else
                SheetFilterCombo.SelectedItem = "All";
        }

        // ============================================================================
        // HOOK INTERNAL EDITABLE TEXTBOX
        // ============================================================================
        private void SheetFilterCombo_Loaded(object sender, RoutedEventArgs e)
        {
            _sheetFilterTextBox =
                (TextBox)SheetFilterCombo.Template.FindName("PART_EditableTextBox", SheetFilterCombo);

            if (_sheetFilterTextBox != null)
            {
                _sheetFilterTextBox.TextChanged += SheetFilterInternalTextBox_TextChanged;
            }
        }

        // ============================================================================
        // FILTER ON TEXT INPUT (not on selection)
        // ============================================================================
        private void SheetFilterInternalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressFilter)
                return;

            if (_sheetFilterTextBox == null)
                return;

            string input = (_sheetFilterTextBox.Text ?? "").ToLower();

            string previous = SheetFilterCombo.SelectedItem as string;

            SheetFilterCombo.Items.Clear();
            SheetFilterCombo.Items.Add("All");
            SheetFilterCombo.Items.Add("None");

            var filtered = AllSheets
                .Where(s => s != null && s.ToLower().Contains(input))
                .ToList();

            foreach (var s in filtered)
                SheetFilterCombo.Items.Add(s);

            SheetFilterCombo.IsDropDownOpen = true;

            if (previous != null && SheetFilterCombo.Items.Contains(previous))
                SheetFilterCombo.SelectedItem = previous;

            _sheetFilterTextBox.CaretIndex = _sheetFilterTextBox.Text.Length;
        }

        // ============================================================================
        // APPLY SHEET FILTER
        // ============================================================================
        private void SheetFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SheetFilterCombo.SelectedItem == null)
                return;

            string selected = SheetFilterCombo.SelectedItem as string;
            if (selected == null)
                return;

            _suppressFilter = true;

            if (_sheetFilterTextBox != null)
            {
                _sheetFilterTextBox.Text = selected;
                _sheetFilterTextBox.CaretIndex = _sheetFilterTextBox.Text.Length;
            }

            _suppressFilter = false;

            _vm.SelectedSheetFilter = selected;
        }

        // ============================================================================
        // PREVIEW
        // ============================================================================
        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            _vm.UpdatePreview();
        }

        // ============================================================================
        // RENAME (Update)
        // ============================================================================
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            _vm.UpdatePreview();

            SectionsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            SectionsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);
            SectionsDataGrid.Items.Refresh();

            Document doc = UiDoc.Document;

            HashSet<string> existingNames = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            HashSet<string> used = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

            List<SectionViewModelRefactored> toRename = new List<SectionViewModelRefactored>();

            foreach (var vm in _vm.Sections)
            {
                if (!used.Add(vm.PreviewName))
                    continue;

                if (!string.Equals(vm.OriginalName, vm.PreviewName, StringComparison.Ordinal))
                    toRename.Add(vm);
            }

            if (toRename.Count > 0)
            {
                try
                {
                    SectionManagerEventManagerRefactored.RenameHandler.PayloadList = toRename;
                    SectionManagerEventManagerRefactored.RenameEvent.Raise();
                    MessageBox.Show("Rename request sent for " + toRename.Count + " sections.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Rename Error: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("No sections renamed.");
            }

            _vm.UpdatePreview();
        }

        // ============================================================================
        // TRUNCATE LAST DIGIT
        // ============================================================================
        private void TruncateLastDigit_Click(object sender, RoutedEventArgs e)
        {
            SectionsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            SectionsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

            foreach (var item in SectionsDataGrid.SelectedItems)
            {
                var vm = item as SectionViewModelRefactored;
                if (vm == null)
                    continue;

                string name = vm.EditableName ?? "";
                if (name.Length > 0)
                {
                    char last = name[name.Length - 1];
                    if (char.IsDigit(last))
                        vm.EditableName = name.Substring(0, name.Length - 1);
                }
            }
        }

        // ============================================================================
        // CLOSE WINDOW
        // ============================================================================
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
