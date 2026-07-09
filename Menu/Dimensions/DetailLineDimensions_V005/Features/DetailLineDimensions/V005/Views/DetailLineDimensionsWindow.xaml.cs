using System.Linq;
using System.Windows;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DetailLIneDimensions.V005.Views
{
    public partial class DetailLineDimensionsWindow : Window
    {
        public DetailLineDimensionsWindow()
        {
            InitializeComponent();
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Join(
                System.Environment.NewLine,
                LogListBox.SelectedItems.Cast<LogEntry>().Select(entry => entry.ToString()));

            if (!string.IsNullOrEmpty(text))
                Clipboard.SetText(text);
        }
    }
}
