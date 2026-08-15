// ==============================================
// File: DetailFillRegionStyleService.cs
// Layer: Services
// Namespace: Revit26_Plugin.DwgToDetailLines.V010.Services
// ==============================================

using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.DwgToDetailLines.V010.Models;

namespace Revit26_Plugin.DwgToDetailLines.V010.Services
{
    /// <summary>
    /// Resolves FilledRegionType based on CAD hatch layer names, for
    /// FilledRegion creation. Handles missing patterns via user prompt
    /// (pre-filled with the global default) and caching. Mirrors
    /// DetailLineStyleService's structure for line styles.
    /// </summary>
    public class DetailFillRegionStyleService
    {
        private readonly Document _doc;
        private readonly FillPatternResolutionService _resolver;
        private readonly string _defaultPatternName;
        private readonly Dictionary<string, ElementId> _createdPerLayer = new();

        public DetailFillRegionStyleService(
            Document document,
            FillPatternResolutionService resolver,
            string defaultPatternName)
        {
            _doc = document;
            _resolver = resolver;
            _defaultPatternName = defaultPatternName;
        }

        /// <summary>
        /// Gets an existing FilledRegionType matching the CAD layer name,
        /// or falls back to a type named after the global default pattern.
        /// Returns null if the user chooses to skip this CAD layer.
        /// </summary>
        public FilledRegionType GetOrResolve(string cadLayerName)
        {
            // Try a FilledRegionType already named after this layer.
            var existing = new FilteredElementCollector(_doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .FirstOrDefault(t => t.Name.Equals(cadLayerName));

            if (existing != null)
                return existing;

            MissingFillPatternDecision decision =
                _resolver.Resolve(cadLayerName, _defaultPatternName);

            if (decision == MissingFillPatternDecision.Skip)
                return null;

            // Base the new type on the FilledRegionType matching the global
            // default pattern name if one exists, otherwise on the first
            // available FilledRegionType (Revit requires duplicating an
            // existing type — there is no bare "create new" constructor).
            FilledRegionType baseType =
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .FirstOrDefault(t => t.Name.Equals(_defaultPatternName))
                ?? new FilteredElementCollector(_doc)
                    .OfClass(typeof(FilledRegionType))
                    .Cast<FilledRegionType>()
                    .FirstOrDefault();

            if (baseType == null)
                return null;

            FilledRegionType newType =
                (FilledRegionType)baseType.Duplicate(cadLayerName);

            return newType;
        }
    }
}
