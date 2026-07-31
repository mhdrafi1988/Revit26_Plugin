using System.Windows;
using System.Windows.Input;
using Revit26_Plugin.DtlLineDim.V008.UI.ViewModels;

namespace Revit26_Plugin.DtlLineDim.V008.UI.Views
{
    public partial class DtlLineDimWindow : Window
    {
        public DtlLineDimWindow()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDown;
            Closing += OnClosing;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Give the ViewModel a reference to this window so secondary dialogs
            // it raises (e.g. the export-folder picker) can be owner-parented
            // correctly instead of appearing ownerless. Fixed in V007.
            if (e.NewValue is DtlLineDimViewModel vm)
                vm.OwnerWindow = this;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is DtlLineDimViewModel vm)
                vm.PersistSelections();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
