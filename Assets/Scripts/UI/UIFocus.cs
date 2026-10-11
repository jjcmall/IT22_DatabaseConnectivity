using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodeEscape.UI
{
    /// <summary>Keyboard focus helper for uGUI menus.</summary>
    public static class UIFocus
    {
        public static void Select(Selectable selectable)
        {
            if (EventSystem.current == null)
                return;
            EventSystem.current.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
        }

        public static void Clear()
        {
            Select(null);
        }
    }
}
