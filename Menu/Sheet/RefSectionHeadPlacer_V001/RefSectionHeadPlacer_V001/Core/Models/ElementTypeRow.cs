using System.Collections.Generic;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Models
{
    /// <summary>
    /// Bindable row for Grid 2 — one distinct (SourceLabel, Bic, TypeName)
    /// grouping. SourceLabel is "Host" for roofs, or the link instance's name for
    /// doors/plumbing/walls — INCLUDED in the identity on purpose: two different
    /// linked files can each contain a type named e.g. "Floor Drain 100mm" and
    /// these must NOT merge (confirmed with Rafi — always kept separate per link).
    ///
    /// Bic (BuiltInCategory) is the STABLE category identity used for routing in
    /// SectionOriginService and the engine. Category (the display name) is only for
    /// the UI — matching on the display name is fragile because Revit localizes it,
    /// so all logic keys off Bic instead.
    /// </summary>
    public partial class ElementTypeRow : ObservableObject
    {
        [ObservableProperty]
        private bool isSelected;

        /// <summary>"Host" for roofs, or the originating link instance's name.</summary>
        public string SourceLabel { get; }

        /// <summary>Display-only Revit category name (localized).</summary>
        public string Category { get; }

        /// <summary>Stable category identity — used for all routing/matching.</summary>
        public BuiltInCategory Bic { get; }

        public string TypeName { get; }
        public int Count => ElementIds.Count;
        public IReadOnlyList<ElementId> ElementIds { get; }

        /// <summary>Document that OWNS these ElementIds (host doc or a specific link doc).</summary>
        public Document SourceDocument { get; }

        /// <summary>Transform from SourceDocument space to HOST space.
        /// Identity for host-owned elements; that link's GetTotalTransform() otherwise.</summary>
        public Transform LinkTransform { get; }

        public bool IsFromLink { get; }

        public ElementTypeRow(
            string sourceLabel,
            string category,
            string typeName,
            BuiltInCategory bic,
            IReadOnlyList<ElementId> elementIds,
            Document sourceDocument,
            Transform linkTransform,
            bool isFromLink)
        {
            SourceLabel = sourceLabel;
            Category = category;
            TypeName = typeName;
            Bic = bic;
            ElementIds = elementIds;
            SourceDocument = sourceDocument;
            LinkTransform = linkTransform;
            IsFromLink = isFromLink;
        }
    }
}
