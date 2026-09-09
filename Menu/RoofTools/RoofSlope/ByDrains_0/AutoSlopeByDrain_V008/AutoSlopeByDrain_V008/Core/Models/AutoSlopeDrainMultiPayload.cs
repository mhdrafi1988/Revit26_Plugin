// File: AutoSlopeDrainMultiPayload.cs
// Location: Core/Models/
//
// NEW (V008), per Rafi's confirmed multi-roof decision (2026-09-08).
// AutoSlopeDrainPayload itself stayed per-roof (RoofId + SelectedDrains +
// TotalDetectedCount + the shared slope/threshold/marker/export settings,
// copied identically onto every roof's payload). This wrapper carries the
// full batch of per-roof payloads across the ExternalEvent boundary as one
// unit, so AutoSlopeDrainHandler can run every roof inside a single
// TransactionGroup (one combined Undo entry) and report back once with
// every roof's result.

using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.MultiRoofSlopeByDrain.Core.Models
{
    public class AutoSlopeDrainMultiPayload
    {
        /// <summary>One payload per roof the user ran — each carries its own selected drains but shares slope/threshold/marker/export settings.</summary>
        public List<AutoSlopeDrainPayload> RoofPayloads { get; set; }

        /// <summary>Shared log callback, used for messages that apply to the whole batch (e.g. the combined summary export) rather than one roof.</summary>
        public Action<LogEntry> Log { get; set; }

        /// <summary>
        /// When true and more than one roof actually ran, the Handler writes one
        /// extra Excel workbook after the batch comparing every roof's result
        /// (longest path, highest elevation, drain count) — in addition to each
        /// roof's own per-roof workbook, which AutoSlopeDrainEngine already
        /// writes per RoofPayloads entry.
        /// </summary>
        public bool WriteCombinedSummary { get; set; }

        public string ExportFolderPath { get; set; }

        public string ProjectTitle { get; set; }

        /// <summary>Called exactly once, after every roof in RoofPayloads has been processed (success or failure) and the combined summary (if any) has been written.</summary>
        public Action<List<AutoSlopeDrainRoofResult>> OnAllCompleted { get; set; }
    }

    /// <summary>One roof's outcome within a multi-roof run.</summary>
    public class AutoSlopeDrainRoofResult
    {
        public string RoofName { get; set; }
        public long RoofElementId { get; set; }
        public AutoSlopeDrainResult Result { get; set; }
    }
}
