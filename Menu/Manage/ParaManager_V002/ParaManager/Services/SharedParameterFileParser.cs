using Revit26_Plugin.ParaManager.V002.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.ParaManager.V002.Services
{
    /// <summary>
    /// Parses a Revit shared parameter definition file (tab-delimited .txt, Revit's
    /// standard export format) into SharedParameterInfo records.
    ///
    /// File format reference (Revit standard):
    ///   *META   VERSION MINVERSION
    ///   META    2       1
    ///   *GROUP  ID      NAME
    ///   GROUP   1       Text
    ///   GROUP   2       Dimensions
    ///   *PARAM  GUID    NAME    DATATYPE  DATACATEGORY  GROUP   VISIBLE ...
    ///   PARAM   {guid}  Panel_Reference_Code  TEXT      ...     1       1 ...
    ///
    /// This parser does not touch the Revit API — it is pure file I/O so it can be
    /// unit-tested and run without a live Document.
    /// </summary>
    public static class SharedParameterFileParser
    {
        private const int ParamGuidCol = 1;
        private const int ParamNameCol = 2;
        private const int ParamDataTypeCol = 3;
        private const int ParamGroupIdCol = 5;

        private const int GroupIdCol = 1;
        private const int GroupNameCol = 2;

        /// <summary>
        /// Reads and parses the given shared parameter file.
        /// Throws FileNotFoundException / IOException on file problems — caller
        /// (ViewModel) is responsible for catching and logging as an Error.
        /// </summary>
        public static List<SharedParameterInfo> Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Shared parameter file path is empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Shared parameter file not found.", filePath);

            var groupIdToName = new Dictionary<string, string>();
            var results = new List<SharedParameterInfo>();

            foreach (var rawLine in File.ReadLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;

                var cols = rawLine.Split('\t');
                if (cols.Length == 0) continue;

                switch (cols[0])
                {
                    case "GROUP":
                        if (cols.Length > GroupNameCol)
                            groupIdToName[cols[GroupIdCol]] = cols[GroupNameCol];
                        break;

                    case "PARAM":
                        if (cols.Length > ParamGroupIdCol)
                        {
                            var groupId = cols[ParamGroupIdCol];
                            var groupName = groupIdToName.TryGetValue(groupId, out var name)
                                ? name
                                : "Other";

                            results.Add(new SharedParameterInfo(
                                guid: cols[ParamGuidCol],
                                name: cols[ParamNameCol],
                                dataType: cols[ParamDataTypeCol],
                                parameterGroup: groupName));
                        }
                        break;

                    // "*META", "*GROUP", "*PARAM" header rows and "META" data row intentionally ignored.
                }
            }

            return results;
        }

        /// <summary>Convenience grouping helper used by the popover ViewModel.</summary>
        public static List<IGrouping<string, SharedParameterInfo>> GroupByParameterGroup(
            IEnumerable<SharedParameterInfo> parameters)
            => parameters.GroupBy(p => p.ParameterGroup).OrderBy(g => g.Key).ToList();
    }
}
