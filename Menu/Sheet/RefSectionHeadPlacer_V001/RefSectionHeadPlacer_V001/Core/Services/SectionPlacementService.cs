using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Services
{
    public class SectionPlacementResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ElementId CreatedSectionId { get; set; }
    }

    /// <summary>
    /// Creates a REFERENCE section head in the active host plan view that points
    /// at the mapped drafting view, without cutting new geometry.
    ///
    /// Uses ViewSection.CreateReferenceSection — the correct API for "reference
    /// another view". That API does not accept a section ViewFamilyType, so the
    /// user's chosen "Section type" is applied afterwards via ChangeTypeId (this
    /// is what keeps the Section type dropdown meaningful). Must run inside an
    /// open host Transaction; this method does not manage its own transaction.
    ///
    /// REVIT 2026 API CHANGE: ViewSection.CreateReferenceSection now returns
    /// void (it returned ElementId in 2020–2025). The new section's id is no
    /// longer handed back, so it is resolved by diffing the set of ViewSection
    /// ElementIds in the document before and after the call.
    /// </summary>
    public class SectionPlacementService
    {
        private const double TailLength = 3.0; // ft — head->tail sets marker direction

        private readonly Document _hostDoc;

        public SectionPlacementService(Document hostDoc)
        {
            _hostDoc = hostDoc;
        }

        /// <param name="parentViewId">Active host plan view the head is drawn in.</param>
        /// <param name="hostOrigin">Head point, HOST coordinates.</param>
        /// <param name="hostDirection">Marker direction, HOST coordinates.</param>
        /// <param name="draftingViewId">Drafting view to reference.</param>
        /// <param name="sectionTypeId">Chosen section ViewFamilyType (may be Invalid to keep default).</param>
        public SectionPlacementResult PlaceReferenceSection(
            ElementId parentViewId, XYZ hostOrigin, XYZ hostDirection,
            ElementId draftingViewId, ElementId sectionTypeId)
        {
            try
            {
                XYZ head = hostOrigin;
                XYZ tail = hostOrigin + hostDirection.Normalize() * TailLength;

                HashSet<ElementId> before = GetAllViewSectionIds();

                // Void in Revit 2026 — no return value to capture.
                ViewSection.CreateReferenceSection(
                    _hostDoc, parentViewId, draftingViewId, head, tail);

                ElementId newId = GetAllViewSectionIds()
                    .FirstOrDefault(id => !before.Contains(id));

                if (newId == null || newId == ElementId.InvalidElementId)
                {
                    return new SectionPlacementResult
                    {
                        Success = false,
                        Message = "Reference section was not found after creation (id diff returned none)."
                    };
                }

                // Apply the user's chosen section type to the head, if provided/valid.
                if (sectionTypeId != null && sectionTypeId != ElementId.InvalidElementId &&
                    _hostDoc.GetElement(newId) is ViewSection section &&
                    section.IsValidType(sectionTypeId))
                {
                    section.ChangeTypeId(sectionTypeId);
                }

                return new SectionPlacementResult { Success = true, CreatedSectionId = newId };
            }
            catch (System.Exception ex)
            {
                return new SectionPlacementResult { Success = false, Message = ex.Message };
            }
        }

        private HashSet<ElementId> GetAllViewSectionIds()
            => new HashSet<ElementId>(
                new FilteredElementCollector(_hostDoc)
                    .OfClass(typeof(ViewSection))
                    .ToElementIds());
    }
}
