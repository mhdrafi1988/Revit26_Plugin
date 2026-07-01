using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using System;
using System.Linq;

namespace Revit26_Plugin.LinesFromMechanical.V009.Services;

/// <summary>
/// Extensible-storage identity tracking for created detail lines and floors.
///
/// SCHEMA HISTORY:
///   V003 used the literal "...700V001" which is NOT a valid GUID — it could
///   never be constructed, so no real V001-tagged entities exist in any model.
///   There is therefore nothing to migrate. V007 starts fresh on a valid GUID
///   with schema name "...V009". Geometry created before V007 is untracked and
///   will not be detected by existence checks.
/// </summary>
public static class CircleIdentityStorage
{
    private static readonly Guid SchemaGuid =
        new("A3C7821E-4F90-4B2D-9E56-D1F3B8C20007");

    private const string SchemaName       = "LinkedMechanicalEquipmentCircleIdentityV009";
    private const string SourceKeyField   = "SourceKey";
    private const string ElementTypeField = "ElementType";

    private static Schema? _cachedSchema;

    public enum LinkedElementType { DetailLine, Floor }

    /// <summary>Call once before any transaction is opened to build/cache the schema.</summary>
    public static void Initialize() => GetOrCreateSchema();

    public static Schema GetOrCreateSchema()
    {
        if (_cachedSchema != null)
            return _cachedSchema;

        Schema? schema = Schema.Lookup(SchemaGuid);
        if (schema != null)
        {
            _cachedSchema = schema;
            return schema;
        }

        var builder = new SchemaBuilder(SchemaGuid);
        builder.SetSchemaName(SchemaName);
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField(SourceKeyField,   typeof(string));
        builder.AddSimpleField(ElementTypeField, typeof(string));

        schema = builder.Finish();
        _cachedSchema = schema;
        return schema;
    }

    public static string BuildSourceKey(RevitLinkInstance linkInstance, Element linkedElement)
        => $"{linkInstance.UniqueId}|{linkedElement.UniqueId}";

    public static bool DetailCurveExistsForSource(Document doc, View view, string sourceKey)
    {
        try
        {
            Schema schema = GetOrCreateSchema();
            return new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(CurveElement))
                .WhereElementIsNotElementType()
                .Cast<CurveElement>()
                .Any(c =>
                {
                    if (c == null) return false;
                    Entity entity = c.GetEntity(schema);
                    return entity.IsValid()
                        && entity.Get<string>(SourceKeyField)   == sourceKey
                        && entity.Get<string>(ElementTypeField) == LinkedElementType.DetailLine.ToString();
                });
        }
        catch { return false; }
    }

    public static bool FloorExistsForSource(Document doc, string sourceKey)
    {
        try
        {
            Schema schema = GetOrCreateSchema();
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .Any(f =>
                {
                    if (f == null) return false;
                    Entity entity = f.GetEntity(schema);
                    return entity.IsValid()
                        && entity.Get<string>(SourceKeyField)   == sourceKey
                        && entity.Get<string>(ElementTypeField) == LinkedElementType.Floor.ToString();
                });
        }
        catch { return false; }
    }

    public static void AttachSourceKey(
        Element element,
        string sourceKey,
        LinkedElementType elementType = LinkedElementType.DetailLine)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        Schema schema = GetOrCreateSchema();
        var entity = new Entity(schema);
        entity.Set(SourceKeyField,   sourceKey);
        entity.Set(ElementTypeField, elementType.ToString());
        element.SetEntity(entity);
    }
}
