using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NearbyChests
{
    /// <summary>
    /// Slots in the player's inventory that Stack leaves alone. Middle-click a slot in the inventory
    /// window to toggle it; a small pin in the slot's corner shows it's ignored. The mark belongs to
    /// the slot, not the item, so a reserved slot stays reserved whatever you put in it.
    ///
    /// The list is saved with the character (in the player's custom data), so it survives logging out.
    /// </summary>
    internal static class IgnoredSlots
    {
        private const string Key = Plugin.Guid + ".IgnoredSlots";

        private static readonly HashSet<Vector2i> Slots = new HashSet<Vector2i>();
        private static Player _loadedFor;

        public static bool Contains(Player player, Vector2i pos)
        {
            EnsureLoaded(player);
            return Slots.Contains(pos);
        }

        public static bool Toggle(Player player, Vector2i pos)
        {
            EnsureLoaded(player);
            bool ignored;
            if (Slots.Remove(pos))
                ignored = false;
            else
            {
                Slots.Add(pos);
                ignored = true;
            }
            Save(player);
            return ignored;
        }

        private static void EnsureLoaded(Player player)
        {
            if (player == _loadedFor)
                return;
            _loadedFor = player;
            Slots.Clear();
            if (player == null || !player.m_customData.TryGetValue(Key, out string saved) || string.IsNullOrEmpty(saved))
                return;
            foreach (string part in saved.Split(';'))
            {
                string[] xy = part.Split(',');
                if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
                    Slots.Add(new Vector2i(x, y));
            }
        }

        private static void Save(Player player)
        {
            if (player == null)
                return;
            if (Slots.Count == 0)
                player.m_customData.Remove(Key);
            else
                player.m_customData[Key] = string.Join(";", Slots.Select(s => s.x + "," + s.y).ToArray());
        }
    }

    /// <summary>
    /// Wires middle-click on the player's inventory grid and keeps each slot's pin marker in step with
    /// the ignored list. Runs after the game has (re)built the grid's elements.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    internal static class InventoryGrid_UpdateGui_IgnoredSlots_Patch
    {
        private const string MarkerName = "NearbyChests_Ignored";

        private static void Postfix(InventoryGrid __instance, Player player)
        {
            if (!Plugin.IgnoreSlots.Value || InventoryGui.instance == null || __instance != InventoryGui.instance.m_playerGrid)
                return;

            int width = __instance.m_width;
            for (int i = 0; i < __instance.m_elements.Count; i++)
            {
                InventoryElement element = __instance.m_elements[i];
                Image marker = Marker(__instance, element);
                Vector2i pos = new Vector2i(i % width, i / width);
                marker.enabled = player != null && IgnoredSlots.Contains(player, pos);
            }
        }

        /// <summary>The pin image in the slot's top-right corner, created (and middle-click wired) on first sight.</summary>
        private static Image Marker(InventoryGrid grid, InventoryElement element)
        {
            Transform existing = element.transform.Find(MarkerName);
            if (existing != null)
                return existing.GetComponent<Image>();

            var go = new GameObject(MarkerName, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(element.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-3f, -3f);
            rect.sizeDelta = new Vector2(18f, 18f);
            var image = go.GetComponent<Image>();
            image.sprite = PinIcon.Create();
            image.color = new Color(1f, 0.85f, 0.55f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;

            UIInputHandler input = element.GetComponentInChildren<UIInputHandler>();
            if (input != null)
            {
                input.m_onMiddleClick = (Action<UIInputHandler>)Delegate.Combine(input.m_onMiddleClick,
                    new Action<UIInputHandler>(_ => OnMiddleClick(grid, element)));
            }
            return image;
        }

        private static void OnMiddleClick(InventoryGrid grid, InventoryElement element)
        {
            Player player = Player.m_localPlayer;
            if (player == null || !Plugin.IgnoreSlots.Value)
                return;
            int index = grid.m_elements.IndexOf(element);
            if (index < 0)
                return;
            Vector2i pos = new Vector2i(index % grid.m_width, index / grid.m_width);
            bool ignored = IgnoredSlots.Toggle(player, pos);

            ItemDrop.ItemData item = player.GetInventory().GetItemAt(pos.x, pos.y);
            string what = item != null ? Localization.instance.Localize(item.m_shared.m_name) : "This slot";
            player.Message(MessageHud.MessageType.TopLeft,
                ignored ? $"{what}: Stack will leave it alone" : $"{what}: Stack can move it again");
            grid.UpdateGui(player, InventoryGui.instance.m_dragItem);
        }
    }

    /// <summary>Draws the pin marker (a round head on a short stem, leaning right) at runtime.</summary>
    internal static class PinIcon
    {
        private static Sprite _sprite;

        public static Sprite Create()
        {
            if (_sprite != null)
                return _sprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[size * size];
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            // Head: a filled circle in the upper right. Stem: a thick diagonal line down to the
            // lower left, tapering to a point. Texture rows start at the bottom.
            Vector2 head = new Vector2(20f, 20f);
            const float headRadius = 7.5f;
            Vector2 tip = new Vector2(4f, 4f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Vector2.Distance(p, head) - headRadius;

                    // Distance to the stem segment, with a width that shrinks toward the tip.
                    Vector2 ab = head - tip;
                    float t = Mathf.Clamp01(Vector2.Dot(p - tip, ab) / ab.sqrMagnitude);
                    float stemWidth = Mathf.Lerp(0.5f, 2.5f, t);
                    float ds = Vector2.Distance(p, tip + ab * t) - stemWidth;

                    float dist = Mathf.Min(d, ds);
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    if (alpha > 0f)
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            // A darker dot in the head so it reads as a pin, not a blob.
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), head + new Vector2(1f, 1f));
                    if (d < 2.5f)
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(d - 1.5f));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            _sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _sprite;
        }
    }
}
