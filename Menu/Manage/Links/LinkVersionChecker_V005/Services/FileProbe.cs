using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Revit26_Plugin_Revised.LinkVersionChecker_V005.Core.Models;

namespace Revit26_Plugin_Revised.LinkVersionChecker_V005.Services
{
    /// <summary>Plain file-system facts about one linked file.</summary>
    public sealed class FileFacts
    {
        public bool Exists;
        public double SizeMb;
        // IFC
        public string IfcPath = "";
        public string IfcSchema = "";
        public string CachePath = "";
        public bool CacheExists;
        public DateTime? CacheTimeUtc;
        public DateTime? IfcTimeUtc;
        public bool CacheFolderWritable = true;
        // CAD
        public string CadVersion = "";
        // Cloud-sync placeholder state (V005) — only meaningful for Desktop Connector / OneDrive paths.
        public AccSyncState DiskSync = AccSyncState.NotChecked;
    }

    /// <summary>
    /// File-system checks only — no Revit API — so it is safe to run on many threads at once.
    /// This is where network latency is spent, so the scanner runs it in parallel.
    /// </summary>
    public static class FileProbe
    {
        public static FileFacts Probe(string path, LinkKind kind)
        {
            var f = new FileFacts();
            if (string.IsNullOrWhiteSpace(path)) return f;

            try
            {
                switch (kind)
                {
                    case LinkKind.IFC:
                        ProbeIfc(path, f);
                        break;

                    case LinkKind.PointCloud:
                        f.Exists = File.Exists(path);
                        if (f.Exists)
                        {
                            f.SizeMb = PointCloudSizeMb(path);
                            f.DiskSync = PlaceholderState(new FileInfo(path));
                        }
                        break;

                    default:
                        var fi = new FileInfo(path);
                        f.Exists = fi.Exists;
                        if (f.Exists)
                        {
                            f.SizeMb = fi.Length / 1048576.0;
                            f.DiskSync = PlaceholderState(fi);
                            if (kind == LinkKind.CAD) f.CadVersion = ReadDwgVersion(path);
                        }
                        break;
                }
            }
            catch
            {
                // Access denied / path too long etc. — treat as not reachable.
            }
            return f;
        }

        public static string IfcPathFrom(string path) =>
            path.EndsWith(".ifc.rvt", StringComparison.OrdinalIgnoreCase) ? path.Substring(0, path.Length - 4) : path;

        private static void ProbeIfc(string path, FileFacts f)
        {
            f.IfcPath = IfcPathFrom(path);
            f.CachePath = f.IfcPath + ".RVT";

            var ifc = new FileInfo(f.IfcPath);
            var cache = new FileInfo(f.CachePath);
            f.CacheExists = cache.Exists;
            if (cache.Exists) f.CacheTimeUtc = cache.LastWriteTimeUtc;

            if (ifc.Exists)
            {
                f.Exists = true;
                f.SizeMb = ifc.Length / 1048576.0;
                f.IfcTimeUtc = ifc.LastWriteTimeUtc;
                f.IfcSchema = ReadIfcSchema(f.IfcPath);
            }
            else if (cache.Exists)
            {
                f.Exists = true;                       // only the cache is left — Revit can still load it
                f.SizeMb = cache.Length / 1048576.0;
                f.IfcSchema = "IFC (source missing)";
            }

            f.CacheFolderWritable = IsFolderWritable(Path.GetDirectoryName(f.IfcPath));
            if (ifc.Exists) f.DiskSync = PlaceholderState(ifc);
        }

        // ---------------------------------------------------------------- placeholder state (V005)
        // Windows marks a cloud-backed file that is not downloaded yet. Autodesk does not document
        // Desktop Connector's behaviour, so anything inconclusive stays NotChecked rather than
        // claiming a file is local when it is not — a false green is worse than an honest blank.
        private const FileAttributes RecallOnOpen       = (FileAttributes)0x00040000;
        private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;
        private const uint InvalidFileSize = 0xFFFFFFFF;

