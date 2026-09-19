namespace Revit26_Plugin.ParaManager.V003.Models
{
    /// <summary>
    /// Represents a single definition line parsed from a Revit shared parameter (.txt) file.
    /// Immutable data carrier — no Revit API types here so it can be parsed off the main thread.
    /// </summary>
    public class SharedParameterInfo
    {
        /// <summary>GUID string from the shared parameter file (column: GUID).</summary>
        public string Guid { get; }

        /// <summary>Parameter name (column: NAME).</summary>
        public string Name { get; }

        /// <summary>Parameter data type as stored in file (column: DATATYPE), e.g. TEXT, LENGTH.</summary>
        public string DataType { get; }

        /// <summary>Parameter Group this definition belongs to in the file (column: GROUP name resolved via *GROUP rows).</summary>
        public string ParameterGroup { get; }

        public SharedParameterInfo(string guid, string name, string dataType, string parameterGroup)
        {
            Guid = guid;
            Name = name;
            DataType = dataType;
            ParameterGroup = parameterGroup;
        }
    }
}
