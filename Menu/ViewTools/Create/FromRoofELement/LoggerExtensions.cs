using System.Windows.Documents;
using System.Windows.Media;

namespace Revit22_Plugin.AutoRoofSections.Utils
{
    public static class LoggerExtensions
    {
        public static void AppendLogLine(this FlowDocument doc, string text)
        {
            Paragraph p = new Paragraph(new Run(text));
            p.Margin = new System.Windows.Thickness(0, 0, 0, 4);

            // Optional clean Metro visual
            p.Foreground = Brushes.Black;
            p.FontSize = 12;

            doc.Blocks.Add(p);
        }
    }
}
