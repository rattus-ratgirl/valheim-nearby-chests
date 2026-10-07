using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NearbyChests
{
    internal static class ChestSelection
    {
        internal static readonly int Key = "NearbyChests.Included".GetStableHashCode();

        internal static bool IsMarked(Container c) => c != null && c.m_nview != null
            && c.m_nview.IsValid() && c.m_nview.GetZDO().GetBool(Key, false);

        internal static bool Allows(Container c) => !Plugin.OnlyUseMarkedChests.Value || IsMarked(c);

        internal static bool IsSupported(Container c)
        {
            if (c == null || c.m_nview == null || !c.m_nview.IsValid() || c.GetInventory() == null
                || c.GetComponentInParent<Incinerator>() != null || c.GetComponentInParent<TombStone>() != null)
                return false;
            return c.m_wagon != null || c.GetComponentInParent<Ship>() != null
                || (c.m_piece != null && c.m_piece.IsPlacedByPlayer());
        }

        internal static bool CanEdit(InventoryGui gui, Container c)
        {
            Player player = Player.m_localPlayer;
            return player != null && !player.IsTeleporting() && !player.IsDead()
                && gui != null && gui.m_currentContainer == c && IsSupported(c)
                && c.IsInUse() && c.m_nview.IsOwner()
                && (c.m_privacy != Container.PrivacySetting.Private || c.m_piece != null)
                && c.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID())
                && (!c.m_checkGuardStone || PrivateArea.CheckAccess(c.transform.position, 0f, false));
        }

        internal static bool TrySet(InventoryGui gui, Container c, bool included)
        {
            if (!CanEdit(gui, c))
                return false;

            // Opening already obtained ownership through vanilla's access/busy checks.
            // Never claim it here, save the inventory, or alter its in-use state.
            c.m_nview.GetZDO().Set(Key, included);
            ChestFinder.Invalidate();
            return true;
        }
    }

    // Metadata changes do not invoke Container.OnContainerChanged. Observe received marks directly.
    [HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize))]
    internal static class ChestSelectionDeserializePatch
    {
        private static void Prefix(ZDO __instance, out bool __state) =>
            __state = __instance.GetBool(ChestSelection.Key, false);

        private static void Postfix(ZDO __instance, bool __state)
        {
            if (__state != __instance.GetBool(ChestSelection.Key, false))
                ChestFinder.Invalidate();
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    internal static class ChestSelectionUI
    {
        private static Toggle _toggle;

        private static void Postfix(InventoryGui __instance)
        {
            if (__instance.m_container == null || __instance.m_stackAllButton == null)
                return;
            var row = new GameObject("NearbyChests_Included", typeof(RectTransform), typeof(Toggle));
            var rect = (RectTransform)row.transform;
            rect.SetParent(__instance.m_container.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(12f, 8f);
            rect.sizeDelta = new Vector2(280f, 26f);

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRect = (RectTransform)box.transform;
            boxRect.SetParent(rect, false);
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.sizeDelta = new Vector2(22f, 22f);
            var background = box.GetComponent<Image>();
            var source = __instance.m_stackAllButton.GetComponent<Image>();
            if (source != null)
            {
                background.sprite = source.sprite;
                background.type = source.type;
            }
            background.color = new Color(0.65f, 0.65f, 0.65f);

            TMP_Text template = __instance.m_stackAllButton.GetComponentInChildren<TMP_Text>(true);
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(30f, 0f);
            labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            if (template != null) label.font = template.font;
            label.fontSize = 18f;
            label.color = template != null ? template.color : new Color(1f, 0.85f, 0.55f);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.text = "Include in NearbyChests";

            var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
            var checkRect = (RectTransform)check.transform;
            checkRect.SetParent(boxRect, false);
            checkRect.anchorMin = new Vector2(0.25f, 0.25f);
            checkRect.anchorMax = new Vector2(0.75f, 0.75f);
            checkRect.offsetMin = checkRect.offsetMax = Vector2.zero;
            var checkImage = check.GetComponent<Image>();
            checkImage.color = label.color;
            checkImage.raycastTarget = false;

            _toggle = row.GetComponent<Toggle>();
            _toggle.targetGraphic = background;
            _toggle.graphic = checkImage;
            _toggle.onValueChanged.AddListener(value =>
            {
                Container c = __instance.m_currentContainer;
                ChestSelection.TrySet(__instance, c, value);
                Refresh(__instance);
            });
            row.SetActive(false);
        }

        internal static void Refresh(InventoryGui gui)
        {
            if (_toggle == null) return;
            Container c = gui.m_currentContainer;
            bool visible = ChestSelection.IsSupported(c);
            _toggle.gameObject.SetActive(visible);
            if (!visible) return;
            _toggle.SetIsOnWithoutNotify(ChestSelection.IsMarked(c));
            _toggle.interactable = ChestSelection.CanEdit(gui, c);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    internal static class ChestSelectionUI_Update_Patch
    {
        private static void Postfix(InventoryGui __instance) => ChestSelectionUI.Refresh(__instance);
    }
}
