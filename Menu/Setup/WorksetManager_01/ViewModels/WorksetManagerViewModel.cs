using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WorksetManager_01.Helpers;
using WorksetManager_01.Models;

namespace WorksetManager_01.ViewModels
{
    public partial class WorksetManagerViewModel : ObservableObject
    {
        private readonly Document _doc;

        public WorksetManagerViewModel(Document doc)
        {
            _doc = doc;
        }

        // ── Observable properties ──────────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<WorksetSummaryItem> _worksetItems = new();

        [ObservableProperty]
        private WorksetSummaryItem? _selectedWorkset;

        [ObservableProperty]
        private bool _includeLinkedModels = false;

        [ObservableProperty]
        private bool _isScanning = false;

        [ObservableProperty]
        private string _statusText = "Ready. Click Scan to begin.";

        [ObservableProperty]
        private string _searchText = string.Empty;

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        // Filtered view
        public ObservableCollection<WorksetSummaryItem> FilteredItems { get; } = new();

        // ── Commands ───────────────────────────────────────────────────────

        [RelayCommand]
        private async Task ScanAsync()
        {
            IsScanning = true;
            StatusText = "Scanning worksets…";
            WorksetItems.Clear();
            FilteredItems.Clear();

            try
            {
                var results = await Task.Run(() =>
                    WorksetScanner.Scan(_doc, IncludeLinkedModels));

                foreach (var item in results)
                    WorksetItems.Add(item);

                ApplyFilter();

                int total = results.Sum(r => r.TotalElements);
                StatusText = $"Done — {results.Count} worksets, {total} elements total.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Scan Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
            }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            if (WorksetItems.Count == 0)
            {
                MessageBox.Show("Nothing to export. Run a scan first.",
                    "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "Export Workset Summary",
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"WorksetSummary_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                string path = ExcelExportHelper.Export(WorksetItems.ToList(), dlg.FileName);
                StatusText = $"Exported to {path}";
                MessageBox.Show($"Exported successfully:\n{path}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CopyToClipboard()
        {
            if (FilteredItems.Count == 0)
            {
                MessageBox.Show("Nothing to copy. Run a scan first.",
                    "Copy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Workset Name\tTotal Elements\tStatus\tEditable\tType Breakdown");

            foreach (var item in FilteredItems)
                sb.AppendLine($"{item.WorksetName}\t{item.TotalElements}\t{item.StatusLabel}\t{item.EditableLabel}\t{item.TypeBreakdown}");

            Clipboard.SetText(sb.ToString());
            StatusText = "Copied to clipboard.";
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private void ApplyFilter()
        {
            FilteredItems.Clear();
            var query = WorksetItems.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(w =>
                    w.WorksetName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    w.TypeBreakdown.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            foreach (var item in query)
                FilteredItems.Add(item);
        }
    }
}
