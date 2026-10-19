using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkVersionChecker_V005.Core.Models;

namespace Revit26_Plugin.LinkVersionChecker_V005.Services
{
    /// <summary>What one run of the cloud status check found.</summary>
    public sealed class CloudSyncResult
    {
        public int Checked, Current, OutOfDate, Unknown, Failed;
        public bool Cancelled;

        public void Count(AccSyncState s)
        {
            Checked++;
            switch (s)
            {
                case AccSyncState.Current: Current++; break;
                case AccSyncState.OutOfDate: OutOfDate++; break;
                case AccSyncState.Failed: Failed++; break;
                default: Unknown++; break;
            }
        }

        public string Summary =>
            Checked == 0
                ? "No ACC cloud links to check in this model"
                : $"Checked {Checked} ACC link{(Checked == 1 ? "" : "s")} | {Current} current | {OutOfDate} out of date | " +
                  $"{Unknown} unknown | {Failed} failed" + (Cancelled ? "  (cancelled — the rest stay \"Not checked\")" : "");
    }

    /// <summary>
    /// Asks ACC whether each cloud link is the current version.  [V005]
    ///
    /// Uses <c>ExternalResourceReference.GetResourceVersionStatus()</c>, which needs an open
    /// model, a signed-in session and the network, so this only ever runs In-Model and only
    /// when the user presses the button — the cost per link is undocumented and may be a
    /// network round trip. Every call is wrapped: a failure degrades to Failed or Unknown and
    /// never interrupts the run or shows a dialog.
    ///
    /// Revit API calls must stay on Revit's main thread. The window is modal, so every await
    /// here resumes on that thread; <see cref="ScanReporter"/> yields just long enough for the
    /// progress text to repaint and Cancel to be clickable.
    /// </summary>
    public static class CloudSyncProbe
    {
        /// <summary>True for a row this check can actually answer: a live pure-cloud link.</summary>
        public static bool CanCheck(LinkInfo l) =>
            l != null && l.InModel && l.RefId != null && LinkSourceInfo.IsCloudPath(l.Source);

        /// <summary>
        /// Sets the starting state of every cloud row after a scan: ready to be checked
        /// In-Model, honestly unavailable in Pre-Open / Folder Audit. Disk-derived states
        /// (Desktop Connector, OneDrive) are left as the file probe found them.
        /// </summary>
        public static void Initialise(IEnumerable<LinkInfo> links, bool inModel)
        {
            foreach (var l in links ?? Enumerable.Empty<LinkInfo>())
            {
                if (!LinkSourceInfo.IsCloudPath(l.Source)) continue;
                if (AccSyncInfo.IsResolved(l.Sync)) continue;          // already answered — keep it
                l.SetSync(inModel && l.RefId != null ? AccSyncState.NotChecked : AccSyncState.NotAvailableInMode,
                          l.CloudVersion, "");
            }
        }

        public static async Task<CloudSyncResult> CheckAsync(Document doc, IList<LinkInfo> links, ScanReporter rep)
        {
            var res = new CloudSyncResult();
            var todo = (links ?? new List<LinkInfo>()).Where(CanCheck).ToList();

            try
            {
                for (int i = 0; i < todo.Count; i++)
                {
                    LinkInfo l = todo[i];
                    await rep.StepAsync(i, todo.Count, $"Checking cloud status {i + 1}/{todo.Count}: {l.Name}");
                    Apply(doc, l);
                    res.Count(l.Sync);
                }
            }
            catch (OperationCanceledException)
            {
                res.Cancelled = true;      // rows not yet reached keep "Not checked"
            }

            rep.Report(todo.Count, todo.Count, res.Summary);
            return res;
        }

