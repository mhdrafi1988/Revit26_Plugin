using System.Windows;
using System.Windows.Controls;

namespace Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Views
{
    public partial class RoofLoopAnalyzerWindow : Window
    {
        public RoofLoopAnalyzerWindow()
        {
            InitializeComponent();
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogTextBox.ScrollToEnd();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
