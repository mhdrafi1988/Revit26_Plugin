// ==============================================
// File: DetailFillRegionStyleService.cs
// Layer: Core/Services
// ==============================================

using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.DwgToDetailLines.V011.Core.Models;

namespace Revit26_Plugin.DwgToDetailLines.V011.Core.Services
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

        public FilledRegionType GetOrResolve(string cadLayerName)
        {
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
