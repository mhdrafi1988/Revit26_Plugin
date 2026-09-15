using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Menu.Ribbon
{
    /// <summary>
    /// Lays a flat list of ribbon items (push buttons and/or split buttons)
    /// directly on a panel — no dropdown menus — as small stacked icons, 3 per
    /// column (2 for a trailing pair, a single full-size button/split button
    /// for a trailing odd one out).
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
                    else if (items[i] is SplitButtonData splitData && splitData.LargeImage == null)
                        splitData.LargeImage = splitData.Image;
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
        /// Builds the placeholder SplitButtonData for two coexisting versions of
        /// the same tool — one click runs <paramref name="primary"/> (the newer
        /// version); the dropdown arrow reveals <paramref name="secondary"/>
        /// (the older one). Insert the returned data into the list passed to
        /// <see cref="AddStackedButtons"/>, then call <see cref="WireSplitButton"/>
        /// afterward with the same name to finish attaching both versions.
        /// </summary>
        public static SplitButtonData CreateSplitButtonData(string name, string text, PushButtonData primary)
        {
            return new SplitButtonData(name, text)
            {
                Image = primary.Image,
                ToolTip = primary.ToolTip
            };
        }

        /// <summary>
        /// Finishes wiring a SplitButton created via <see cref="AddStackedButtons"/>:
        /// finds it among the returned items by name, attaches both versions
        /// (primary first, so it's the one-click default), and pins the default
        /// to always be primary rather than "whichever was clicked last".
        /// </summary>
        public static void WireSplitButton(IList<RibbonItem> createdItems, string name, PushButtonData primary, PushButtonData secondary)
        {
            var split = createdItems.OfType<SplitButton>().FirstOrDefault(s => s.Name == name);
            if (split == null) return;

            split.AddPushButton(primary);
            split.AddPushButton(secondary);
            split.IsSynchronizedWithCurrentItem = false;
        }
    }
}
