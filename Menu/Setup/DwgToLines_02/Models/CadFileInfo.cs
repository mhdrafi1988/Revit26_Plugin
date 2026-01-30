using Autodesk.Revit.DB;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Models
{
    /// <summary>
    /// Lightweight metadata model representing a CAD file.
    /// This model is safe for ViewModels and Services.
    /// </summary>
    public class CadFileInfo
    {
        /// <summary>
        /// Revit ElementId of the ImportInstance.
        /// </summary>
        public ElementId ElementId { get; set; }

        /// <summary>
        /// File name / display name of the CAD.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Import type (Imported / Linked).
        /// </summary>
        public string ImportType { get; set; }

        /// <summary>
        /// Optional reference to the ImportInstance.
        /// Can be null if not resolved yet.
        /// </summary>
        public ImportInstance ImportInstance { get; set; }
    }
}
