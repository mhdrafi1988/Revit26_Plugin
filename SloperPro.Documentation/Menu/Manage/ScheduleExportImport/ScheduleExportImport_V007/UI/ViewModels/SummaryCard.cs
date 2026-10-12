namespace Revit26_Plugin.ScheduleExportImport.V007.UI.ViewModels
{
    public enum CardTone { Neutral, Accent, Success, Warning, Danger }

    /// <summary>One metric tile (big number + label) in a summary card row.</summary>
    public class SummaryCard
    {
        public SummaryCard(string label, int value, CardTone tone = CardTone.Neutral, string hint = "")
        {
            Label = label;
            Value = value.ToString("N0");
            Tone = tone;
            Hint = hint;
        }

        public string Label { get; }
        public string Value { get; }
        public CardTone Tone { get; }
        public string Hint { get; }
    }
}
