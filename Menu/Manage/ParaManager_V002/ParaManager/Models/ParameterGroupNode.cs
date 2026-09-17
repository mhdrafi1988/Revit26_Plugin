using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.ParaManager.V002.Models
{
    /// <summary>
    /// One selectable parameter leaf inside the popover's grouped list.
    /// </summary>
    public partial class ParameterLeafNode : ObservableObject
    {
        [ObservableProperty]
        private bool _isChecked;

        public SharedParameterInfo Parameter { get; }

        /// <summary>Back-reference so the leaf can notify its parent group to refresh its own checkbox state.</summary>
        public ParameterGroupNode Parent { get; set; }

        public ParameterLeafNode(SharedParameterInfo parameter)
        {
            Parameter = parameter;
        }

        partial void OnIsCheckedChanged(bool value)
        {
            Parent?.RefreshCheckedStateFromChildren();
        }
    }

    /// <summary>
    /// One Parameter Group header in the popover (e.g. "Text", "Dimensions", "Identity Data").
    /// Its own checkbox is tri-state-like in behavior: checking/unchecking the header
    /// cascades to all children; children can also be toggled individually.
    /// </summary>
    public partial class ParameterGroupNode : ObservableObject
    {
        [ObservableProperty]
        private bool _isChecked;

        /// <summary>Suppresses the cascade-to-children handler while we're updating state FROM the children.</summary>
        private bool _isSyncingFromChildren;

        public string GroupName { get; }

        public ObservableCollection<ParameterLeafNode> Parameters { get; } = new();

        public int ParameterCount => Parameters.Count;

        public ParameterGroupNode(string groupName)
        {
            GroupName = groupName;
        }

        public void AddParameter(ParameterLeafNode leaf)
        {
            leaf.Parent = this;
            Parameters.Add(leaf);
        }

        partial void OnIsCheckedChanged(bool value)
        {
            if (_isSyncingFromChildren) return;

            foreach (var leaf in Parameters)
                leaf.IsChecked = value;
        }

        /// <summary>Called by a child leaf when its own checkbox changes, to keep the group header in sync
        /// (checked only when ALL children are checked; unchecked otherwise — simple two-state, no
        /// indeterminate glyph since WPF CheckBox tri-state adds complexity not requested for this tool).</summary>
        public void RefreshCheckedStateFromChildren()
        {
            _isSyncingFromChildren = true;
            IsChecked = Parameters.Count > 0 && Parameters.All(p => p.IsChecked);
            _isSyncingFromChildren = false;
        }
    }
}
