using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkVersionChecker_V003.Services
{
    public sealed class RvtHeader
    {
        public bool Ok;
        public string Year = "";
        public bool IsCurrent, IsLater, IsWorkshared, IsCentral, IsLocal;
        public string Error = "";
    }

    /// <summary>One entry of a file's link table (TransmissionData), independent of any host.</summary>
    public sealed class RefData
    {
        public ElementId Id;
        public string RefType = "";
        public string Stored = "";
        public string Full = "";
        public string PathType = "";
        public string Status = "";
        public bool Cloud;
    }

    /// <summary>
    /// Revit API reads that don't open a model: BasicFileInfo (version header) and
    /// TransmissionData (link table). Call on Revit's main thread only. Results are cached
    /// so a folder audit reads each shared link once.
    /// </summary>
    public sealed class RevitFileReader
    {
        private readonly Dictionary<string, RvtHeader> _headers = new Dictionary<string, RvtHeader>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<RefData>> _tables = new Dictionary<string, List<RefData>>(StringComparer.OrdinalIgnoreCase);

        public void ClearCache() { _headers.Clear(); _tables.Clear(); }

        public RvtHeader ReadHeader(string path)
        {
            if (_headers.TryGetValue(path, out var cached)) return cached;
            var h = new RvtHeader();
            try
            {
                BasicFileInfo bfi = BasicFileInfo.Extract(path);
                var m = Regex.Match(bfi.Format ?? "", @"\d{4}");
                h.Year = m.Success ? m.Value : (bfi.Format ?? "?");
                h.IsCurrent = bfi.IsSavedInCurrentVersion;
                h.IsLater = bfi.IsSavedInLaterVersion;
                h.IsWorkshared = bfi.IsWorkshared;
                h.IsCentral = bfi.IsCentral;
                h.IsLocal = bfi.IsLocal;
                h.Ok = true;
            }
            catch (Exception ex) { h.Error = ex.Message; }
            _headers[path] = h;
            return h;
        }

        /// <summary>Link table of a host or link file. Returns an empty list when it has no links.</summary>
        public List<RefData> ReadLinkTable(string filePath)
        {
            if (_tables.TryGetValue(filePath, out var cached)) return cached;

            var list = new List<RefData>();
            TransmissionData td = TransmissionData.ReadTransmissionData(ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath));
            if (td != null)
            {
                string baseDir = Path.GetDirectoryName(filePath);
                foreach (ElementId id in td.GetAllExternalFileReferenceIds())
                {
                    ExternalFileReference efr;
                    try { efr = td.GetLastSavedReferenceData(id); } catch { continue; }
                    if (efr == null || IsIgnoredType(efr)) continue;
                    var rd = FromReference(efr, baseDir);
                    rd.Id = id;
                    list.Add(rd);
                }
            }
            _tables[filePath] = list;
            return list;
        }

        public static bool IsIgnoredType(ExternalFileReference efr)
        {
            string t = efr.ExternalFileReferenceType.ToString();
            return t == "KeynoteTable" || t == "AssemblyCodeTable" || t == "Decal";
        }

        public static RefData FromReference(ExternalFileReference efr, string baseDir)
        {
            var rd = new RefData { RefType = efr.ExternalFileReferenceType.ToString() };
            ModelPath mp = efr.GetPath();
            rd.Stored = UserPath(mp);
            try { rd.Status = efr.GetLinkedFileStatus().ToString(); } catch { rd.Status = "Unknown"; }

            try { rd.Cloud = mp != null && mp.CloudPath; } catch { /* ignore */ }
            if (rd.Cloud || rd.Stored.StartsWith("Autodesk Docs", StringComparison.OrdinalIgnoreCase)
                         || rd.Stored.StartsWith("BIM 360", StringComparison.OrdinalIgnoreCase))
            {
                rd.Cloud = true;
                rd.Full = rd.Stored;
                rd.PathType = "Cloud";
                return rd;
            }

            string full = "";
            try { full = UserPath(efr.GetAbsolutePath()); } catch { /* fall back */ }
            if (string.IsNullOrEmpty(full)) full = rd.Stored;
            if (!Path.IsPathRooted(full) && !string.IsNullOrEmpty(baseDir))
            {
                try { full = Path.GetFullPath(Path.Combine(baseDir, rd.Stored)); } catch { /* keep */ }
            }
            rd.Full = full;
            try { rd.PathType = efr.PathType.ToString(); } catch { rd.PathType = ""; }
            return rd;
        }

        private static string UserPath(ModelPath mp)
        {
            try { return mp == null ? "" : ModelPathUtils.ConvertModelPathToUserVisiblePath(mp); }
            catch { return ""; }
        }
    }
}
