using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models
{
    /// <summary>
    /// One row of the Mapping Grid (Section 3): a single Category+Family+Type from a
    /// single linked model instance, with its Representation, Detail Line Style, and
    /// Color Override. Created automatically when a Type checkbox is checked in
    /// Section 2's tree, removed when unchecked.
    ///
    /// NOTE (Phase 1 scope): this model carries only the configuration data needed for
    /// the UI. DetailLineStyle/Color population from the host project, and the actual
    /// Revit write pipeline (GeometryExtractionService etc.), are Phase 2+.
    /// </summary>
    public partial class ElementMapping : ObservableObject
    {
        /// <summary>Stable identifier for this mapping row (Guid), also used as the
        /// Mapping Id stored in ExtensibleStorage metadata on generated Detail Lines.</summary>
        public Guid MappingId { get; } = Guid.NewGuid();

        public long LinkInstanceId { get; set; }
        public string LinkDisplayName { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public long TypeId { get; set; }

        public RepresentationGroup Group { get; set; }

        /// <summary>Display-only helper for the Mapping Grid's Extraction Method
        /// column, which only applies to (and is only shown for) Profile-group rows.</summary>
        public bool IsProfileGroup => Group == RepresentationGroup.Profile;

        /// <summary>Display-only helper for the Mapping Grid's Representation column —
        /// Point-group rows get an editable Circle/Rectangle ComboBox; Profile/Linear
        /// rows show their fixed Boundary/Centerline value as read-only text.</summary>
        public bool IsPointGroup => Group == RepresentationGroup.Point;

        [ObservableProperty]
        private RepresentationMode _representation;

        /// <summary>Profile-group only (Floor/Roof) — Solid Geometry (top-face) vs
        /// Profile-Based (sketch curves) boundary extraction. No effect on Linear/
        /// Point mappings. See ProfileExtractionMethod.</summary>
        [ObservableProperty]
        private ProfileExtractionMethod _extractionMethod = ProfileExtractionMethod.SolidGeometry;

        /// <summary>Selected Detail Line Style name (populated from host project's
        /// available line styles — Phase 2 DetailLineStyleService).</summary>
        [ObservableProperty]
        private string _detailLineStyleName = string.Empty;

        /// <summary>Optional per-mapping color override (view-specific graphics override
        /// via View.SetElementOverrides — never touches the shared GraphicsStyle color).</summary>
        [ObservableProperty]
        private string _colorName = "None";

        [ObservableProperty]
        private bool _isEnabled = true;
    }
}
