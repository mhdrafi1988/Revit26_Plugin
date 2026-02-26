using Autodesk.Revit.DB;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class DetailItemPlacementResult
    {
        public ElementId DetailCurveId { get; set; }
        public ElementId FamilyInstanceId { get; set; }
        public bool PlacementSucceeded { get; set; }
        public string FailureReason { get; set; }
    }
}
