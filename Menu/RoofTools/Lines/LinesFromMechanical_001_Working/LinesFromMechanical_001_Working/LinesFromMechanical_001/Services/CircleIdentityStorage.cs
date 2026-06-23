using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using System;
using System.Linq;

namespace Revit26_Plugin.LinesFromMechanical.V001_01.Services;

public static class CircleIdentityStorage
{
    private static readonly Guid SchemaGuid =
        new("70F4544F-7C59-4D76-A31A-F5D5B2C70001");

    private const string SchemaName = "LinkedMechanicalEquipmentCircleIdentity";
    private const string SourceKeyField = "SourceKey";

    public static Schema GetOrCreateSchema()
    {
        Schema? schema = Schema.Lookup(SchemaGuid);
        if (schema != null)
            return schema;

        var builder = new SchemaBuilder(SchemaGuid);
        builder.SetSchemaName(SchemaName);
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField(SourceKeyField, typeof(string));

        return builder.Finish();
    }

    public static string BuildSourceKey(RevitLinkInstance linkInstance, Element linkedElement)
    {
        return $"{linkInstance.UniqueId}|{linkedElement.UniqueId}";
    }

    public static bool DetailCurveExistsForSource(Document doc, View view, string sourceKey)
    {
        Schema schema = GetOrCreateSchema();

        return new FilteredElementCollector(doc, view.Id)
            .OfClass(typeof(CurveElement))
            .WhereElementIsNotElementType()
            .Cast<CurveElement>()
            .Any(curveElement =>
            {
                Entity entity = curveElement.GetEntity(schema);
                return entity.IsValid()
                       && entity.Get<string>(SourceKeyField) == sourceKey;
            });
    }

    public static void AttachSourceKey(Element element, string sourceKey)
    {
        Schema schema = GetOrCreateSchema();

        var entity = new Entity(schema);
        entity.Set(SourceKeyField, sourceKey);

        element.SetEntity(entity);
    }
}