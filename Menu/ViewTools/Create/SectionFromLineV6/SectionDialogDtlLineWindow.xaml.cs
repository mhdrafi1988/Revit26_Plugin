using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Interop;
using Revit22_Plugin.PlanSections.ViewModels;

namespace Revit22_Plugin.PlanSections.Views
{
    public partial class SectionDialogDtlLineWindow : Window
    {
        public SectionDialogDtlLineWindow(UIDocument uidoc, UIApplication uiapp)
        {
            InitializeComponent();

            // attach ViewModel
            DataContext = new SectionDialogDtlLineViewModel(uidoc.Document);

            // Attach to Revit main window
            IntPtr hwnd = uiapp.MainWindowHandle;
            new WindowInteropHelper(this) { Owner = hwnd };

            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
