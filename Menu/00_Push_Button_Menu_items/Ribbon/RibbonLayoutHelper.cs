using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Menu.Ribbon
{
    /// <summary>
    /// Lays a flat list of ribbon items (push buttons and/or pulldown buttons)
    /// directly on a panel as small stacked icons, 3 per column (2 for a
    /// trailing pair, a single full-size button for a trailing odd one out).
    /// </summary>
    internal static class RibbonLayoutHelper
    {
        public static IList<RibbonItem> AddStackedButtons(RibbonPanel panel, IList<RibbonItemData> items)
        {
            var created = new List<RibbonItem>();
            int i = 0;
            while (i < items.Count)
            {
                int remaining = items.Count - i;
                if (remaining >= 3)
                {
                    var stacked3 = panel.AddStackedItems(items[i], items[i + 1], items[i + 2]);
                    RebindStackedText(stacked3, items[i], items[i + 1], items[i + 2]);
                    created.AddRange(stacked3);
                    i += 3;
                }
                else if (remaining == 2)
                {
                    var stacked2 = panel.AddStackedItems(items[i], items[i + 1]);
                    RebindStackedText(stacked2, items[i], items[i + 1]);
                    created.AddRange(stacked2);
                    i += 2;
                }
                else
                {
                    // Lone leftover: shown as a full-size button, so give it a
                    // LargeImage too — otherwise a button added via AddItem
                    // (rather than stacked) renders with a blank icon slot.
                    // Image/LargeImage aren't declared on RibbonItemData itself,
                    // so handle the two concrete types this helper is ever given.
                    if (items[i] is PushButtonData pushData && pushData.LargeImage == null)
                        pushData.LargeImage = pushData.Image;
                    else if (items[i] is PulldownButtonData pulldownData && pulldownData.LargeImage == null)
                        pulldownData.LargeImage = pulldownData.Image;
                    created.Add(panel.AddItem(items[i]));
                    i += 1;
                }
            }
            return created;
        }

        /// <summary>
        /// Works around a Revit ribbon bug: a stacked item's label, set via the
        /// Text passed to its Data object's constructor, is frequently dropped
        /// at render time (icon shows, text doesn't) — this only happens inside
        /// AddStackedItems, never on a lone button added via AddItem. Explicitly
        /// re-assigning ItemText on the RibbonItem AddStackedItems just returned
        /// forces the label to actually bind.
        /// </summary>
        private static void RebindStackedText(IList<RibbonItem> createdItems, params RibbonItemData[] sourceData)
        {
            for (int j = 0; j < createdItems.Count && j < sourceData.Length; j++)
            {
                if (sourceData[j] is ButtonData buttonData && !string.IsNullOrEmpty(buttonData.Text))
                    createdItems[j].ItemText = buttonData.Text;
            }
        }

        /// <summary>
        /// Builds the placeholder PulldownButtonData for 2+ coexisting versions
        /// of the same tool, collected under one dropdown button — no version
        /// runs by default on a bare click, the list always shows on click.
        /// <paramref name="primary"/> (the newest version) supplies the button's
        /// own icon. Insert the returned data into the list passed to
        /// <see cref="AddStackedButtons"/>, then call <see cref="WirePulldownButton"/>
        /// afterward with the same name and every version (primary first) to
        /// finish populating the dropdown.
        /// </summary>
        public static PulldownButtonData CreatePulldownButtonData(string name, string text, PushButtonData primary)
        {
            return new PulldownButtonData(name, text)
            {
                Image = primary.Image,
                ToolTip = primary.ToolTip
            };
        }

        /// <summary>
        /// Finishes wiring a PulldownButton created via <see cref="AddStackedButtons"/>:
        /// finds it among the returned items by name and adds every version to
        /// its dropdown list, in the given order (newest/primary first). Only
        /// the first (primary) version should carry an icon on its PushButtonData
        /// — the rest are expected to be text-only in the dropdown.
        /// </summary>
        public static void WirePulldownButton(IList<RibbonItem> createdItems, string name, params PushButtonData[] versions)
        {
            var pulldown = createdItems.OfType<PulldownButton>().FirstOrDefault(p => p.Name == name);
            if (pulldown == null) return;

            foreach (var version in versions)
                pulldown.AddPushButton(version);
        }
    }
}
