using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_adv_V001.Models
{
    /// <summary>
    /// Result of placing a single detail item instance.
    /// </summary>
    public class DetailItemPlacementResult
    {
        public ElementId DetailCurveId { get; set; }
        public ElementId FamilyInstanceId { get; set; }
        public bool PlacementSucceeded { get; set; }
        public string FailureReason { get; set; }
    }
}