        /// <summary>Downloaded, still a placeholder, or we cannot tell. Never throws.</summary>
        public static AccSyncState PlaceholderState(FileInfo fi)
        {
            try
            {
                if (fi == null || !fi.Exists) return AccSyncState.NotChecked;

                FileAttributes a = fi.Attributes;

                // The unambiguous signals: Windows will have to fetch the bytes on access.
                if ((a & (RecallOnOpen | RecallOnDataAccess | FileAttributes.Offline)) != 0)
                    return AccSyncState.NotDownloaded;

                // A placeholder with no recall flag (e.g. pinned) is still a reparse point —
                // allocated size tells us whether the bytes are actually here.
                long allocated = AllocatedBytes(fi.FullName);
                if ((a & FileAttributes.ReparsePoint) != 0)
                {
                    if (allocated == 0 && fi.Length > 0) return AccSyncState.NotDownloaded;
                    return allocated > 0 ? AccSyncState.Downloaded : AccSyncState.NotChecked;
                }

                // An ordinary file: a non-empty file with nothing allocated is a placeholder.
                if (allocated == 0 && fi.Length > 0) return AccSyncState.NotDownloaded;
                return AccSyncState.Downloaded;
            }
            catch { return AccSyncState.NotChecked; }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetCompressedFileSizeW(string lpFileName, out uint lpFileSizeHigh);

        /// <summary>Bytes actually allocated on disk, or -1 when Windows will not say.</summary>
        private static long AllocatedBytes(string path)
        {
            try
            {
                uint low = GetCompressedFileSizeW(path, out uint high);
                if (low == InvalidFileSize && Marshal.GetLastWin32Error() != 0) return -1;
                return ((long)high << 32) | low;
            }
            catch { return -1; }
        }

        /// <summary>A .rcp is a small index; the scan data is in .rcs files in a "Support" folder beside it.</summary>
        private static double PointCloudSizeMb(string path)
        {
            long bytes = new FileInfo(path).Length;
            if (path.EndsWith(".rcp", StringComparison.OrdinalIgnoreCase))
            {
                string dir = Path.GetDirectoryName(path);
                string name = Path.GetFileNameWithoutExtension(path);
                foreach (var sub in Directory.EnumerateDirectories(dir, name + "*"))
                {
                    if (sub.IndexOf("support", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    bytes += Directory.EnumerateFiles(sub, "*.rcs", SearchOption.AllDirectories)
                                      .Sum(p => new FileInfo(p).Length);
                }
            }
            return bytes / 1048576.0;
        }

        public static string ReadDwgVersion(string path)
        {
            if (!path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)) return "";
            try
            {
                var buf = new byte[6];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (fs.Read(buf, 0, 6) < 6) return "DWG";
                switch (Encoding.ASCII.GetString(buf))
                {
                    case "AC1015": return "AutoCAD 2000";
                    case "AC1018": return "AutoCAD 2004";
                    case "AC1021": return "AutoCAD 2007";
                    case "AC1024": return "AutoCAD 2010";
                    case "AC1027": return "AutoCAD 2013";
                    case "AC1032": return "AutoCAD 2018+";
                    default: return "DWG";
                }
            }
            catch { return "DWG"; }
        }

        public static string ReadIfcSchema(string path)
        {
            try
            {
                if (!path.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase)) return "IFC";
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var sb = new StringBuilder();
                for (int i = 0; i < 40 && !sr.EndOfStream; i++)
                {
                    string line = sr.ReadLine();
                    sb.Append(line);
                    if (line != null && line.StartsWith("DATA;", StringComparison.OrdinalIgnoreCase)) break;
                }
                var m = Regex.Match(sb.ToString(), @"FILE_SCHEMA\s*\(\s*\(\s*'([^']+)'", RegexOptions.IgnoreCase);
                return m.Success ? m.Groups[1].Value.ToUpperInvariant() : "IFC";
            }
            catch { return "IFC"; }
        }

        private static bool IsFolderWritable(string folder)
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return false;
                return (new DirectoryInfo(folder).Attributes & FileAttributes.ReadOnly) == 0;
            }
            catch { return false; }
        }
    }
}
