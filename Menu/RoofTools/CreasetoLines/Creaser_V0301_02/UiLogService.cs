using System.Text;

namespace Revit26_Plugin.Creaser_V32.Helpers
{
    public class UiLogService
    {
        private readonly StringBuilder _sb = new();

        public string FullText => _sb.ToString();

        public void Write(string message)
        {
            _sb.AppendLine(message);
        }
    }
}
