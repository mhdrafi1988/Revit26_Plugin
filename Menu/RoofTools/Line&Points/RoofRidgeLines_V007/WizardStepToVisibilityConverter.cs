using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Converters
{
    public class WizardStepToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int currentStep && parameter is string stepParam)
            {
                if (int.TryParse(stepParam, out int targetStep))
                {
                    return currentStep == targetStep ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}