        /// <summary>Reads one link's version status. Never throws.</summary>
        public static void Apply(Document doc, LinkInfo l)
        {
            if (l == null) return;
            try
            {
                Element e = doc?.GetElement(l.RefId);
                if (e == null) { l.SetSync(AccSyncState.Failed, "", "the link type is no longer in the model"); return; }

                IDictionary<ExternalResourceType, ExternalResourceReference> refs = null;
                try { refs = e.GetExternalResourceReferences(); } catch { /* handled below */ }
                if (refs == null || refs.Count == 0)
                {
                    l.SetSync(AccSyncState.Failed, "", "Revit exposes no external resource reference for this link");
                    return;
                }

                foreach (var kv in refs)
                {
                    ExternalResourceReference err = kv.Value;
                    if (err == null) continue;

                    bool valid = true;
                    try { valid = err.IsValidReference(kv.Key); } catch { /* assume valid, the status call will tell us */ }
                    if (!valid) { l.SetSync(AccSyncState.Failed, "", $"the reference is not valid for {kv.Key}"); return; }

                    string version = "";
                    try { version = err.Version ?? ""; } catch { /* optional */ }

                    ResourceVersionStatus status;
                    try { status = err.GetResourceVersionStatus(); }
                    catch (Exception ex) { l.SetSync(AccSyncState.Failed, version, ex.Message); return; }

                    AccSyncState state =
                        status == ResourceVersionStatus.Current   ? AccSyncState.Current :
                        status == ResourceVersionStatus.OutOfDate ? AccSyncState.OutOfDate :
                                                                    AccSyncState.Unknown;

                    l.SetSync(state, version, $"ResourceVersionStatus.{status}");
                    return;                                        // one answer per link type
                }

                l.SetSync(AccSyncState.Failed, "", "no usable resource reference on this link type");
            }
            catch (Exception ex)
            {
                l.SetSync(AccSyncState.Failed, "", ex.Message);
            }
        }

        /// <summary>
        /// Everything Revit will tell us about one cloud reference, for the clipboard.
        /// The ACC key names in GetReferenceInformation() are undocumented and vary, so this is
        /// how we learn the real ones from a real project instead of guessing.
        /// </summary>
        public static string Dump(Document doc, LinkInfo l)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Link Version Checker V005 — cloud reference information");
            sb.AppendLine($"Link      : {l?.Name}");
            sb.AppendLine($"Source    : {l?.SourceText}");
            sb.AppendLine($"Path      : {l?.FullPath}");
            sb.AppendLine($"Sync      : {l?.SyncText}  ({l?.SyncDetectedBy})");
            sb.AppendLine($"Read at   : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            try
            {
                Element e = doc?.GetElement(l.RefId);
                if (e == null) { sb.AppendLine("The link type is no longer in the model."); return sb.ToString(); }

                IDictionary<ExternalResourceType, ExternalResourceReference> refs = e.GetExternalResourceReferences();
                if (refs == null || refs.Count == 0) { sb.AppendLine("No external resource references."); return sb.ToString(); }

                foreach (var kv in refs)
                {
                    ExternalResourceReference err = kv.Value;
                    sb.AppendLine($"--- ExternalResourceType: {kv.Key} ---");
                    if (err == null) { sb.AppendLine("  (null reference)"); continue; }

                    Try(sb, "InSessionPath", () => err.InSessionPath);
                    Try(sb, "ServerId", () => err.ServerId.ToString());
                    Try(sb, "Version", () => err.Version);
                    Try(sb, "VersionStatus", () => err.GetResourceVersionStatus().ToString());
                    Try(sb, "ShortDisplayName", () => err.GetResourceShortDisplayName());
                    Try(sb, "HasValidDisplayPath", () => err.HasValidDisplayPath().ToString());

                    sb.AppendLine("  GetReferenceInformation():");
                    try
                    {
                        IDictionary<string, string> info = err.GetReferenceInformation();
                        if (info == null || info.Count == 0) sb.AppendLine("    (empty)");
                        else foreach (var pair in info) sb.AppendLine($"    {pair.Key} = {pair.Value}");
                    }
                    catch (Exception ex) { sb.AppendLine($"    <failed: {ex.Message}>"); }
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"<failed: {ex.Message}>");
            }
            return sb.ToString();
        }

        private static void Try(StringBuilder sb, string label, Func<string> read)
        {
            try { sb.AppendLine($"  {label} = {read()}"); }
            catch (Exception ex) { sb.AppendLine($"  {label} = <failed: {ex.Message}>"); }
        }
    }
}
