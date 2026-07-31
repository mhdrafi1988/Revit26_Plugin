using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RefSectionHeadPlacer.V012.Core.Models;
using Revit26_Plugin.RefSectionHeadPlacer.V012.UI.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RefSectionHeadPlacer.V012.UI.Views
{
    public partial class RefSectionHeadPlacerWindow : Window
    {
        private readonly RefSectionHeadPlacerViewModel _viewModel;

        public RefSectionHeadPlacerWindow(UIApplication uiApp, Document doc)
        {
            InitializeComponent();

            _viewModel = new RefSectionHeadPlacerViewModel(doc);
            DataContext = _viewModel;

            // Window ownership via WindowInteropHelper — never Application.Current
            // .MainWindow in a Revit add-in.
            new WindowInteropHelper(this).Owner = uiApp.MainWindowHandle;

            // ViewModel raises CloseRequested (it has no window reference itself).
            _viewModel.CloseRequested += Close;

            // MEMORY: dispose the ViewModel when the window closes so it unsubscribes
            // from the long-lived ExternalEvent handler and disposes the event.
            Closed += (s, e) =>
            {
                _viewModel.CloseRequested -= Close;
                _viewModel.Dispose();
            };
        }

        /// <summary>
        /// Copy Selected — code-behind per convention: reads the log grid's
        /// SelectedItems directly rather than routing through the ViewModel.
        /// </summary>
        private void OnCopySelectedLogsClick(object sender, RoutedEventArgs e)
        {
            var text = string.Join(System.Environment.NewLine,
                GridLog.SelectedItems.Cast<LogEntry>().Select(entry => entry.ToString()));
            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }

        /// <summary>
        /// V010: fires when the user picks a drafting view from a Grid 3 row's
        /// ComboBox drop-down. Unlike the V008 popover, the ComboBox sits directly
        /// in the DataGrid cell's visual/logical tree — its DataContext is simply
        /// the CategoryMappingRow for that row, so no Popup/logical-tree lookup is
        /// needed here (that workaround existed only because a Popup's content
        /// renders in a separate visual root; a ComboBox's drop-down does not have
        /// that problem for DataContext inheritance purposes).
        ///
        /// GUARD (V010 review): FilteredDraftingViews (the ComboBox's ItemsSource)
        /// is cleared and rebuilt on every keystroke (CategoryMappingRow.ApplyFilter).
        /// Some WPF versions raise a spurious SelectionChanged with SelectedItem
        /// momentarily null during that Clear()/re-Add() sequence, not just from an
        /// actual user click. The pattern match below already no-ops on a null/
        /// non-DraftingViewOption SelectedItem, so a mapping is only ever committed
        /// on a genuine item pick — never on a filter-driven collection reset.
        /// </summary>
        private void MappingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not System.Windows.Controls.ComboBox { SelectedItem: DraftingViewOption option, DataContext: CategoryMappingRow row })
                return; // null/none selected (e.g. filter reset mid-type) — ignore, no mapping change

            row.SelectDraftingView(option);
        }

        /// <summary>
        /// V012 FIX: typing now auto-opens the drop-down. StaysOpenOnEdit only
        /// keeps an ALREADY-open drop-down open — in V011, typing into a closed
        /// box filtered invisibly and the control felt dead until the arrow was
        /// clicked. TextChanged bubbles up from the ComboBox's internal editable
        /// TextBox (attached-event handler wired in XAML). Guards:
        ///   - IsKeyboardFocusWithin → only USER typing opens it, not programmatic
        ///     SearchText writes (restore-from-session, revert-on-dismiss).
        ///   - !IsDropDownOpen → a pick (SelectionChanged sets SearchText while
        ///     the drop-down is still open) doesn't re-open it as it closes.
        /// </summary>
        private void MappingCombo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox combo &&
                combo.IsKeyboardFocusWithin && !combo.IsDropDownOpen)
                combo.IsDropDownOpen = true;
        }

        /// <summary>
        /// V012 FIX: opening the drop-down always shows the FULL list (the box
        /// usually displays the current pick's name, which would otherwise
        /// substring-narrow the list to that one item — the removed V010
        /// "isShowingCurrentPickUnedited" hack patched exactly this).
        ///
        /// PERF FIX (V012 review, Rafi-reported "every click delayed"): the row
        /// now keeps FilteredDraftingViews at the FULL list between visits (see
        /// CategoryMappingRow.SelectDraftingView/RevertSearchTextToPick/
        /// SetAvailableDraftingViews) rather than narrowed to the picked name, so
        /// ShowFullList() here is a no-op most clicks (ReplaceFiltered's
        /// SequenceEqual guard skips the rebuild). Previously every pick narrowed
        /// the list to 1 item, so EVERY later click rebuilt the full list from
        /// scratch — that rebuild was the delay.
        ///
        /// Also only reassigns SelectedItem when it's not already correct —
        /// re-setting an already-correct SelectedItem still makes WPF redo
        /// selection-sync/highlight work on every open for no reason.
        /// </summary>
        private void MappingCombo_DropDownOpened(object sender, System.EventArgs e)
        {
            if (sender is not System.Windows.Controls.ComboBox { DataContext: CategoryMappingRow row } combo)
                return;

            row.ShowFullList();
            if (row.MappedDraftingView != null && !row.MappedDraftingView.Equals(combo.SelectedItem))
                combo.SelectedItem = row.MappedDraftingView; // DraftingViewOption equality is by view ElementId
        }

        /// <summary>
        /// V012 FIX: dismissal without a pick reverts stray typed text back to
        /// the mapped view's name — in V011, typing "xyz" and clicking away left
        /// "xyz" displayed while the mapping was unchanged, so the row looked
        /// remapped. Wired to BOTH DropDownClosed and LostKeyboardFocus: closing
        /// via Esc/click-away doesn't always move keyboard focus, and tabbing out
        /// doesn't always close a drop-down. After a genuine pick this is a no-op
        /// (SearchText already equals the picked name).
        /// </summary>
        private void MappingCombo_Dismissed(object sender, System.EventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox { DataContext: CategoryMappingRow row })
                row.RevertSearchTextToPick();
        }

        private void MappingCombo_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            // Focus moving INTO the drop-down popup still counts as "within" —
            // only revert when keyboard focus has fully left the ComboBox.
            if (sender is System.Windows.Controls.ComboBox { IsKeyboardFocusWithin: false, DataContext: CategoryMappingRow row })
                row.RevertSearchTextToPick();
        }
    }
}
