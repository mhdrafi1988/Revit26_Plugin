namespace Revit26_Plugin.Creaser_V08.Commands.ViewModels
{
    public class CreaserSummaryViewModel
    {
        public string SummaryText { get; }

        public CreaserSummaryViewModel(string summaryText)
        {
            SummaryText = summaryText;
        }
    }
}
