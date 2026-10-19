using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Revit26_Plugin_Revised.DwgStatistics.V005.Core.Models;

namespace Revit26_Plugin_Revised.DwgStatistics.V005.Core.Services
{
    /// <summary>
    /// "Compare with last scan" (V004) + "DWG updated on disk" (V005): builds snapshots and compares a
    /// new scan against one. Files are matched by kind + path (or name when there is no path), not by
    /// element ID, so a re-linked file is "Changed", not "Removed" + "New". Pure .NET.
    /// </summary>
    public static class DwgSnapshotService
    {
        // File times closer than this are treated as equal (FAT/network shares round to 2 s).
        private static readonly TimeSpan FileTimeTolerance = TimeSpan.FromSeconds(2);

        /// <summary>Builds a snapshot from the current statistics.</summary>
        public static DwgSnapshot Create(IEnumerable<DwgFileStats> files, string projectTitle, DateTime scannedAt)
        {
            return new DwgSnapshot
            {
                ScannedAt = scannedAt,
                ProjectTitle = projectTitle ?? string.Empty,
                Tool = DwgStatisticsTool.Label,
                ScannedBy = Environment.UserName,
                Files = files.Select(f => new DwgSnapshotEntry
                {
                    Key = KeyOf(f.Kind, f.Path, f.FileName),
                    TypeId = f.TypeId,
                    FileName = f.FileName,
                    Kind = f.Kind,
                    Status = f.Status,
                    Instances = f.InstanceCount,
                    Views = f.VisibleViewCount,
                    Sheets = f.SheetCount,
                    Pinned = f.PinnedCount,
                    FileModifiedUtc = f.FileModified?.ToUniversalTime(),
                    FileSizeBytes = f.FileSizeBytes
                }).ToList()
            };
        }

