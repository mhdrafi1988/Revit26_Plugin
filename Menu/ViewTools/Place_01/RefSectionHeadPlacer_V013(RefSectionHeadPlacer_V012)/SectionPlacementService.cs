using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RefSectionHeadPlacer.V013.Core.Services
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
    /// another view". IMPORTANT: this API creates NO new view and has always
    /// returned void. What it creates is a section MARKER — an annotation
    /// instance in category OST_Viewers placed in the parent view. Detection
    /// therefore diffs OST_Viewers instance ids before/after the call, never
    /// ViewSection ids (a ViewSection diff always comes back empty and
    /// previously caused a false failure → SubTransaction rollback that
    /// deleted the freshly created marker).
    ///
    /// The user's chosen "Section type" (ViewFamilyType) is applied to the
    /// marker afterwards via ChangeTypeId, guarded by IsValidType. Must run
    /// inside an open host Transaction/SubTransaction; this method does not
    /// manage its own transaction.
    /// </summary>
    public class SectionPlacementService
    {
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
        /// <param name="tailLengthFeet">Head-&gt;tail distance, Revit internal units (feet).
        /// User-entered in mm via the UI (default 2000 mm) and converted before this call.</param>
        public SectionPlacementResult PlaceReferenceSection(
            ElementId parentViewId, XYZ hostOrigin, XYZ hostDirection,
            ElementId draftingViewId, ElementId sectionTypeId, double tailLengthFeet)
        {
            try
            {
                XYZ head = hostOrigin;
                XYZ tail = hostOrigin + hostDirection.Normalize() * tailLengthFeet;

                HashSet<ElementId> before = GetViewerInstanceIds();

                // Void by design — creates a section marker (OST_Viewers), not a view.
                ViewSection.CreateReferenceSection(
                    _hostDoc, parentViewId, draftingViewId, head, tail);

                ElementId newId = GetViewerInstanceIds()
                    .FirstOrDefault(id => !before.Contains(id));

                if (newId == null || newId == ElementId.InvalidElementId)
                {
                    return new SectionPlacementResult
                    {
                        Success = false,
                        Message = "Reference section marker not found after creation (OST_Viewers id diff returned none)."
                    };
                }

                // Apply the user's chosen section type to the marker, if provided/valid.
                Element marker = _hostDoc.GetElement(newId);
                if (sectionTypeId != null && sectionTypeId != ElementId.InvalidElementId &&
                    marker != null && marker.IsValidType(sectionTypeId))
                {
                    marker.ChangeTypeId(sectionTypeId);
                }

                return new SectionPlacementResult { Success = true, CreatedSectionId = newId };
            }
            catch (System.Exception ex)
            {
                return new SectionPlacementResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>Instance ids in OST_Viewers — the category reference section markers land in.</summary>
        private HashSet<ElementId> GetViewerInstanceIds()
            => new HashSet<ElementId>(
                new FilteredElementCollector(_hostDoc)
                    .OfCategory(BuiltInCategory.OST_Viewers)
                    .WhereElementIsNotElementType()
                    .ToElementIds());
    }
}
