// File: RoofInfo.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Models
//
// Responsibility:
// - Lightweight data model representing a selected roof
// - Used by ViewModels and summary reporting
// - NO Revit API references

namespace Revit26_Plugin.RoofRidgeLines_V06.Models
{
    /// <summary>
    /// Data container describing a roof selection.
    /// </summary>
    public class RoofInfo
    {
        /// <summary>
        /// Revit ElementId value as integer (stored safely without API dependency).
        /// </summary>
        public int RoofElementId { get; }

        /// <summary>
        /// Display name of the roof.
        /// </summary>
        public string RoofName { get; }

        /// <summary>
        /// Indicates whether the roof supports shape editing.
        /// </summary>
        public bool IsShapeEditable { get; }

        public RoofInfo(int roofElementId, string roofName, bool isShapeEditable)
        {
            RoofElementId = roofElementId;
            RoofName = roofName;
            IsShapeEditable = isShapeEditable;
        }
    }
}
