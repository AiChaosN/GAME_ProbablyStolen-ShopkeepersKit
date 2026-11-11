# [Gameplay] Shopkeeper's Kit: Filter Recharge

**English** | [中文](README.zh-CN.md)

> Part of the **Shopkeeper's Kit** series · category **[Gameplay]**: changes a game mechanic and affects balance. See the [repository home](../../README.md) for the series and categories.
> By ChaosN · developed with assistance from [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding assistant). Mod ID: `chaos5090.filter-recharge`.
>
> **Steam Workshop: [subscribe here](https://steamcommunity.com/sharedfiles/filedetails/?id=3837823741)**

UI in Chinese or English, following the game's language (Chinese shows Chinese, every other language shows English).

## What it does

In the base game the **Advanced Water Filter** has 400 durability and recovers 50 at the start of each day. This mod lets you recharge it:

- drop a **water filter** (a regular Water Filter, another Advanced Water Filter, or a Hydroreed Filter) on an Advanced Water Filter: **all** its remaining durability moves over and the source filter is used up;
- the Advanced Water Filter's durability cap rises to **20× a regular filter = 8000** (5 / 10 / 40× in the settings);
- near the cap only what's needed is moved; the rest stays in the source filter;
- no need to open the pitcher: drop it on a **water pitcher holding an Advanced Water Filter** (or any other item with a filter slot at the top of its window);
- hovering shows the green "use Water Filter on Advanced Water Filter" hint; after releasing, a message shows e.g. "Advanced Water Filter durability +400 → 1200/8000".

The used-up source filter stays where it was with 0 durability (as used-up filters do in the base game); trash it if you don't want it.

### Balance

- one Advanced Water Filter holds as much as 20 regular filters, so you swap filters far less often;
- the Advanced Water Filter's daily recovery of 50 still works, all the way up to the new cap (the base game stops at 400);
- price: a fully charged filter sells for the same as a full Advanced Water Filter in the base game, less when not full (e.g. 4000/8000 is about 75% of full price). The game's price formula includes a "cap ÷ default cap" term; this mod raises both together so charging doesn't multiply the price tenfold.

### Unchanged

- only your own items, one at a time; nothing happens when group-dragging;
- Kotton Fabric also works as a filter, but it's a clothing material, so it's never used for charging (no accidental drains); UV Lamps have no durability and are not involved;
- an Advanced Water Filter that was never charged behaves exactly as in the base game.

## With Quick Swap

Both mods can be installed together. When a pitcher holds an **Advanced Water Filter**, dropping a water filter on it **recharges** it (if it's full you get a message; it is never swapped out). When the pitcher holds a regular filter, the drop **swaps** it.

## Install

1. After building (see "Build from source" on the [repository home](../../README.md)), copy the whole `dist/FilterRecharge/` folder into the game's `Mods` folder:
   ```
   <game folder>/Mods/FilterRecharge/manifest.xml
   <game folder>/Mods/FilterRecharge/FilterRecharge.dll
   ```
   The DLL is locked while the game runs, so quit the game first.
2. Start the game → main menu **Mods** → select **[Gameplay] Shopkeeper's Kit: Filter Recharge** → **Review and enable** → restart when asked.
3. This mod requests **no special capabilities**; the game's own scanner ([`tools/ScanCheck`](../../tools/ScanCheck)) reports no capabilities, not refused, no findings.

Uninstalling: filters you already charged keep their durability and cap (they're stored on the item in your save) and then behave as in the base game.

## Settings

Mods screen → select this mod → **Settings**, or edit `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModConfigs\chaos5090.filter-recharge.json`.

| Key | Default | Meaning |
|---|---|---|
| `enabled` | `true` | Drop a water filter on an Advanced Water Filter to recharge it; off = base game |
| `capMultiplier` | `20` | Advanced Water Filter cap = 400 × this (5 / 10 / 20 / 40). Filters already charged to a higher cap keep it |

## How to test

> Back up your saves first: `save_*.es3` in `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\`.

1. Get an Advanced Water Filter (Dr. Jackson sometimes sells one after you unlock the water trader) and a few regular filters.
2. Drop a regular filter on the Advanced Water Filter: hovering is green; after releasing you get a message. Hover the Advanced Water Filter: durability reads e.g. "800/8000".
3. The dropped filter is now at 0 durability.
4. Put the Advanced Water Filter in a water pitcher and drop a regular filter on the pitcher: it recharges the same way.
5. When full, dropping again shows "already full" and changes nothing.
6. Search `Player.log` for `[FilterRecharge]`: each charge logs a "充能：…" line.

## Implementation

| File | Contents |
|---|---|
| `src/Recharge.cs` | Rules and charging. Patches: postfixes on `GameItem.MayTarget` / `CanTarget` (count "water filter → Advanced Water Filter" as a use), prefix on `GameItem.Target` (charge and skip the original; runs before Quick Swap), postfix on `GameSlotInventory.MayHaveValidInventorySlot` (only while the mouse is dragging that very item: report a slot holding the Advanced Water Filter as not insertable, so hovering a pitcher turns green). Durability is set through the game's `DurabilityHelper.SetDurability` (current / cap / default) |
| `src/FilterRechargeMod.cs` | Entry point, settings |

**Status**: builds and passes the game's mod scanner; **not yet tested in game**.

## Changelog

- **1.0.0**: First release: drop a water filter on an Advanced Water Filter (or a pitcher holding one) to recharge it; cap defaults to 20× a regular filter.
