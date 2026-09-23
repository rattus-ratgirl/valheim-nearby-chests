# Nearby Chests

Craft, build and fuel stations from the chests around you, and put everything away with one button.
Client-side only: nobody else on the server needs it.

## What it does

- **Craft and build from nearby chests.** Recipes, upgrades and hammer builds count materials in
  chests within 30 m. What you carry is spent first, then the closest chests.
- **Feed stations from chests.** Coal and ore into smelters and kilns, wood into fires, fuel into
  cooking stations. One press adds one item, the same as vanilla. Food and mead stay manual.
- **Stack puts everything away.** Press **Stack** with a chest open, or hold **E** on a chest, and
  each item goes to every nearby chest that already holds it. New items join a chest of similar
  things (metals with metals, hides with hides, same biome), or take an empty chest. Chests that
  received items get merged and sorted.
- **Tidy a messy chest.** The three-bar icon left of *Place stacks* sends anything that doesn't
  belong in the open chest to the right one, and pulls in strays that do belong.
- **Ignore a slot.** Middle-click a slot in your inventory and Stack leaves it alone. A small pin
  marks it. Middle-click again to clear. Marks are saved with your character.
- **Pull build materials.** With a piece selected on your hammer, **Ctrl + left click** pulls its
  materials from nearby chests into your inventory, so you can build out of range. Each press adds
  one more piece's worth.

![Inventory with pinned slots](https://raw.githubusercontent.com/timothydodd/valheim-nearby-chests/main/docs/ignored-slots.png)

Your food, meads, ammo, equipment, hotbar and anything equipped stay with you. The Obliterator,
gravestones, dungeon chests, chests another player has open and chests you can't open are never
touched.

## Settings

Settings are in `BepInEx\config\NearbyChests.cfg` after the first launch. The ones most people
change:

| Setting | Default | What it does |
|---|---|---|
| `CraftingRange` | 30 | How far a chest can be for crafting, building and pulling materials (3–60 m). |
| `StackingRange` | 15 | How far a chest can be for Stack and Tidy (3–60 m). |
| `IncludeCartsAndShips` | false | Also use cart and ship storage. |
| `ExcludeFood`, `ExcludeAmmo`, `ExcludeEquipment` | true | Keep food, ammo and gear in your inventory when stacking. |
| `KeepHotbar` | true | Never stack items from the top row. |
| `PullBuildMaterialsKey` | Ctrl + left click | The key that pulls build materials. |

Which items count as "similar" is a plain text file, `BepInEx\config\NearbyChests.groups.txt`, one
line per group. Edits apply the next time you stack. Delete it to get the defaults back.

## Links

- [Full guide](https://robododd.com/nearby-chests/)
- [Source and issues on GitHub](https://github.com/timothydodd/valheim-nearby-chests)
- [Changelog](https://github.com/timothydodd/valheim-nearby-chests/blob/main/CHANGELOG.md)
