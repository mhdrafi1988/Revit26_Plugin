using System.Collections.Generic;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Menu.Ribbon
{
    /// <summary>
    /// Lays a flat list of ribbon items (push buttons)
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
    }
}
