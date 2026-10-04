using Autodesk.Revit.UI;
using Revit26_Plugin.BubbleAutoRenumber.V007.Handlers;
using Revit26_Plugin.BubbleAutoRenumber.V007.ViewModels;
using System.Windows;
using System.Windows.Interop;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.BubbleAutoRenumber.V007.Views
{
    public partial class SectionAutoRenumberWindow : Window
    {
        public SectionAutoRenumberWindow(
            UIDocument                 uidoc,
            UIApplication              uiapp,
            SectionAutoRenumberHandler handler,
            ExternalEvent              externalEvent)
        {
            InitializeComponent();

            DataContext = new SectionAutoRenumberViewModel(uidoc, handler, externalEvent);

            new WindowInteropHelper(this)
            {
                Owner = uiapp.MainWindowHandle
            };
        }

        private void CopyAllLog_Click(object sender, RoutedEventArgs e) => LogClipboardService.CopyAll(new[] { LogBox.Text });

        private void CopySelectedLog_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(LogBox.SelectedText))
                LogClipboardService.CopyAll(new[] { LogBox.SelectedText });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
