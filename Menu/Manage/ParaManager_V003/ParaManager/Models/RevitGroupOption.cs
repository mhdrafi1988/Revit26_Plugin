using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.ParaManager.V003.Models
{
    /// <summary>
    /// One Properties-palette parameter group a shared parameter can be bound under
    /// (Revit 2026 uses ForgeTypeId-based GroupTypeId.* values, not BuiltInParameterGroup).
    /// The grid's "Revit Group" column and the bulk "Apply to Selected" control both
    /// pick from <see cref="All"/>.
    /// </summary>
    public class RevitGroupOption
    {
        public string Name { get; }
        public ForgeTypeId GroupTypeId { get; }

        private RevitGroupOption(string name, ForgeTypeId groupTypeId)
        {
            Name = name;
            GroupTypeId = groupTypeId;
        }

        public override string ToString() => Name;

        public static IReadOnlyList<RevitGroupOption> All { get; } = new List<RevitGroupOption>
        {
            new("Data", Autodesk.Revit.DB.GroupTypeId.Data),
            new("Identity Data", Autodesk.Revit.DB.GroupTypeId.IdentityData),
            new("Text", Autodesk.Revit.DB.GroupTypeId.Text),
            new("Dimensions", Autodesk.Revit.DB.GroupTypeId.Geometry),
            new("General", Autodesk.Revit.DB.GroupTypeId.General),
            new("Graphics", Autodesk.Revit.DB.GroupTypeId.Graphics),
            new("Materials and Finishes", Autodesk.Revit.DB.GroupTypeId.Materials),
            new("Construction", Autodesk.Revit.DB.GroupTypeId.Construction),
            new("Constraints", Autodesk.Revit.DB.GroupTypeId.Constraints),
            new("Phasing", Autodesk.Revit.DB.GroupTypeId.Phasing),
            new("Structural", Autodesk.Revit.DB.GroupTypeId.Structural),
            new("Structural Analysis", Autodesk.Revit.DB.GroupTypeId.StructuralAnalysis),
            new("Analysis Results", Autodesk.Revit.DB.GroupTypeId.AnalysisResults),
            new("Mechanical", Autodesk.Revit.DB.GroupTypeId.Mechanical),
            new("Electrical", Autodesk.Revit.DB.GroupTypeId.Electrical),
            new("Plumbing", Autodesk.Revit.DB.GroupTypeId.Plumbing),
            new("Energy Analysis", Autodesk.Revit.DB.GroupTypeId.EnergyAnalysis),
            new("Green Building Properties", Autodesk.Revit.DB.GroupTypeId.GreenBuilding),
            new("IFC Parameters", Autodesk.Revit.DB.GroupTypeId.Ifc),
        };

        public static RevitGroupOption Default => All[0];

        /// <summary>
        /// Auto-map a shared-parameter-file group name (e.g. "Dimensions", "AutoSlope") to
        /// the closest Revit group. Unknown names fall back to Data. This is only the
        /// starting value — the user can override it per row in the UI.
        /// </summary>
        public static RevitGroupOption FromFileGroupName(string fileGroupName)
        {
            var key = fileGroupName?.Trim();
            return All.FirstOrDefault(o => string.Equals(o.Name, key, System.StringComparison.OrdinalIgnoreCase))
                   ?? Default;
        }
    }
}
