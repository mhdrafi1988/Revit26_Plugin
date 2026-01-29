using System.Windows;
using Autodesk.Revit.UI;
using Revit26_Plugin.CSFL_V07.ViewModels;

namespace Revit26_Plugin.CSFL_V07.Views.SectionFromLineDialog
{
    /// <summary>
    /// Interaction logic for SectionFromLineDialog.xaml
    /// View wiring only.
    /// </summary>
    public partial class SectionFromLineDialog : Window
    {
        public SectionFromLineViewModel ViewModel { get; }

        public SectionFromLineDialog(
            UIDocument uiDoc,
            UIApplication uiApp)
        {
            InitializeComponent();

            ViewModel = new SectionFromLineViewModel(uiDoc.Document);
            DataContext = ViewModel;

            // IMPORTANT:
            // Create does NOT close the dialog anymore
            ViewModel.CreateRequested += OnCreateRequested;

            // Cancel closes the dialog
            ViewModel.CloseRequested += OnCloseRequested;
        }

        private void OnCreateRequested()
        {
            // Do NOTHING here.
            // ExternalCommand will read the ViewModel
            // and run orchestration while UI stays open.
        }

        private void OnCloseRequested()
        {
            DialogResult = false;
            Close();
        }
    }
}
