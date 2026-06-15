using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Revit26_Plugin.WorksetManager_02.Helpers;
using Revit26_Plugin.WorksetManager_02.Models;

namespace Revit26_Plugin.WorksetManager_02.ViewModels
{
    public partial class WorksetManagerViewModel : ObservableObject
    {
        private readonly Document _doc;

        public WorksetManagerViewModel(Document doc)
        {
            _doc = doc;
        }

        // ── Observable collections ─────────────────────────────────────────

        public ObservableCollection<ExactMatchItem>       ExactMatches    { get; } = new();
        public ObservableCollection<LinkWorksetMatchItem> ActionableLinks { get; } = new();
        public ObservableCollection<UnmatchedLinkItem>    UnmatchedLinks  { get; } = new();

        // ── State ──────────────────────────────────────────────────────────

        [ObservableProperty]
        private bool _isAnalysing = false;

        [ObservableProperty]
        private string _statusText = "Ready. Click Analyse to scan linked files.";

        // ── Summary properties (live) ──────────────────────────────────────

        [ObservableProperty]
        private int _totalLinksFound;

        [ObservableProperty]
        private int _totalMatchingWorksetsFound;

        [ObservableProperty]
        private int _totalInstancesFound;

        [ObservableProperty]
        private int _totalInstancesAssigned;

        [ObservableProperty]
        private int _totalInstancesNotAssigned;

        [ObservableProperty]
        private int _totalSelectedForAction;

        [ObservableProperty]
        private int _totalExactMatches;

        [ObservableProperty]
        private int _totalActionableChecked;

        [ObservableProperty]
        private int _totalUnmatchedChecked;

        // ── Log ───────────────────────────────────────────────────────────

        [ObservableProperty]
        private string _logText = string.Empty;

        // ── Commands ──────────────────────────────────────────────────────

