using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreateSectionsFromDetailLines.V008.ViewModels;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V008.Views.SectionFromLineDialog
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
    /// </summary>
    public partial class SectionFromLineDialog : Window
    {
        public SectionFromLineViewModel ViewModel { get; }

        private bool _logExpanded = false;

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
    }
}