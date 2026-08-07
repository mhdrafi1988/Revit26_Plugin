using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreateSectionsFromDetailLines.V009.ViewModels;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V009.Views.SectionFromLineDialog
{
    /// <summary>
    /// Interaction logic for SectionFromLineDialog.xaml. View wiring only.
    ///
    /// V008 changes from V07:
    /// - Dialog no longer no-ops on CreateRequested; the Command now wires
    ///   CreateRequested directly to the ExternalEvent's Raise() (see
    ///   CreateSectionsFromDetailLinesCommand).
    /// - Added accordion toggle for the Live Log panel, Copy All/Copy
    ///   Selected/Clear handlers, and settings save on close.
    ///
    /// V009 changes from V008:
    /// - Added the same accordion toggle pattern for the new View Crop
    ///   card. Per confirmed spec, expanded/collapsed state is NOT
    ///   persisted to settings.json — always starts collapsed.
    /// - Added PreviewRowCheckBox_PreviewMouseLeftButtonDown, per suite
    ///   DATAGRID SPEC rule 2 — blocks the checkbox click from also
    ///   triggering DataGridRow's native row-select cascade.
    /// </summary>
    public partial class SectionFromLineDialog : Window
    {
        public SectionFromLineViewModel ViewModel { get; }

        private bool _logExpanded = false;
        private bool _viewCropExpanded = false;

        public SectionFromLineDialog(UIDocument uiDoc, UIApplication uiApp)
        {
            InitializeComponent();

            ViewModel = new SectionFromLineViewModel(uiDoc.Document);
            DataContext = ViewModel;

            // Auto-scroll to the bottom when a new log entry is added
            ViewModel.LiveLog.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    LogListBox.ScrollIntoView(ViewModel.LiveLog.LastOrDefault());
                }
            };

            ViewModel.CloseRequested += OnCloseRequested;
            Closing += (_, __) => ViewModel.SaveSettings();
        }

        private void OnCloseRequested() => Close();

        // ================= LIVE LOG ACCORDION =================

        private void LogHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _logExpanded = !_logExpanded;
            LogListBox.Visibility = _logExpanded ? Visibility.Visible : Visibility.Collapsed;
            LogChevron.Text = _logExpanded ? "▼" : "▶";
        }

        // ================= VIEW CROP ACCORDION (V009 — new) =================

        private void ViewCropHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _viewCropExpanded = !_viewCropExpanded;
            ViewCropBody.Visibility = _viewCropExpanded ? Visibility.Visible : Visibility.Collapsed;
            ViewCropChevron.Text = _viewCropExpanded ? "▼" : "▶";
        }

        // ================= LOG ACTIONS =================

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var entry in ViewModel.LiveLog)
                sb.AppendLine(entry.ToString());

            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString());
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = LogListBox.SelectedItems.Cast<object>().ToList();
            if (selected.Count == 0)
                return;

            var sb = new StringBuilder();
            foreach (var item in selected)
                sb.AppendLine(item.ToString());

            Clipboard.SetText(sb.ToString());
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.LiveLog.Clear();
        }

        // ================= SECTIONS TO CREATE GRID (V009 — new) =================

        /// <summary>
        /// Per suite DATAGRID SPEC rule 2: blocks the checkbox click from
        /// also triggering the native DataGridRow selection cascade — the
        /// row's checkbox binding (IsIncluded) is the only thing that
        /// should react to this click.
        /// </summary>
        private void PreviewRowCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false; // let the CheckBox itself toggle...
            if (sender is System.Windows.Controls.CheckBox cb)
            {
                cb.IsChecked = !(cb.IsChecked ?? false);
                e.Handled = true; // ...but stop it from bubbling into row selection.
            }
        }
    }
}