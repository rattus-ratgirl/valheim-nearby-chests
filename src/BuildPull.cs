using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace NearbyChests
{
    /// <summary>
    /// With a piece selected on the hammer (or hoe, cultivator...), Ctrl+click pulls that piece's
    /// materials out of nearby chests into your inventory, so you can carry them somewhere out of
    /// crafting range. Each press adds enough for one more piece on top of what you already carry
    /// (PullBuildSets pieces, if you change it). The click doesn't place anything.
    /// </summary>
    internal static class BuildPull
    {
        public static bool ShortcutPressed()
        {
            KeyboardShortcut shortcut = Plugin.PullBuildMaterialsKey.Value;
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey))
                return false;
            // Not KeyboardShortcut.IsDown(): that refuses to fire while any other key is held, and
            // people build while holding W.
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!ZInput.GetKey(modifier))
                    return false;
            }
            return true;
        }

        public static void Pull(Player player)
        {
            Piece piece = player.GetSelectedPiece();
            if (piece == null)
            {
                player.Message(MessageHud.MessageType.Center, "Select a piece to build first");
                return;
            }
            string pieceName = Localization.instance.Localize(piece.m_name);
            var needs = piece.m_resources.Where(r => r.m_resItem != null && r.m_amount > 0).ToList();
            if (needs.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center, $"{pieceName} costs nothing");
                return;
            }

            Inventory inv = player.GetInventory();

            // How many of the piece you could already build from what you carry; then aim one set
            // (or PullBuildSets) beyond that.
            int carriedSets = int.MaxValue;
            foreach (Piece.Requirement r in needs)
                carriedSets = Mathf.Min(carriedSets, Carried(inv, r) / r.m_amount);
            int targetSets = carriedSets + Mathf.Max(1, Plugin.PullBuildSets.Value);

            ChestFinder.Invalidate();
            List<Container> chests = ChestFinder.GetNearby(player, Plugin.CraftingRange.Value);
            var touched = new HashSet<Container>();
            var pulled = new List<string>();
            var missing = new List<string>();
            bool full = false;

            foreach (Piece.Requirement r in needs)
            {
                string name = r.m_resItem.m_itemData.m_shared.m_name;
                int need = r.m_amount * targetSets - Carried(inv, r);
                if (need <= 0)
                    continue;
                int got = Take(inv, chests, name, need, touched, out bool inventoryFull);
                full |= inventoryFull;
                string label = Localization.instance.Localize(name);
                if (got > 0)
                    pulled.Add($"{got} {label}");
                if (got < need && !inventoryFull)
                    missing.Add($"{need - got} {label}");
            }

            if (touched.Count > 0)
            {
                inv.Changed();
                ChestFinder.InvalidateCounts();
                foreach (Container c in touched)
                    InventoryGui.instance.m_moveItemEffects.Create(c.transform.position, Quaternion.identity);
            }

            var lines = new List<string>();
            if (pulled.Count > 0)
                lines.Add($"Pulled {string.Join(", ", pulled.ToArray())} for {pieceName}");
            if (full)
                lines.Add("Inventory full");
            if (missing.Count > 0)
                lines.Add($"Nearby chests are short {string.Join(", ", missing.ToArray())}");
            if (lines.Count == 0)
                lines.Add($"Already carrying enough for {pieceName}");
            player.Message(MessageHud.MessageType.Center, string.Join("\n", lines.ToArray()));
        }

        private static int Carried(Inventory inv, Piece.Requirement r) =>
            inv.CountItems(r.m_resItem.m_itemData.m_shared.m_name);

        /// <summary>Move up to <paramref name="amount"/> of the item from chests (nearest first) into the inventory.</summary>
        private static int Take(Inventory inv, List<Container> chests, string name, int amount,
            HashSet<Container> touched, out bool inventoryFull)
        {
            inventoryFull = false;
            int taken = 0;
            foreach (Container chest in chests)
            {
                if (amount <= 0)
                    break;
                Inventory from = chest.GetInventory();
                if (!from.HaveItem(name, false))
                    continue;
                if (!ChestFinder.EnsureOwner(chest))
                    continue;

                foreach (ItemDrop.ItemData item in from.GetAllItems().ToList())
                {
                    if (amount <= 0 || item.m_shared.m_name != name)
                        continue;
                    if (!inv.HaveEmptySlot() && inv.FindFreeStackSpace(name, item.m_worldLevel) <= 0)
                    {
                        inventoryFull = true;
                        return taken;
                    }

                    // Add a copy of the wanted amount; AddItem shrinks its stack to whatever didn't fit.
                    int want = Mathf.Min(amount, item.m_stack);
                    ItemDrop.ItemData copy = item.Clone();
                    copy.m_stack = want;
                    int placed = inv.AddItem(copy) ? want : want - copy.m_stack;
                    if (placed <= 0)
                    {
                        inventoryFull = true;
                        return taken;
                    }
                    from.RemoveItem(item, placed);
                    touched.Add(chest);
                    taken += placed;
                    amount -= placed;
                }
            }
            return taken;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacement))]
    internal static class Player_UpdatePlacement_BuildPull_Patch
    {
        private static void Prefix(Player __instance, ref bool takeInput)
        {
            if (!takeInput || !Plugin.PullBuildMaterials.Value || __instance != Player.m_localPlayer
                || !__instance.InPlaceMode() || __instance.IsDead() || Hud.IsPieceSelectionVisible()
                || !BuildPull.ShortcutPressed())
                return;

            BuildPull.Pull(__instance);
            // Swallow this frame's input so the click doesn't also place the piece.
            takeInput = false;
        }
    }
}
