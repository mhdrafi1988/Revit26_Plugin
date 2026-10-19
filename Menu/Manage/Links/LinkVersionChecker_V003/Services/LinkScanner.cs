using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Revit26_Plugin_Revised.LinkVersionChecker_V003.Core.Models;
using Revit26_Plugin_Revised.LinkVersionChecker_V003.Core.Rules;
using RevitApp = Autodesk.Revit.ApplicationServices.Application;

namespace Revit26_Plugin_Revised.LinkVersionChecker_V003.Services
{
    /// <summary>
    /// Scan pipeline:
    ///   1. link tables (TransmissionData / open document)  — main thread
    ///   2. file-system probes                              — parallel, background
    ///   3. RVT + IFC-cache version headers (BasicFileInfo)  — main thread, cached
    ///   4. nested links (link tables of each RVT link)      — main thread, cached
    ///   5. rule engine
    /// </summary>
    public sealed class LinkScanner
    {
        private readonly RevitFileReader _reader = new RevitFileReader();
        private readonly ConcurrentDictionary<string, FileFacts> _probes = new ConcurrentDictionary<string, FileFacts>(StringComparer.OrdinalIgnoreCase);

        public int CurrentYear { get; }
        public List<string> HostErrors { get; } = new List<string>();

        public LinkScanner(RevitApp app)
        {
            CurrentYear = int.TryParse(app.VersionNumber, out int y) ? y : 2026;
        }

        /// <summary>Forget cached headers/probes (call after an action changed files).</summary>
        public void ClearCache()
        {
            _reader.ClearCache();
            _probes.Clear();
        }

        // =====================================================================
        // PRE-OPEN: one or many host files (folder audit)
        // =====================================================================
        public async Task<List<LinkInfo>> ScanHostsAsync(IList<string> hostPaths, bool includeNested, ScanReporter rep)
        {
            HostErrors.Clear();
            var all = new List<LinkInfo>();

            for (int i = 0; i < hostPaths.Count; i++)
            {
                string host = hostPaths[i];
                await rep.StepAsync(i, hostPaths.Count, $"Reading link table {i + 1}/{hostPaths.Count}: {Path.GetFileName(host)}");
                try
                {
                    foreach (var rd in _reader.ReadLinkTable(host))
                        all.Add(FromRef(rd, host, Path.GetFileName(host)));
                }
                catch (Exception ex)
                {
                    HostErrors.Add($"{Path.GetFileName(host)}: {ex.Message}");
                }
            }

            await ProbeAsync(all, rep);
            await ReadHeadersAsync(all, rep);

            if (includeNested)
            {
                var nested = await CollectNestedAsync(all, rep);
                if (nested.Count > 0)
                {
                    await ProbeAsync(nested, rep);
                    await ReadHeadersAsync(nested, rep);
                    all.AddRange(nested);
                }
            }

            rep.Report(1, 1, "Applying checks…");
            RuleEngine.Apply(all, CurrentYear, inModel: false);
            return Order(all);
        }

        private async Task<List<LinkInfo>> CollectNestedAsync(List<LinkInfo> top, ScanReporter rep)
        {
            var result = new List<LinkInfo>();
            var parents = top.Where(l => l.Kind == LinkKind.RVT && l.Exists && !l.IsUnloaded && !l.IsCloud).ToList();

            int done = 0;
            foreach (var parent in parents)
            {
                await rep.StepAsync(done++, parents.Count, $"Nested links: {parent.Name}");
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { parent.HostPath };
                AddNested(parent.FullPath, parent.Name, parent, 1, visited, result);
            }
            return result;
        }

        private void AddNested(string linkPath, string parentName, LinkInfo hostInfo, int depth, HashSet<string> visited, List<LinkInfo> output)
        {
            if (depth > 2 || !visited.Add(linkPath)) return;

            List<RefData> table;
            try { table = _reader.ReadLinkTable(linkPath); } catch { return; }

            foreach (var rd in table)
            {
                var li = FromRef(rd, hostInfo.HostPath, hostInfo.HostName);
                li.IsNested = true;
                li.ParentName = parentName;
                li.Attachment = "Unknown";
                li.CountsInTotal = false;     // only loads if the parent attaches it as Attachment
                output.Add(li);

                if (li.Kind == LinkKind.RVT && !li.IsCloud && File.Exists(li.FullPath))
                    AddNested(li.FullPath, li.Name, hostInfo, depth + 1, visited, output);
            }
        }

