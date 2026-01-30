// ==============================================
// File: CadFileInfo.cs
// Layer: Models
// ==============================================

using Autodesk.Revit.DB;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Models
{
    /// <summary>
    /// Lightweight metadata describing a CAD import.
    /// </summary>
    public class CadFileInfo
    {
        /// <summary>
        /// Revit ElementId of the ImportInstance.
        /// </summary>
        public ElementId ElementId { get; set; }

        /// <summary>
        /// Display name of the CAD file.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Import type (Import / Link).
        /// </summary>
        public string ImportType { get; set; }

        /// <summary>
        /// Optional reference to the ImportInstance.
        /// </summary>
        public ImportInstance ImportInstance { get; set; }
    }
}
