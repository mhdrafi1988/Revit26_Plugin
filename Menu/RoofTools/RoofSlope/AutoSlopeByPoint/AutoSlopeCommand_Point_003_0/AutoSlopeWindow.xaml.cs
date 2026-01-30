using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.ViewModels;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;   // WPF color namespace

namespace Revit26_Plugin.AutoSlopeByPoint.Views
{
    public partial class AutoSlopeWindow : Window
    {
        // FIXED: Constructor takes ONLY 4 arguments
        public AutoSlopeWindow(UIDocument uidoc, UIApplication app, ElementId roofId,
                               List<XYZ> drains)
        {
            InitializeComponent();

            // Pass AddColoredLog automatically
            DataContext = new AutoSlopeViewModel(uidoc, app, roofId, drains, AddColoredLog);
        }

        // ===============================
        // REAL COLORED LOGGING
        // ===============================
        private void AddColoredLog(string text)
        {
            Dispatcher.Invoke(() =>
            {
                Paragraph p = new Paragraph();
                Run run = new Run(StripTags(text));

                // FIX: Force WPF Color class explicitly
                run.Foreground = ParseColor(text);

                p.Inlines.Add(run);
                LogBox.Document.Blocks.Add(p);
                LogBox.ScrollToEnd();
            });
        }

        // Remove markup tags like <color=#FF00FF>
        private string StripTags(string input)
        {
            return Regex.Replace(input, "<.*?>", "");
        }

        private SolidColorBrush ParseColor(string input)
        {
            var match = Regex.Match(input, @"<color=?(#[0-9A-Fa-f]{6})?>");

            if (match.Success && match.Groups.Count > 1)
            {
                try
                {
                    // FIX: fully qualify WPF Color to avoid clash with Revit.Color
                    System.Windows.Media.Color c =
                        (System.Windows.Media.Color)ColorConverter.ConvertFromString(match.Groups[1].Value);

                    return new SolidColorBrush(c);
                }
                catch { }
            }

            return new SolidColorBrush(System.Windows.Media.Colors.White);
        }

        private void LogBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            LogBox.ScrollToEnd();
        }

        private void LogBox_TextChanged_1(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}