        [RelayCommand]
        private async Task AnalyseAsync()
        {
            IsAnalysing = true;
            StatusText  = "Analysing linked files…";

            ExactMatches.Clear();
            ActionableLinks.Clear();
            UnmatchedLinks.Clear();
            LogText = string.Empty;

            try
            {
                var result = await Task.Run(() => LinkScanner.Scan(_doc));

                foreach (var item in result.ExactMatches)
                    ExactMatches.Add(item);

                foreach (var item in result.ActionableLinks)
                {
                    item.PropertyChanged += OnItemCheckedChanged;
                    ActionableLinks.Add(item);
                }

                foreach (var item in result.UnmatchedLinks)
                {
                    item.PropertyChanged += OnItemCheckedChanged;
                    UnmatchedLinks.Add(item);
                }

                RefreshSummary();

                StatusText = $"Analysis complete — " +
                             $"{result.ExactMatches.Count} exact, " +
                             $"{result.ActionableLinks.Count} actionable, " +
                             $"{result.UnmatchedLinks.Count} unmatched.";

                AppendLog($"[{Ts}] Analyse complete. " +
                          $"Exact: {result.ExactMatches.Count}  " +
                          $"Actionable: {result.ActionableLinks.Count}  " +
                          $"Unmatched: {result.UnmatchedLinks.Count}");
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                AppendLog($"[{Ts}] ERROR: {ex.Message}");
                MessageBox.Show(ex.Message, "Analyse Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsAnalysing = false;
            }
        }

        // ── Grid 2 action — Reassign checked items ─────────────────────────

        [RelayCommand]
        private void ReassignChecked()
        {
            var toProcess = ActionableLinks.Where(i => i.IsChecked).ToList();
            if (toProcess.Count == 0)
            {
                MessageBox.Show("No items checked in Grid 2.",
                    "Reassign", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalReassigned = 0;
            int totalFailed     = 0;

            using var tx = new Transaction(_doc, "WM02 — Reassign Link Instances");
            tx.Start();
            try
            {
                foreach (var item in toProcess)
                {
                    int count = LinkScanner.ReassignInstances(_doc, item);
                    totalReassigned += count;
                    AppendLog($"[{Ts}] Reassigned {count} instance(s) of " +
                              $"\"{item.LinkedFileName}\" → \"{item.MatchedWorkset}\"");
                }
                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.RollBack();
                totalFailed++;
                AppendLog($"[{Ts}] ROLLBACK — {ex.Message}");
                MessageBox.Show($"Reassignment failed and was rolled back:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusText = $"Reassigned {totalReassigned} instance(s) across {toProcess.Count} link(s).";
            MessageBox.Show(
                $"Done.\n{totalReassigned} instance(s) reassigned across {toProcess.Count} link(s).",
                "Reassign Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            // Re-run analysis to refresh all three grids
            _ = AnalyseAsync();
        }

        // ── Grid 3 action — Create workset + assign ────────────────────────

        [RelayCommand]
        private void CreateAndAssignChecked()
        {
            var toProcess = UnmatchedLinks.Where(i => i.IsChecked).ToList();
            if (toProcess.Count == 0)
            {
                MessageBox.Show("No items checked in Grid 3.",
                    "Create & Assign", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int totalCreated    = 0;
            int totalAssigned   = 0;

            using var tx = new Transaction(_doc, "WM02 — Create Link Worksets");
            tx.Start();
            try
            {
                foreach (var item in toProcess)
                {
                    var (created, assigned) = LinkScanner.CreateWorksetAndAssign(_doc, item);
                    if (created) totalCreated++;
                    totalAssigned += assigned;

                    AppendLog($"[{Ts}] \"{item.ProposedWorkset}\" — " +
                              (created ? "created" : "already existed") +
                              $", {assigned} instance(s) assigned.");
                }
                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.RollBack();
                AppendLog($"[{Ts}] ROLLBACK — {ex.Message}");
                MessageBox.Show($"Operation failed and was rolled back:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusText = $"Created {totalCreated} workset(s), assigned {totalAssigned} instance(s).";
            MessageBox.Show(
                $"Done.\n{totalCreated} workset(s) created.\n{totalAssigned} instance(s) assigned.",
                "Create & Assign Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            _ = AnalyseAsync();
        }

        // ── Grid 2 selection commands ──────────────────────────────────────

        [RelayCommand]
        private void SelectAllGrid2()
        {
            foreach (var item in ActionableLinks) item.IsChecked = true;
        }

        [RelayCommand]
        private void SelectNoneGrid2()
        {
            foreach (var item in ActionableLinks) item.IsChecked = false;
        }

        [RelayCommand]
        private void InvertGrid2()
        {
            foreach (var item in ActionableLinks) item.IsChecked = !item.IsChecked;
        }

        // ── Grid 3 selection commands ──────────────────────────────────────

        [RelayCommand]
        private void SelectAllGrid3()
        {
            foreach (var item in UnmatchedLinks) item.IsChecked = true;
        }

        [RelayCommand]
        private void SelectNoneGrid3()
        {
            foreach (var item in UnmatchedLinks) item.IsChecked = false;
        }

        [RelayCommand]
        private void InvertGrid3()
        {
            foreach (var item in UnmatchedLinks) item.IsChecked = !item.IsChecked;
        }

        // ── Summary refresh ────────────────────────────────────────────────

        private void OnItemCheckedChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LinkWorksetMatchItem.IsChecked) ||
                e.PropertyName == nameof(UnmatchedLinkItem.IsChecked))
                RefreshSummary();
        }

        private void RefreshSummary()
        {
            TotalLinksFound            = ExactMatches.Count + ActionableLinks.Count + UnmatchedLinks.Count;
            TotalMatchingWorksetsFound = ExactMatches.Count + ActionableLinks.Count;
            TotalInstancesFound        = ExactMatches.Sum(i => i.InstanceCount)
                                       + ActionableLinks.Sum(i => i.TotalInstances)
                                       + UnmatchedLinks.Sum(i => i.InstanceCount);
            TotalInstancesAssigned     = ExactMatches.Sum(i => i.InstanceCount)
                                       + ActionableLinks.Sum(i => i.InstancesOnCorrectWorkset);
            TotalInstancesNotAssigned  = ActionableLinks.Sum(i => i.InstancesOnWrongWorkset)
                                       + UnmatchedLinks.Sum(i => i.InstanceCount);
            TotalExactMatches          = ExactMatches.Count;
            TotalActionableChecked     = ActionableLinks.Count(i => i.IsChecked);
            TotalUnmatchedChecked      = UnmatchedLinks.Count(i => i.IsChecked);
            TotalSelectedForAction     = TotalActionableChecked + TotalUnmatchedChecked;
        }

        // ── Log helper ─────────────────────────────────────────────────────

        private void AppendLog(string line)
        {
            LogText = string.IsNullOrEmpty(LogText)
                ? line
                : LogText + "\n" + line;
        }

        private static string Ts =>
            DateTime.Now.ToString("HH:mm:ss");
    }
}
