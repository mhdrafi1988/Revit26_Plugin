using System.Collections.Generic;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Menu.Ribbon
{
    /// <summary>
    /// Lays a flat list of tool buttons directly on a ribbon panel — no dropdown
    /// menus — as small stacked icons, 3 per column (2 for a trailing pair, a
    /// single full-size button for a trailing odd one out).
    /// </summary>
    internal static class RibbonLayoutHelper
    {
        public static void AddStackedButtons(RibbonPanel panel, IList<PushButtonData> buttons)
        {
            int i = 0;
            while (i < buttons.Count)
            {
                int remaining = buttons.Count - i;
                if (remaining >= 3)
                {
                    panel.AddStackedItems(buttons[i], buttons[i + 1], buttons[i + 2]);
                    i += 3;
                }
                else if (remaining == 2)
                {
                    panel.AddStackedItems(buttons[i], buttons[i + 1]);
                    i += 2;
                }
                else
                {
                    // Lone leftover: shown as a full-size button, so give it a
                    // LargeImage too — otherwise a button added via AddItem
                    // (rather than stacked) renders with a blank icon slot.
                    if (buttons[i].LargeImage == null)
                        buttons[i].LargeImage = buttons[i].Image;
                    panel.AddItem(buttons[i]);
                    i += 1;
                }
            }
        }
    }
}
