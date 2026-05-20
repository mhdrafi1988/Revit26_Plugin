using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using System;
using System.Linq;

namespace Revit26_Plugin.LinesFromMechanical.V003.Services;

public static class CircleIdentityStorage
{
    // FIXED: Removed 'V' character - now valid hexadecimal GUID
    private static readonly Guid SchemaGuid =
        new("70F4544F-7C59-4D76-A31A-F5D5B2C80025");

    private const string SchemaName = "LinkedMechanicalEquipmentCircleIdentity";
    private const string SourceKeyField = "SourceKey";
    private const string ElementTypeField = "ElementType";

    private static Schema? _cachedSchema = null;

    public enum LinkedElementType
    {
        DetailLine,
        Floor
    }

    /// <summary>
    /// Call this once before any transaction is opened to ensure the schema
    /// is built and cached outside of any active transaction context.
    /// </summary>
    public static void Initialize()
    {
        GetOrCreateSchema();
    }

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
        builder.AddSimpleField(SourceKeyField, typeof(string));
        builder.AddSimpleField(ElementTypeField, typeof(string));

        schema = builder.Finish();
        _cachedSchema = schema;
        return schema;
    }

    public static string BuildSourceKey(RevitLinkInstance linkInstance, Element linkedElement)
    {
        return $"{linkInstance.UniqueId}|{linkedElement.UniqueId}";
    }

    public static bool DetailCurveExistsForSource(Document doc, View view, string sourceKey)
    {
        try
        {
            Schema schema = GetOrCreateSchema();

            return new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(CurveElement))
                .WhereElementIsNotElementType()
                .Cast<CurveElement>()
                .Any(curveElement =>
                {
                    if (curveElement == null) return false;
                    Entity entity = curveElement.GetEntity(schema);
                    return entity.IsValid()
                           && entity.Get<string>(SourceKeyField) == sourceKey
                           && entity.Get<string>(ElementTypeField) == LinkedElementType.DetailLine.ToString();
                });
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool FloorExistsForSource(Document doc, string sourceKey)
    {
        try
        {
            Schema schema = GetOrCreateSchema();

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .Any(floor =>
                {
                    if (floor == null) return false;
                    Entity entity = floor.GetEntity(schema);
                    return entity.IsValid()
                           && entity.Get<string>(SourceKeyField) == sourceKey
                           && entity.Get<string>(ElementTypeField) == LinkedElementType.Floor.ToString();
                });
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void AttachSourceKey(Element element, string sourceKey, LinkedElementType elementType = LinkedElementType.DetailLine)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        Schema schema = GetOrCreateSchema();

        var entity = new Entity(schema);
        entity.Set(SourceKeyField, sourceKey);
        entity.Set(ElementTypeField, elementType.ToString());

        element.SetEntity(entity);
    }
}