        /// <summary>
        /// Compares <paramref name="files"/> with <paramref name="baseline"/>: sets each file's change tag /
        /// detail, adds the V005 "Updated on disk" issue to links whose file is newer than at the baseline
        /// scan, and returns every change (removed files included). A null baseline gives no changes.
        /// </summary>
        public static List<DwgChange> Compare(IReadOnlyList<DwgFileStats> files, DwgSnapshot baseline)
        {
            var changes = new List<DwgChange>();
            foreach (var f in files)
            {
                f.ChangeTag = string.Empty;
                f.ChangeDetail = string.Empty;
                f.IsUpdatedOnDisk = false;
                f.Issues.RemoveAll(i => i.Text == UpdatedOnDiskText);
            }
            if (baseline == null) return changes;

            // Duplicate keys are possible (same file twice): pair them up in order.
            var old = baseline.Files
                .GroupBy(e => e.Key)
                .ToDictionary(g => g.Key, g => new Queue<DwgSnapshotEntry>(g));

            foreach (var f in files)
            {
                string key = KeyOf(f.Kind, f.Path, f.FileName);
                if (!old.TryGetValue(key, out var queue) || queue.Count == 0)
                {
                    f.ChangeTag = "New";
                    f.ChangeDetail = $"Not in the scan of {baseline.ScannedAt:yyyy-MM-dd HH:mm}.";
                    changes.Add(new DwgChange { Change = "New", FileName = f.FileName, Kind = f.Kind, Detail = f.ChangeDetail });
                    continue;
                }

                var before = queue.Dequeue();
                var parts = new List<string>();

                // V005: the DWG on disk is newer than when the baseline scan saw it.
                if (f.IsLinked && f.FileModified.HasValue && before.FileModifiedUtc.HasValue &&
                    f.FileModified.Value.ToUniversalTime() - before.FileModifiedUtc.Value > FileTimeTolerance)
                {
                    f.IsUpdatedOnDisk = true;
                    var local = f.FileModified.Value;
                    parts.Add($"file updated on disk {local:yyyy-MM-dd HH:mm}");
                    f.Issues.Insert(0, new DwgIssue(UpdatedOnDiskText, IssueSeverity.Warning,
                        $"The DWG was saved on {local:yyyy-MM-dd HH:mm}, after the scan of {baseline.ScannedAt:yyyy-MM-dd HH:mm}" +
                        (string.IsNullOrEmpty(baseline.ScannedBy) ? "." : $" by {baseline.ScannedBy}."),
                        "Tick it and use Reload Links so the model shows the latest drawing."));
                }

                if (!string.Equals(before.Status, f.Status, StringComparison.OrdinalIgnoreCase))
                    parts.Add($"status {before.Status} → {f.Status}");
                if (before.Instances != f.InstanceCount) parts.Add($"instances {before.Instances} → {f.InstanceCount}");
                if (before.Views != f.VisibleViewCount) parts.Add($"views {before.Views} → {f.VisibleViewCount}");
                if (before.Sheets != f.SheetCount) parts.Add($"sheets {before.Sheets} → {f.SheetCount}");
                if (before.Pinned != f.PinnedCount) parts.Add($"pinned {before.Pinned} → {f.PinnedCount}");

                if (parts.Count > 0)
                {
                    f.ChangeTag = "Changed";
                    f.ChangeDetail = string.Join("; ", parts);
                    changes.Add(new DwgChange { Change = "Changed", FileName = f.FileName, Kind = f.Kind, Detail = f.ChangeDetail });
                }
            }

            foreach (var leftover in old.Values.SelectMany(q => q))
            {
                changes.Add(new DwgChange
                {
                    Change = "Removed",
                    FileName = leftover.FileName,
                    Kind = leftover.Kind,
                    Detail = $"Had {leftover.Instances} instance(s) in {leftover.Views} view(s)."
                });
            }

            return changes
                .OrderBy(c => c.Change == "Removed" ? 0 : c.Change == "New" ? 1 : 2)
                .ThenBy(c => c.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// V005: after a successful Reload, records the files' current dates in the baseline so "Updated on
        /// disk" clears on the rescan (the model now shows that version).
        /// </summary>
        public static void MarkReloaded(DwgSnapshot baseline, IEnumerable<DwgFileStats> reloaded)
        {
            if (baseline == null) return;
            foreach (var f in reloaded)
            {
                string key = KeyOf(f.Kind, f.Path, f.FileName);
                foreach (var entry in baseline.Files.Where(e => e.Key == key))
                {
                    entry.FileModifiedUtc = f.FileModified?.ToUniversalTime();
                    entry.FileSizeBytes = f.FileSizeBytes;
                }
            }
        }

        /// <summary>Issue text of the V005 updated-on-disk check.</summary>
        public const string UpdatedOnDiskText = "Updated on disk";

        /// <summary>Match key of a file: "link|c:\cad\site.dwg".</summary>
        public static string KeyOf(string kind, string path, string fileName)
            => $"{kind}|{(string.IsNullOrWhiteSpace(path) ? fileName : path)}".Trim().ToLowerInvariant();

        /// <summary>Short, stable hash of a text (file names of the history).</summary>
        public static string Hash(string text)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes((text ?? string.Empty).ToLowerInvariant()));
                return string.Concat(bytes.Take(8).Select(b => b.ToString("x2")));
            }
        }
    }

    /// <summary>Where the history was read from / written to.</summary>
    public sealed class DwgHistoryLocation
    {
        /// <summary>Full path of the history file.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>True when it is the shared file next to the project.</summary>
        public bool IsShared { get; init; }

        /// <summary>"shared (\\server\proj\_DwgStatistics)" / "this computer".</summary>
        public string Label => IsShared ? $"shared ({(global::System.IO.Path.GetDirectoryName(Path))})" : "this computer";
    }

    /// <summary>
    /// V005 "Shared history": keeps the scan history in a <c>_DwgStatistics</c> folder next to the project
    /// file (central model folder for workshared models) so the whole team compares against the same
    /// last scan. Falls back to %AppData% when sharing is off, the model is in the cloud / on Revit Server /
    /// unsaved, or the folder cannot be written. Writes are atomic (temp file + move). Never throws.
    /// </summary>
    public static class DwgHistoryStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        /// <summary>Shared history path for a project folder, or null when there is none.</summary>
        public static string SharedPath(string projectFolder, string projectTitle, string projectKey)
        {
            if (string.IsNullOrWhiteSpace(projectFolder)) return null;
            string name = SafeName(projectTitle);
            return global::System.IO.Path.Combine(projectFolder, DwgStatisticsTool.SharedHistoryFolderName,
                $"{name}_{DwgSnapshotService.Hash(projectKey)}.dwgstats.json");
        }

        /// <summary>Local (this computer) history path.</summary>
        public static string LocalPath(string projectKey)
            => global::System.IO.Path.Combine(DwgStatisticsTool.DataFolder, "history", DwgSnapshotService.Hash(projectKey) + ".dwgstats.json");

        // V004 kept a bare snapshot here; read once as a baseline so the first V005 run still compares.
        private static string V004SnapshotPath(string projectKey)
            => global::System.IO.Path.Combine(DwgStatisticsTool.DataFolder, "snapshots", DwgSnapshotService.Hash(projectKey) + ".json");

        /// <summary>
        /// Loads the history: shared file first (when <paramref name="useShared"/>), then this computer's,
        /// then a V004 snapshot. Returns an empty history (Latest = null) when there is none.
        /// </summary>
        public static DwgHistoryFile Load(bool useShared, string projectFolder, string projectTitle, string projectKey,
            out DwgHistoryLocation readFrom)
        {
            readFrom = null;
            string shared = useShared ? SharedPath(projectFolder, projectTitle, projectKey) : null;

            if (shared != null && TryRead(shared, out var sharedFile))
            {
                readFrom = new DwgHistoryLocation { Path = shared, IsShared = true };
                return sharedFile;
            }

            string local = LocalPath(projectKey);
            if (TryRead(local, out var localFile))
            {
                readFrom = new DwgHistoryLocation { Path = local, IsShared = false };
                return localFile;
            }

            try
            {
                string v4 = V004SnapshotPath(projectKey);
                if (File.Exists(v4))
                {
                    var snap = JsonSerializer.Deserialize<DwgSnapshot>(File.ReadAllText(v4));
                    if (snap != null)
                    {
                        readFrom = new DwgHistoryLocation { Path = v4, IsShared = false };
                        return new DwgHistoryFile { ProjectTitle = projectTitle ?? string.Empty, Latest = snap };
                    }
                }
            }
            catch (Exception ex) when (IsIoOrJson(ex))
            {
                // No usable V004 snapshot: start fresh.
            }

            return new DwgHistoryFile { ProjectTitle = projectTitle ?? string.Empty };
        }

        /// <summary>
        /// Saves <paramref name="latest"/> as the new baseline and adds a line to the history log. Tries the
        /// shared location first (when <paramref name="useShared"/>); on failure falls back to this computer.
        /// Returns where it was written, or null when nothing could be written (<paramref name="error"/> set).
        /// </summary>
        public static DwgHistoryLocation Save(bool useShared, string projectFolder, string projectTitle, string projectKey,
            DwgSnapshot latest, DwgHistoryEntry entry, out string error, out string sharedError)
        {
            error = null;
            sharedError = null;
            string shared = useShared ? SharedPath(projectFolder, projectTitle, projectKey) : null;

            if (shared != null)
            {
                if (TryWrite(shared, projectTitle, latest, entry, out sharedError))
                    return new DwgHistoryLocation { Path = shared, IsShared = true };
            }

            string local = LocalPath(projectKey);
            if (TryWrite(local, projectTitle, latest, entry, out error))
                return new DwgHistoryLocation { Path = local, IsShared = false };

            return null;
        }

        private static bool TryWrite(string path, string projectTitle, DwgSnapshot latest, DwgHistoryEntry entry, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(global::System.IO.Path.GetDirectoryName(path));

                // Re-read just before writing so a teammate's newer log lines are kept.
                TryRead(path, out var file);
                file ??= new DwgHistoryFile();
                file.Format = 5;
                file.ProjectTitle = projectTitle ?? string.Empty;
                file.Latest = latest;
                file.History ??= new List<DwgHistoryEntry>();
                file.History.Insert(0, entry);
                if (file.History.Count > DwgStatisticsTool.HistoryLength)
                    file.History.RemoveRange(DwgStatisticsTool.HistoryLength, file.History.Count - DwgStatisticsTool.HistoryLength);

                string temp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(file, JsonOptions));
                File.Move(temp, path, overwrite: true);
                return true;
            }
            catch (Exception ex) when (IsIoOrJson(ex))
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryRead(string path, out DwgHistoryFile file)
        {
            file = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                file = JsonSerializer.Deserialize<DwgHistoryFile>(File.ReadAllText(path));
                if (file == null) return false;
                file.History ??= new List<DwgHistoryEntry>();
                return true;
            }
            catch (Exception ex) when (IsIoOrJson(ex))
            {
                file = null;
                return false;
            }
        }

        private static bool IsIoOrJson(Exception ex)
            => ex is IOException || ex is UnauthorizedAccessException || ex is JsonException ||
               ex is NotSupportedException || ex is ArgumentException || ex is global::System.Security.SecurityException;

        private static string SafeName(string title)
        {
            string name = string.IsNullOrWhiteSpace(title) ? "Project" : title;
            foreach (char c in global::System.IO.Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*"))
                name = name.Replace(c, '_');
            name = name.Trim().TrimEnd('.');
            return name.Length > 60 ? name.Substring(0, 60) : name;
        }
    }

    /// <summary>
    /// "Remember settings": one small JSON file in %AppData%. Load falls back to the V004 file, then to
    /// defaults on any problem, as the repo convention asks.
    /// </summary>
    public static class DwgSettingsStore
    {
        private static string SettingsPath
            => global::System.IO.Path.Combine(DwgStatisticsTool.DataFolder, "settings_v005.json");

        private static string V004SettingsPath
            => global::System.IO.Path.Combine(DwgStatisticsTool.DataFolder, "settings_v004.json");

        /// <summary>Loads the settings, or defaults.</summary>
        public static DwgToolSettings Load()
        {
            foreach (var path in new[] { SettingsPath, V004SettingsPath })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var s = JsonSerializer.Deserialize<DwgToolSettings>(File.ReadAllText(path));
                    if (s == null) continue;
                    s.MaxOpenViews = Math.Max(1, Math.Min(DwgStatisticsTool.MaxOpenViewsLimit, s.MaxOpenViews));
                    if (s.DetailTab < 0 || s.DetailTab > 2) s.DetailTab = 0;
                    s.Rules = (s.Rules ?? new DwgRules()).Normalized();
                    return s;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                           ex is JsonException || ex is NotSupportedException)
                {
                    // Try the next file / defaults.
                }
            }
            return new DwgToolSettings();
        }

        /// <summary>Saves the settings; failures are ignored (settings are a convenience).</summary>
        public static void Save(DwgToolSettings settings)
        {
            try
            {
                Directory.CreateDirectory(DwgStatisticsTool.DataFolder);
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                // Not critical: the next session starts from defaults.
            }
        }
    }
}
