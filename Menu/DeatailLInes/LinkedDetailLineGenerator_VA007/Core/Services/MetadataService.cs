using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA007.Core.Services
{
    /// <summary>
    /// ExtensibleStorage schema + read/write for source metadata attached to every
    /// generated Detail Line, per spec Section 23. Defined now (Phase 2) even though
    /// Update/Refresh/Delete commands are future work, per Rafi's confirmation --
    /// cheaper to bake the schema in at V1 than retrofit onto already-created lines.
    ///
    /// Schema GUID is fixed and must never change across versions of this tool --
    /// changing it would orphan metadata on lines created by earlier builds.
    /// </summary>
    public class MetadataService
    {
        private static readonly Guid SchemaGuid = new Guid("A7E4F1B2-3C4D-4E5F-8A9B-1C2D3E4F5A6B");
        private const string SchemaName = "LinkedDetailLineGenerator_SourceMetadata";
        private const string VendorId = "RAFI_Revit26_Plugin";

        private const string FieldSourceLinkInstanceId = "SourceLinkInstanceId";
        private const string FieldSourceLinkDocTitle = "SourceLinkDocTitle";
        private const string FieldSourceCategory = "SourceCategory";
        private const string FieldSourceFamily = "SourceFamily";
        private const string FieldSourceType = "SourceType";
        private const string FieldSourceElementId = "SourceElementId";
        private const string FieldRepresentationMode = "RepresentationMode";
        private const string FieldMappingId = "MappingId";
        private const string FieldGeneratorVersion = "GeneratorVersion";

        public const string GeneratorVersion = "VA007";

        private Schema GetOrCreateSchema()
        {
            Schema? existing = Schema.Lookup(SchemaGuid);
            if (existing != null) return existing;

            SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(SchemaName);
            builder.SetVendorId(VendorId);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);

            builder.AddSimpleField(FieldSourceLinkInstanceId, typeof(long));
            builder.AddSimpleField(FieldSourceLinkDocTitle, typeof(string));
            builder.AddSimpleField(FieldSourceCategory, typeof(string));
            builder.AddSimpleField(FieldSourceFamily, typeof(string));
            builder.AddSimpleField(FieldSourceType, typeof(string));
            builder.AddSimpleField(FieldSourceElementId, typeof(long));
            builder.AddSimpleField(FieldRepresentationMode, typeof(string));
            builder.AddSimpleField(FieldMappingId, typeof(string));
            builder.AddSimpleField(FieldGeneratorVersion, typeof(string));

            return builder.Finish();
        }

        public void WriteMetadata(
            Element detailLineElement,
            long sourceLinkInstanceId,
            string sourceLinkDocTitle,
            string sourceCategory,
            string sourceFamily,
            string sourceType,
            long sourceElementId,
            string representationMode,
            Guid mappingId)
        {
            Schema schema = GetOrCreateSchema();
            Entity entity = new Entity(schema);

            entity.Set(FieldSourceLinkInstanceId, sourceLinkInstanceId);
            entity.Set(FieldSourceLinkDocTitle, sourceLinkDocTitle);
            entity.Set(FieldSourceCategory, sourceCategory);
            entity.Set(FieldSourceFamily, sourceFamily);
            entity.Set(FieldSourceType, sourceType);
            entity.Set(FieldSourceElementId, sourceElementId);
            entity.Set(FieldRepresentationMode, representationMode);
            entity.Set(FieldMappingId, mappingId.ToString());
            entity.Set(FieldGeneratorVersion, GeneratorVersion);

            detailLineElement.SetEntity(entity);
        }

        // ── Column profile metadata (second schema) ─────────────────────
        // The source-metadata schema above is frozen (its GUID must never change,
        // and a persisted schema can't gain fields), so the profile a column was
        // drawn with lives in its own schema, attached alongside it.

        private static readonly Guid ProfileSchemaGuid = new Guid("5B3C9D2E-7A41-4F6B-9E08-2D1A6C4B8F35");
        private const string ProfileSchemaName = "LinkedDetailLineGenerator_ColumnProfile";

        private const string FieldProfileShape = "ProfileShape";
        private const string FieldProfileSource = "ProfileSource";
        private const string FieldProfileWidth = "Width";
        private const string FieldProfileHeight = "Height";
        private const string FieldProfileDiameter = "Diameter";
        private const string FieldProfileRotation = "Rotation";

        private Schema GetOrCreateProfileSchema()
        {
            Schema? existing = Schema.Lookup(ProfileSchemaGuid);
            if (existing != null) return existing;

            SchemaBuilder builder = new SchemaBuilder(ProfileSchemaGuid);
            builder.SetSchemaName(ProfileSchemaName);
            builder.SetVendorId(VendorId);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);

            builder.AddSimpleField(FieldProfileShape, typeof(string));
            builder.AddSimpleField(FieldProfileSource, typeof(string));
            builder.AddSimpleField(FieldProfileWidth, typeof(double)).SetSpec(SpecTypeId.Length);
            builder.AddSimpleField(FieldProfileHeight, typeof(double)).SetSpec(SpecTypeId.Length);
            builder.AddSimpleField(FieldProfileDiameter, typeof(double)).SetSpec(SpecTypeId.Length);
            builder.AddSimpleField(FieldProfileRotation, typeof(double)).SetSpec(SpecTypeId.Angle);

            return builder.Finish();
        }

        /// <summary>Records the profile a column was drawn with: shape, where its size
        /// came from, its size (feet; unused fields are 0) and its rotation in the
        /// HOST document (radians; 0 for circles and custom outlines).</summary>
        public void WriteProfileMetadata(Element detailLineElement, ColumnFootprint footprint, double hostRotationRadians)
        {
            Entity entity = new Entity(GetOrCreateProfileSchema());

            entity.Set(FieldProfileShape, footprint.Shape.ToString());
            entity.Set(FieldProfileSource, footprint.Source.ToString());
            entity.Set(FieldProfileWidth, footprint.WidthFeet, UnitTypeId.Feet);
            entity.Set(FieldProfileHeight, footprint.HeightFeet, UnitTypeId.Feet);
            entity.Set(FieldProfileDiameter, footprint.DiameterFeet, UnitTypeId.Feet);
            entity.Set(FieldProfileRotation, hostRotationRadians, UnitTypeId.Radians);

            detailLineElement.SetEntity(entity);
        }

        /// <summary>Reads back the column profile recorded on a generated Detail Line,
        /// or null if it has none (not a column, or generated before this existed).</summary>
        public ColumnProfileMetadata? ReadProfileMetadata(Element element)
        {
            Schema? schema = Schema.Lookup(ProfileSchemaGuid);
            if (schema == null) return null;

            Entity entity = element.GetEntity(schema);
            if (!entity.IsValid()) return null;

            return new ColumnProfileMetadata
            {
                Shape = entity.Get<string>(FieldProfileShape),
                Source = entity.Get<string>(FieldProfileSource),
                WidthFeet = entity.Get<double>(FieldProfileWidth, UnitTypeId.Feet),
                HeightFeet = entity.Get<double>(FieldProfileHeight, UnitTypeId.Feet),
                DiameterFeet = entity.Get<double>(FieldProfileDiameter, UnitTypeId.Feet),
                RotationRadians = entity.Get<double>(FieldProfileRotation, UnitTypeId.Radians),
            };
        }

        /// <summary>Reads back metadata for future Update/Delete commands. Returns
        /// null if the element has no metadata entity attached (not generated by
        /// this tool, or from an incompatible schema version).</summary>
        public SourceMetadata? ReadMetadata(Element element)
        {
            Schema? schema = Schema.Lookup(SchemaGuid);
            if (schema == null) return null;

            Entity entity = element.GetEntity(schema);
            if (!entity.IsValid()) return null;

            return new SourceMetadata
            {
                SourceLinkInstanceId = entity.Get<long>(FieldSourceLinkInstanceId),
                SourceLinkDocTitle = entity.Get<string>(FieldSourceLinkDocTitle),
                SourceCategory = entity.Get<string>(FieldSourceCategory),
                SourceFamily = entity.Get<string>(FieldSourceFamily),
                SourceType = entity.Get<string>(FieldSourceType),
                SourceElementId = entity.Get<long>(FieldSourceElementId),
                RepresentationMode = entity.Get<string>(FieldRepresentationMode),
                MappingId = entity.Get<string>(FieldMappingId),
                GeneratorVersion = entity.Get<string>(FieldGeneratorVersion),
            };
        }
    }

    /// <summary>Deserialized view of a column Detail Line's profile metadata.</summary>
    public class ColumnProfileMetadata
    {
        public string Shape { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public double WidthFeet { get; set; }
        public double HeightFeet { get; set; }
        public double DiameterFeet { get; set; }
        public double RotationRadians { get; set; }
    }

    /// <summary>Deserialized view of a generated Detail Line's ExtensibleStorage metadata.</summary>
    public class SourceMetadata
    {
        public long SourceLinkInstanceId { get; set; }
        public string SourceLinkDocTitle { get; set; } = string.Empty;
        public string SourceCategory { get; set; } = string.Empty;
        public string SourceFamily { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public long SourceElementId { get; set; }
        public string RepresentationMode { get; set; } = string.Empty;
        public string MappingId { get; set; } = string.Empty;
        public string GeneratorVersion { get; set; } = string.Empty;
    }
}
