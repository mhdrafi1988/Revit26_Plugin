namespace Revit26_Plugin.AutoLiner_V02.Models
{
    public class ExecutionSummary
    {
        public int Created { get; set; }
        public int Failed { get; set; }
        public int Corners { get; set; }
        public int Drains { get; set; }

        public override string ToString()
        {
            return
                $"Corners Found : {Corners}\n" +
                $"Drain Points  : {Drains}\n" +
                $"Created       : {Created}\n" +
                $"Failed        : {Failed}";
        }
    }
}
