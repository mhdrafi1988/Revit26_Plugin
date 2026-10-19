using System.Windows.Controls;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.CombinedRoofTools.V002.UI.Views.Tabs
{
    public partial class CreaserAdvTabView : UserControl
    {
        public CreaserAdvTabView()
        {
            InitializeComponent();
        }

        private void CopySelectedLog_Click(object sender, System.Windows.RoutedEventArgs e) => LogClipboardService.CopySelected(LogList.SelectedItems);
    }
}
