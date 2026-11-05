using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Helpers
{
    /// <summary>
    /// v4.3.0: colours clashing annotations red in their view and removes that colour again.
    /// Only the projection line colour of the element override is changed, so any other
    /// override the user set (weight, pattern, halftone…) is kept. The original colour is
    /// saved on the view (Extensible Storage) so Clear can restore it even after the window
    /// or Revit was closed. Must run in a Revit API context.
    /// </summary>
    internal static class HighlightService
    {
        // Fixed forever: never change this GUID or the field name; add a new schema to migrate.
        private static readonly Guid SchemaGuid = new Guid("282B006F-89E4-4A8B-9451-205C1B3EB412");
        private const string SchemaName = "AnnotationOverlapDetection_Highlight";
        private const string VendorId = "RAFI_Revit26_Plugin";
        private const string JsonField = "Json";

        /// <summary>Version of the JSON stored in the schema field.</summary>
        private const int RecordVersion = 1;

        private static readonly Color HighlightColor = new Color(255, 0, 0);

        /// <summary>Outcome of a highlight / clear call, for the status line.</summary>
        public sealed class Outcome
        {
            public int Elements { get; set; }
            public int Views { get; set; }
            public List<string> Warnings { get; } = new();
        }

        private sealed class Record
        {
            public int Version { get; set; } = RecordVersion;
            public List<Item> Items { get; set; } = new();
        }

        private sealed class Item
        {
            public long Id { get; set; }
            public bool HadColor { get; set; }
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
        }

        /// <summary>
        /// Colours the given elements red, per view, in one undoable transaction.
        /// Elements already highlighted keep their first saved original colour.
        /// </summary>
        public static Outcome Highlight(Document doc, IDictionary<ElementId, HashSet<ElementId>> elementsByView)
        {
            var outcome = new Outcome();
            if (elementsByView == null || elementsByView.Count == 0)
                return outcome;

            Schema schema = GetOrCreateSchema();

            using var t = new Transaction(doc, "Highlight annotation overlaps");
            t.Start();
            try
            {
                foreach (var (viewId, ids) in elementsByView)
                {
                    if (doc.GetElement(viewId) is not View view || !view.IsValidObject)
                        continue;
                    if (!CanEditView(doc, view, outcome))
                        continue;

                    Record record = Read(view, schema) ?? new Record();
                    var known = record.Items.Select(i => i.Id).ToHashSet();
                    int count = 0;

                    foreach (ElementId id in ids)
                    {
                        if (doc.GetElement(id) == null)
                            continue;

                        OverrideGraphicSettings current = view.GetElementOverrides(id);
                        if (!known.Contains(id.Value))
                        {
                            Color c = current.ProjectionLineColor;
                            bool had = c != null && c.IsValid;
                            record.Items.Add(new Item
                            {
                                Id = id.Value,
                                HadColor = had,
                                R = had ? c.Red : (byte)0,
                                G = had ? c.Green : (byte)0,
                                B = had ? c.Blue : (byte)0
                            });
                            known.Add(id.Value);
                        }

                        var ogs = new OverrideGraphicSettings(current);
                        ogs.SetProjectionLineColor(HighlightColor);
                        view.SetElementOverrides(id, ogs);
                        count++;
                    }

                    if (count > 0)
                    {
                        Write(view, schema, record);
                        outcome.Elements += count;
                        outcome.Views++;
                    }
                }

                t.Commit();
            }
            catch
            {
                if (t.HasStarted() && !t.HasEnded())
                    t.RollBack();
                throw;
            }

            return outcome;
        }

        /// <summary>
        /// Removes every highlight this tool made in the document and restores the original
        /// colours, in one undoable transaction.
        /// </summary>
        public static Outcome ClearAll(Document doc)
        {
            var outcome = new Outcome();
            Schema schema = Schema.Lookup(SchemaGuid);
            if (schema == null)
                return outcome; // never highlighted in this document

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .WherePasses(new ExtensibleStorageFilter(SchemaGuid))
                .Cast<View>()
                .ToList();
            if (views.Count == 0)
                return outcome;

            using var t = new Transaction(doc, "Clear annotation overlap highlight");
            t.Start();
            try
            {
                foreach (View view in views)
                {
                    if (!CanEditView(doc, view, outcome))
                        continue;

                    Record record = Read(view, schema);
                    foreach (Item item in record?.Items ?? new List<Item>())
                    {
                        var id = new ElementId(item.Id);
                        if (doc.GetElement(id) == null)
                            continue; // deleted since: nothing to restore

                        var ogs = new OverrideGraphicSettings(view.GetElementOverrides(id));
                        ogs.SetProjectionLineColor(item.HadColor ? new Color(item.R, item.G, item.B) : Color.InvalidColorValue);
                        view.SetElementOverrides(id, ogs);
                        outcome.Elements++;
                    }

                    view.DeleteEntity(schema);
                    outcome.Views++;
                }

                t.Commit();
            }
            catch
            {
                if (t.HasStarted() && !t.HasEnded())
                    t.RollBack();
                throw;
            }

            return outcome;
        }

        /// <summary>True when the document has any highlight made by this tool.</summary>
        public static bool HasHighlights(Document doc)
        {
            if (Schema.Lookup(SchemaGuid) == null)
                return false;
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .WherePasses(new ExtensibleStorageFilter(SchemaGuid))
                .Any();
        }

        private static bool CanEditView(Document doc, View view, Outcome outcome)
        {
            if (view.IsTemplate)
                return false;

            if (doc.IsWorkshared)
            {
                CheckoutStatus status = WorksharingUtils.GetCheckoutStatus(doc, view.Id);
                if (status == CheckoutStatus.OwnedByOtherUser)
                {
                    string owner = WorksharingUtils.GetWorksharingTooltipInfo(doc, view.Id)?.Owner ?? "another user";
                    outcome.Warnings.Add($"\"{view.Name}\" is being edited by {owner}; skipped.");
                    return false;
                }
            }
            return true;
        }

        private static Schema GetOrCreateSchema()
        {
            Schema existing = Schema.Lookup(SchemaGuid);
            if (existing != null)
                return existing;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(SchemaName);
            builder.SetVendorId(VendorId);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.SetDocumentation("Original colours of annotations highlighted by Annotation Overlap Detection.");
            builder.AddSimpleField(JsonField, typeof(string));
            return builder.Finish();
        }

        private static Record Read(View view, Schema schema)
        {
            Entity entity = view.GetEntity(schema);
            if (entity == null || !entity.IsValid())
                return null;

            string json = entity.Get<string>(JsonField);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<Record>(json);
            }
            catch (JsonException)
            {
                return null; // unreadable record: Clear will still drop it
            }
        }

        private static void Write(View view, Schema schema, Record record)
        {
            record.Version = RecordVersion;
            var entity = new Entity(schema);
            entity.Set(JsonField, JsonSerializer.Serialize(record));
            view.SetEntity(entity);
        }
    }
}
