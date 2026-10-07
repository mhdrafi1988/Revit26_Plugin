// ==============================================
// File: StyleShortlist.cs
// Layer: UI/ViewModels
// ADDED in V014: the shortlist of line styles or filled region types the
// auto-mapper may use. One instance for lines, one for hatches.
// ==============================================

using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;

namespace Revit26_Plugin.DwgToDetailLines.V014.UI.ViewModels
{
    /// <summary>
    /// A filterable list of project styles with shortlist checkboxes, plus the
    /// names of the checked ones (the choices in each grid row's dropdown).
    /// </summary>
    public partial class StyleShortlist : ObservableObject
    {
        private readonly string _noun;
        private bool _suppress;

        /// <summary>Every project style / type, with a shortlist checkbox.</summary>
        public ObservableCollection<StyleOption> Options { get; } = new();

        /// <summary>Filtered view of <see cref="Options"/> for the list.</summary>
        public ICollectionView OptionsView { get; }

        /// <summary>Names of the shortlisted entries, in list order.</summary>
        public ObservableCollection<string> Names { get; } = new();

        [ObservableProperty] private string filterText = string.Empty;
        [ObservableProperty] private string summary = string.Empty;

        /// <summary>Raised after the checked set changes.</summary>
        public event Action Changed;

        /// <summary>Creates an empty shortlist; <paramref name="noun"/> is used in the summary ("line styles").</summary>
        public StyleShortlist(string noun)
        {
            _noun = noun;
            OptionsView = CollectionViewSource.GetDefaultView(Options);
            OptionsView.Filter = o => string.IsNullOrWhiteSpace(FilterText)
                || (o is StyleOption opt && opt.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Replaces the list; entries named in <paramref name="shortlisted"/> start checked.</summary>
        public void Load(IEnumerable<StyleOption> options, ISet<string> shortlisted)
        {
            foreach (var o in Options)
                o.PropertyChanged -= OnOptionPropertyChanged;
            Options.Clear();

            _suppress = true;
            foreach (var opt in options)
            {
                opt.IsShortlisted = shortlisted.Contains(opt.Name);
                opt.PropertyChanged += OnOptionPropertyChanged;
                Options.Add(opt);
            }
            _suppress = false;

            Rebuild();
        }

        /// <summary>The checked entries, in list order.</summary>
        public List<StyleOption> Checked() => Options.Where(o => o.IsShortlisted).ToList();

        partial void OnFilterTextChanged(string value) => OptionsView?.Refresh();

        private void OnOptionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StyleOption.IsShortlisted) && !_suppress)
                Rebuild();
        }

        // Syncs Names incrementally (not Clear + Add) so row dropdowns whose
        // selection is still shortlisted keep it.
        private void Rebuild()
        {
            var checkedNames = Options.Where(o => o.IsShortlisted).Select(o => o.Name).ToList();

            for (int i = Names.Count - 1; i >= 0; i--)
                if (!checkedNames.Contains(Names[i]))
                    Names.RemoveAt(i);

            for (int i = 0; i < checkedNames.Count; i++)
                if (i >= Names.Count || Names[i] != checkedNames[i])
                    Names.Insert(i, checkedNames[i]);

            Summary = $"{checkedNames.Count} of {Options.Count} {_noun} shortlisted";
            Changed?.Invoke();
        }
    }
}