        /// <summary>Finds host models in a folder, skipping backups, IFC caches and the tool's own backups.</summary>
        public static Task<List<string>> FindHostsAsync(string folder, bool includeSubfolders, CancellationToken token)
        {
            return Task.Run(() =>
            {
                var backup = new Regex(@"\.\d{4}\.rvt$", RegexOptions.IgnoreCase);
                var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var list = new List<string>();
                foreach (var p in Directory.EnumerateFiles(folder, "*.rvt", option))
                {
                    token.ThrowIfCancellationRequested();
                    string name = Path.GetFileName(p);
                    if (backup.IsMatch(name)) continue;
                    if (name.EndsWith(".ifc.rvt", StringComparison.OrdinalIgnoreCase)) continue;
                    if (p.IndexOf(LinkActions.BackupFolderName, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (p.IndexOf("_backup" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (p.IndexOf("Revit_temp", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    list.Add(p);
                }
                return list.OrderBy(p => p).ToList();
            }, token);
        }

        // =====================================================================
        // IN-MODEL
        // =====================================================================
        public async Task<List<LinkInfo>> ScanDocumentAsync(Document doc, ScanReporter rep)
        {
            HostErrors.Clear();
            string hostPath = "";
            string baseDir = null;
            try
            {
                hostPath = doc.PathName ?? "";
                if (!doc.IsModelInCloud && !string.IsNullOrEmpty(hostPath)) baseDir = Path.GetDirectoryName(hostPath);
            }
            catch { /* ignore */ }
            string hostName = doc.Title;

            await rep.StepAsync(0, 1, "Reading links in the open model…");

            var all = new List<LinkInfo>();

            // Instances and workset state per link type
            var instances = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
            var instCount = instances.GroupBy(i => i.GetTypeId().Value).ToDictionary(g => g.Key, g => g.Count());
            var worksets = new Dictionary<long, (int open, int total)>();
            foreach (var inst in instances)
            {
                long typeId = inst.GetTypeId().Value;
                if (worksets.ContainsKey(typeId)) continue;
                try
                {
                    Document linkDoc = inst.GetLinkDocument();
                    if (linkDoc == null || !linkDoc.IsWorkshared) continue;
                    var ws = new FilteredWorksetCollector(linkDoc).OfKind(WorksetKind.UserWorkset).ToWorksets();
                    worksets[typeId] = (ws.Count(w => w.IsOpen), ws.Count);
                }
                catch { /* ignore */ }
            }

            // External file references
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ElementId id in ExternalFileUtils.GetAllExternalFileReferences(doc))
            {
                Element e = doc.GetElement(id);
                if (e == null) continue;
                ExternalFileReference efr;
                try { efr = e.GetExternalFileReference(); } catch { continue; }
                if (efr == null || RevitFileReader.IsIgnoredType(efr)) continue;

                var rd = RevitFileReader.FromReference(efr, baseDir);
                rd.Id = id;
                var li = FromRef(rd, hostPath, hostName);

                if (e is RevitLinkType rlt)
                {
                    li.Attachment = rlt.AttachmentType.ToString();
                    li.IsNested = rlt.IsNestedLink;
                    li.Instances = instCount.TryGetValue(id.Value, out int c) ? c : 0;
                    if (worksets.TryGetValue(id.Value, out var w)) { li.WorksetsOpen = w.open; li.WorksetsTotal = w.total; }
                    if (li.IsNested)
                    {
                        try
                        {
                            ElementId pid = rlt.GetParentId();
                            Element parent = pid != null && pid != ElementId.InvalidElementId ? doc.GetElement(pid) : null;
                            li.ParentName = parent?.Name ?? "parent link";
                        }
                        catch { li.ParentName = "parent link"; }
                    }
                    try
                    {
                        if (!li.IsNested && !RevitLinkType.IsLoaded(doc, id) &&
                            li.Status.IndexOf("NotFound", StringComparison.OrdinalIgnoreCase) < 0)
                            li.Status = "Unloaded";
                    }
                    catch { /* ignore */ }
                }

                seenPaths.Add(li.FullPath);
                all.Add(li);
            }

            // Linked images / PDFs
            foreach (ImageType it in new FilteredElementCollector(doc).OfClass(typeof(ImageType)).Cast<ImageType>())
            {
                try
                {
                    if (it.Source != ImageTypeSource.Link) continue;
                    string p = it.Path;
                    if (string.IsNullOrEmpty(p) || !seenPaths.Add(p)) continue;
                    all.Add(new LinkInfo
                    {
                        HostPath = hostPath, HostName = hostName,
                        Name = Path.GetFileName(p), Kind = LinkKind.Image,
                        StoredPath = p, FullPath = p,
                        PathType = Path.IsPathRooted(p) ? "Absolute" : "Relative",
                        IsUnc = p.StartsWith(@"\\"), Status = "Loaded", RefId = it.Id
                    });
                }
                catch { /* ignore */ }
            }

            // Imported (not linked) CAD
            var imports = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).Cast<ImportInstance>()
                .Where(i => !i.IsLinked).GroupBy(i => i.GetTypeId().Value);
            foreach (var g in imports)
            {
                Element type = doc.GetElement(g.First().GetTypeId());
                all.Add(new LinkInfo
                {
                    HostPath = hostPath, HostName = hostName,
                    Name = type?.Name ?? "Imported CAD", Kind = LinkKind.CAD,
                    IsImport = true, Exists = true, Status = "Imported",
                    Instances = g.Count(), RefId = type?.Id
                });
            }

            await ProbeAsync(all, rep);
            await ReadHeadersAsync(all, rep);

            rep.Report(1, 1, "Applying checks…");
            RuleEngine.Apply(all, CurrentYear, inModel: true);
            return Order(all);
        }

        // =====================================================================
        // SHARED STEPS
        // =====================================================================
        private static LinkInfo FromRef(RefData rd, string hostPath, string hostName)
        {
            return new LinkInfo
            {
                HostPath = hostPath,
                HostName = hostName,
                RefId = rd.Id,
                StoredPath = rd.Stored,
                FullPath = rd.Full,
                Name = SafeFileName(rd.Full.Length > 0 ? rd.Full : rd.Stored),
                PathType = rd.PathType,
                IsCloud = rd.Cloud,
                IsUnc = rd.Full.StartsWith(@"\\"),
                Status = rd.Status,
                Kind = KindOf(rd.Full, rd.RefType)
            };
        }

        /// <summary>Step 2 — file-system facts, in parallel, each distinct path once.</summary>
        private async Task ProbeAsync(List<LinkInfo> links, ScanReporter rep)
        {
            var todo = links.Where(l => !l.IsCloud && !l.IsImport && !string.IsNullOrEmpty(l.FullPath))
                            .Select(l => (path: l.FullPath, kind: l.Kind))
                            .Distinct()
                            .Where(t => !_probes.ContainsKey(Key(t.path, t.kind)))
                            .ToList();

            int done = 0;
            if (todo.Count > 0)
            {
                CancellationToken token = rep.Token;
                Task work = Task.Run(() =>
                {
                    var opts = new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = token };
                    Parallel.ForEach(todo, opts, t =>
                    {
                        _probes[Key(t.path, t.kind)] = FileProbe.Probe(t.path, t.kind);
                        Interlocked.Increment(ref done);
                    });
                }, token);

                await rep.WaitAsync(work, () => $"Checking files on disk ({done}/{todo.Count})…", () => done, todo.Count);
            }

            foreach (var l in links)
            {
                if (l.IsCloud || l.IsImport || string.IsNullOrEmpty(l.FullPath)) continue;
                if (!_probes.TryGetValue(Key(l.FullPath, l.Kind), out var f)) continue;

                l.Exists = f.Exists;
                l.SizeMb = f.SizeMb;
                l.CadVersion = f.CadVersion;
                if (l.Kind == LinkKind.IFC)
                {
                    l.IfcPath = f.IfcPath; l.IfcSchema = f.IfcSchema; l.CachePath = f.CachePath;
                    l.CacheExists = f.CacheExists; l.CacheTimeUtc = f.CacheTimeUtc; l.IfcTimeUtc = f.IfcTimeUtc;
                    l.CacheFolderWritable = f.CacheFolderWritable;
                }
                if (!l.Exists) l.Status = "Not Found";
            }
        }

        /// <summary>Step 3 — version headers of RVT links and IFC caches (main thread, cached).</summary>
        private async Task ReadHeadersAsync(List<LinkInfo> links, ScanReporter rep)
        {
            var targets = links.Where(l => !l.IsCloud && l.Exists &&
                                           (l.Kind == LinkKind.RVT || (l.Kind == LinkKind.IFC && l.CacheExists))).ToList();
            for (int i = 0; i < targets.Count; i++)
            {
                var l = targets[i];
                await rep.StepAsync(i, targets.Count, $"Reading versions ({i + 1}/{targets.Count}): {l.Name}");

                if (l.Kind == LinkKind.RVT)
                {
                    var h = _reader.ReadHeader(l.FullPath);
                    l.HeaderRead = h.Ok;
                    l.HeaderError = h.Error;
                    l.SavedYear = h.Year;
                    l.IsCurrentVersion = h.IsCurrent;
                    l.IsLaterVersion = h.IsLater;
                    l.IsWorkshared = h.IsWorkshared;
                    l.IsCentral = h.IsCentral;
                    l.IsLocal = h.IsLocal;
                }
                else
                {
                    var h = _reader.ReadHeader(l.CachePath);
                    l.CacheYear = h.Ok ? h.Year : "?";
                    l.CacheIsCurrent = h.Ok && h.IsCurrent;
                }
            }
        }

        /// <summary>Host → top-level links by type/name → each link's nested links right under it.</summary>
        private static List<LinkInfo> Order(List<LinkInfo> all)
        {
            var result = new List<LinkInfo>();
            foreach (var host in all.GroupBy(l => l.HostName ?? "").OrderBy(g => g.Key))
            {
                var items = host.ToList();
                var nested = items.Where(l => l.IsNested).ToList();
                var used = new HashSet<LinkInfo>();

                void AddWithChildren(LinkInfo parent, int depth)
                {
                    result.Add(parent);
                    used.Add(parent);
                    if (depth > 3) return;
                    foreach (var child in nested.Where(n => !used.Contains(n) &&
                                 string.Equals(n.ParentName, parent.Name, StringComparison.OrdinalIgnoreCase))
                                 .OrderBy(n => n.Name).ToList())
                        AddWithChildren(child, depth + 1);
                }

                foreach (var top in items.Where(l => !l.IsNested).OrderBy(l => l.Kind).ThenBy(l => l.Name))
                    AddWithChildren(top, 0);
                result.AddRange(nested.Where(n => !used.Contains(n)).OrderBy(n => n.Name));
            }
            return result;
        }

        private static string Key(string path, LinkKind kind) => kind + "|" + path;

        public static LinkKind KindOf(string path, string refType)
        {
            string p = (path ?? "").ToLowerInvariant();
            if (p.EndsWith(".ifc") || p.EndsWith(".ifc.rvt") || p.EndsWith(".ifczip") || p.EndsWith(".ifcxml")) return LinkKind.IFC;
            if (p.EndsWith(".rvt") || refType == "RevitLink") return LinkKind.RVT;
            if (p.EndsWith(".rcp") || p.EndsWith(".rcs")) return LinkKind.PointCloud;
            string[] img = { ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
            if (img.Any(p.EndsWith)) return LinkKind.Image;
            string[] cad = { ".dwg", ".dxf", ".dgn", ".sat", ".skp", ".3dm", ".nwc", ".nwd", ".axm", ".obj", ".stl" };
            if (cad.Any(p.EndsWith) || refType == "CADLink") return LinkKind.CAD;
            return LinkKind.Other;
        }

        private static string SafeFileName(string path)
        {
            try { return Path.GetFileName(path); } catch { return path; }
        }
    }
}
