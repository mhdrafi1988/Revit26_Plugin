using System.Globalization;
using System.Windows;
using Revit26_Plugin.Creaser_V08.Commands.Models;

namespace Revit26_Plugin.Creaser_V08.Commands.UI
{
    public partial class CreaserInputWindow : Window
    {
        public CreaserInput Input { get; private set; }

        public CreaserInputWindow()
        {
            InitializeComponent();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(RadiusBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double radius))
                return;

            if (!double.TryParse(ToleranceBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double tolerance))
                return;

            Input = new CreaserInput(radius, tolerance);
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
