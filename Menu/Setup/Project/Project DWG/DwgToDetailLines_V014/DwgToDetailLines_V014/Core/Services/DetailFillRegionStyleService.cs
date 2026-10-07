// ==============================================
// File: DetailFillRegionStyleService.cs
// Layer: Core/Services
// Changes vs V013: filled region types are looked up once into a
// dictionary instead of a collector per layer, and GetByName resolves
// shortlist-mode mappings without any prompt.
// ==============================================

using Autodesk.Revit.DB;
using System.Collections.Generic;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>
    /// Resolves FilledRegionType for FilledRegion creation, either by CAD hatch
    /// layer name (with a create/skip prompt for missing ones, pre-filled with
    /// the global default) or by an explicit type name. Mirrors
    /// DetailLineStyleService's structure for line styles.
    /// </summary>
    public class DetailFillRegionStyleService
    {
        private readonly FillPatternResolutionService _resolver;
        private readonly string _defaultPatternName;
        private readonly Dictionary<string, FilledRegionType> _byName = new();
        private readonly FilledRegionType _firstType;

        /// <summary>Indexes the project's filled region types.</summary>
        public DetailFillRegionStyleService(
            Document document,
            FillPatternResolutionService resolver,
            string defaultPatternName)
        {
            _resolver = resolver;
            _defaultPatternName = defaultPatternName;

            foreach (FilledRegionType t in new FilteredElementCollector(document).OfClass(typeof(FilledRegionType)))
            {
                _firstType ??= t;
                _byName.TryAdd(t.Name, t);
            }
        }

        /// <summary>
        /// Layer Name mode: the type named exactly like <paramref name="cadLayerName"/>;
        /// when missing, prompts to create it (a copy of the default type) or skip (null).
        /// </summary>
        public FilledRegionType GetOrResolve(string cadLayerName)
        {
            FilledRegionType existing = GetByName(cadLayerName);
            if (existing != null)
                return existing;

            MissingFillPatternDecision decision =
                _resolver.Resolve(cadLayerName, _defaultPatternName);

            if (decision == MissingFillPatternDecision.Skip)
                return null;

            FilledRegionType baseType = GetByName(_defaultPatternName) ?? _firstType;
            if (baseType == null)
                return null;

            var newType = (FilledRegionType)baseType.Duplicate(cadLayerName);
            _byName[cadLayerName] = newType;
            return newType;
        }

        /// <summary>Existing type named <paramref name="typeName"/>, or null. Never prompts or creates.</summary>
        public FilledRegionType GetByName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            return _byName.TryGetValue(typeName, out FilledRegionType t) ? t : null;
        }
    }
}
