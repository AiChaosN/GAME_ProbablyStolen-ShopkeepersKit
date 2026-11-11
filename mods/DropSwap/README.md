# [QoL] Shopkeeper's Kit: Drop Swap

**English** | [中文](README.zh-CN.md)

> Part of the **Shopkeeper's Kit** series · category **[QoL]**: no balance changes, it only saves clicks. See the [repository home](../../README.md) for the series and categories.
> By ChaosN · developed with assistance from [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding assistant). Mod ID: `chaos5090.drop-swap`.
>
> Not on the Steam Workshop yet.

UI in Chinese or English, following the game's language (Chinese shows Chinese, every other language shows English).

## What it does

In the base game, swapping a machine's battery or a water pitcher's filter means opening its window, dragging the old one out and the new one in. Dropping the new one on the **closed** machine works only when the slot is empty; when the old one is still inside, the drop is shown in red and nothing happens.

This mod makes that drop work:

- **Batteries**: drop a new battery on a machine → it replaces the battery inside, and the old battery goes where the new one was (same cell, same orientation);
- **Filters**: drop a new filter on a water pitcher, same thing. Other items whose window has a filter slot at the top level work too;
- while you hover the machine with the new part, it is highlighted green with the base game's green "use on" tooltip; release to swap.

Unchanged:

- an empty slot, or any other free spot in the machine that accepts the item, is filled as in the base game;
- base-game drop effects (e.g. dropping a battery on another battery to swap them) are handled by the base game;
- slot rules still apply (e.g. a water pitcher refuses UV filters, so nothing is swapped);
- only your own items, one item at a time; nothing is swapped when you group-drag several items;
- if the old part has a different shape and doesn't fit the new part's old cell, it goes to any free spot in the storage/box the new part came from; if there's no room there either, nothing is swapped and you get a short message.

## Install

1. After building (see "Build from source" on the [repository home](../../README.md)), copy the whole `dist/DropSwap/` folder into the game's `Mods` folder:
   ```
   <game folder>/Mods/DropSwap/manifest.xml
   <game folder>/Mods/DropSwap/DropSwap.dll
   ```
   The DLL is locked while the game runs, so quit the game first.
2. Start the game → main menu **Mods** → select **[QoL] Shopkeeper's Kit: Drop Swap** → **Review and enable** → restart when asked.
3. This mod requests **no special capabilities**; the game's own scanner ([`tools/ScanCheck`](../../tools/ScanCheck)) reports no capabilities, not refused, no findings.

## Settings

Mods screen → select this mod → **Settings**, or edit `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModConfigs\chaos5090.drop-swap.json`.

| Key | Default | Meaning |
|---|---|---|
| `batteries` | `true` | Drop a battery on a machine to swap |
| `filters` | `true` | Drop a filter on a water pitcher to swap |

## How to test

1. Pick a machine that has a battery, with its window closed. Drag a battery from storage onto the machine: hovering highlights the machine in green with the green "use on" tooltip (not the red "can't insert"); after releasing, open the machine: the new battery is inside and the old one sits in the new one's old cell.
2. Same with a water pitcher that has a filter: drop a new filter on it.
3. Machine with an empty battery slot: the battery goes in as in the base game.
4. Battery dropped on another battery: swapped as in the base game.
5. Search `Player.log` for `[DropSwap]`: every swap logs "换件：old → new (machine)".

## Implementation

| File | Contents |
|---|---|
| `src/Swap.cs` | Rules and the swap. Patches: postfixes on `GameItem.MayTarget` / `CanTarget` (count the drop as "use on" when the swap rule holds), prefix on `GameItem.Target` (swap when the base game has no use effect for that pair), postfix on `GameSlotInventory.MayHaveValidInventorySlot` (only while the mouse is dragging that very item: report a slot holding the old part as not insertable, so hovering turns green) |
| `src/DropSwapMod.cs` | Entry point, settings |

The swap uses the game's own `GraphUtils.SwapItemsInPlace` (the same function the base-game battery swap uses). A machine's battery slot comes from the game's `MachineHelper.GetBatterySlot`; other slots are taken from the top level of the item's window (single-slot windows or machine panels), never from items stored inside.

**Status**: builds and passes the game's mod scanner; **not yet tested in game**.

## Changelog

- **1.0.0**: First release: drop a battery on a machine or a filter on a water pitcher to swap.
