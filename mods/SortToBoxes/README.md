# [QoL] Shopkeeper's Kit: Sort To Boxes

**English** | [中文](README.zh-CN.md)

> Part of the **Shopkeeper's Kit** (掌柜帮手) series · category **[QoL]** (【便利】): no stat changes and no extra information, it only saves you clicks. See the [repository home](../../README.md) for the series and categories.
> By chaos5090 · developed with assistance from [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding assistant). Mod ID: `chaos5090.sort-to-boxes`.

A code mod for *Probably Stolen* (v0.46D) that takes care of two chores:

1. **Storing**: customers sell you all kinds of things. One key press sends each item into the right box (food box, household goods box, ...).
2. **Selling**: when a buyer arrives, one key press puts everything they would buy onto the counter. Or put one item up yourself and its identical copies follow.

The in-game UI (notifications and the category panel) is Chinese only.

## Keys

| Key | What it does |
|---|---|
| **F7** | Sort loose items in **storage** into matching boxes |
| **F7** while pointing at the **counter** | Put **your** items on the counter back into boxes; items with no matching box go back to storage (customer items are left alone) |
| **Ctrl+F7** while pointing at a box | Open that box's **category panel** (or click the **分类** ("Category") button in an open box window's title bar, just left of the close button ×) |
| **Shift+F7** | Toggle **auto-sort on purchase** (off by default) |
| **F4** | Put everything the current buyer **would buy** onto the counter |
| **F4** while pointing at an item | Put all **identical** items onto the counter |
| **Shift+F4** | Toggle **bring identical items when you place one on the counter** (on by default) |

Every action shows a short notification in the top-left corner. Where each item went is written to the game log `Player.log` (search for `[SortToBoxes]`).

## Storing: sorting rules

Each item is judged by its **first item type**: the first `[type]` shown in its tooltip. For `[Food] [Luxury]` the first type is Food.

1. **Boxes with ticked categories** come first: a box with the item itself ticked wins, then a box with the item's type (first type) ticked.
2. **Untagged boxes** go by what they already hold:
   - a box holding an **identical** item is preferred;
   - otherwise a box holding items with the **same first type** (the more such items, the higher the priority).
3. If the preferred box is full the next one is tried; if nothing fits, the item stays where it is.

So:

- An **empty box** needs either one "sample" item inside, or ticked categories.
- Some items have an unusual first type. For example, the cat chocolate bar is `[Treat] [Food]`, so by default it will not go into the food box. Tick "Treat" in the food box's categories to change that.
- Items without any type (about 120 in the game) can only follow an identical item that is already in a box.
- Decisions are based on what the boxes held **before** the key press; items placed during this run don't change this run's decisions.

### Category panel

Ctrl+F7 while pointing at a box, or the **分类** button left of the close button × in an open box window's title bar, opens a window in the game's own UI style. Items are grouped by type (first type):

- each group starts with **【type】整类** ("whole type"): every item of that type goes into this box, including ones you only see later;
- each item in the group has its own checkbox (with its icon): **only that item**. The ×N after a name is how many you have in storage and boxes;
- **only items you have seen in this save are listed** (anything that has been in your storage, boxes, machines, counters or display case). At first it's just what you own; the list grows as you see more goods, so nothing is spoiled. The record is kept in the save, one per save;
- ticks take effect immediately and are **saved with your save file** (stored on the box itself);
- **全部取消** ("Clear all") switches the box back to "go by what it holds";
- if you uninstall the mod, the leftover marks and records in your save do nothing.

### Box icons

Each box in storage (storage bays, etc.) gets a small badge in the **bottom-right corner** of its sprite, so you can tell what a cabinet holds without opening it:

- a box with items shows the icon of its **first item** (the top-left one); if the box has ticked categories, the first item of a ticked type is preferred;
- an empty box with ticked categories borrows the icon of an item of that type from your stock; if you have none, it shows a small chip in the type's color.

The badge moves with the box, hides while the box is dragged, and never blocks clicks. It works alongside **Show Item Names** (物品名称常显), whose "分" page badge sits in the bottom-left corner. Turn off `boxIcons` if you don't want it.

### What counts as a box

Anything of yours in storage (including boxes inside boxes) that the game classes as storage: storage bays of all kinds, the smuggler compartments, the hypertech bay, machine bays, the toolbox, and all backpacks.

Not boxes: the trash can, the dossier, seed packets, locked supply crates, machines.

## Selling: putting goods on the counter

The counter can hold many items at once, but the customer negotiates **one item at a time** (the last item placed comes first; when it's sold the next one is up). Put everything up once and you only need to click "sell" for each.

- **F4**: put everything the current buyer would buy onto the counter. "Would buy" uses the game's own rule, the same function the game uses to decide whether a customer wants an item. Items the customer can afford go first, more valuable first, so they get the space if the counter fills up.
- **F4 while pointing at an item**: put all identical items from storage and boxes onto the counter, with or without a customer present.
- **Bring identical items** (on by default): while a buyer is at the counter, when you move one item from storage or a box onto the counter (drag it or Shift+click it), its identical copies in your stock follow automatically. Only the move "from your stock to the counter" triggers this; items the game itself moves to the counter, for example when a barter completes, do not.
- When you're done, point at the counter and press **F7**: what's left goes back to boxes or storage.

Never put on the counter: pinned items; equipment the game marks as important (appraisal guides, newspapers, scanners, backpacks, machines, storage bays, ...); items in the display case.

## What never moves

- **Pinned** items (pin anything you want to stay where it is);
- boxes, backpacks and other containers themselves; machines; ship systems;
- items that aren't yours, or that are locked in place;
- when sorting: the item currently being negotiated, and items already inside boxes (only loose items at the top level of storage are sorted).

Items are placed with the game's own placement function, so each box's own rules still apply (e.g. a regular storage bay doesn't take machines).

## Install

1. Copy the whole `dist/SortToBoxes/` folder into the game's `Mods` folder:
   ```
   <game folder>/Mods/SortToBoxes/manifest.xml
   <game folder>/Mods/SortToBoxes/SortToBoxes.dll
   ```
   The game folder is `steamapps\common\Probably Stolen` in your Steam library (in Steam: right-click the game → Manage → Browse local files). To build from source, see the [repository home](../../README.md).
2. Start the game → main menu **改装件** (Mods) → select **【便利】掌柜帮手·归类入箱** → **审阅并启用** (Review and enable) → restart when asked.
3. The mod requests **no special capabilities**. The game's own mod scanner ([`tools/ScanCheck`](../../tools/ScanCheck)) reports no capabilities, no blocks and no findings.

## Settings

In game: Mods screen → select this mod → **设置** (Settings). Or edit `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModConfigs\chaos5090.sort-to-boxes.json`.

| Key | Default | Meaning |
|---|---|---|
| `hotkey` | `F7` | Sort key (Unity KeyCode name). F5/F6/F8/F9 are used by Client Demand Highlight, Smart Inventory Arrange, Cheat Menu and Visitor Forecast |
| `sellHotkey` | `F4` | Counter key |
| `autoOnPurchase` | `false` | Auto-sort goods you buy from customers (also Shift+F7 in game) |
| `autoSameOnCounter` | `true` | Bring identical items when you place one on the counter (also Shift+F4 in game) |
| `boxButtons` | `true` | Show the 分类 button in open box windows' title bar (left of ×) |
| `boxIcons` | `true` | Draw the icon of a box's contents on the box |

## Other mods

- **Smart Inventory Arrange (F6)** only rearranges items inside one box and never moves items between boxes, so the two don't conflict. Sort with F7, then tidy a box with F6.
- **Client Demand Highlight (F5)** outlines the items a customer wants; F4 here puts those items on the counter. Both use the game's same "would the customer buy this" rule.
- **Purchase Stay Counter** keeps bought goods on the counter, so auto-sort on purchase won't fire; point at the counter and press F7 instead.

## Implementation

| File | Contents |
|---|---|
| `src/Stock.cs` | What counts as a box, what can move, the first item type, the extent of your stock; reading/writing box category marks |
| `src/Sorter.cs` | Sorting: tally box contents, rank boxes for each item, place via `GraphUtils.TryAcceptAll` |
| `src/Counter.cs` | Selling: buyer's items / identical items to the counter (`StoreClient.IsClientBuyingThisItem`, `HasBudgetLeftToBuy`), clearing the counter |
| `src/Patches.cs` | Postfix on `PlayerStore.MoveDealItemToInventory` (auto-sort on purchase); prefix/postfix on `SlotMarker.TryAcceptOnce` (detects "stock → counter" moves to bring identical items) |
| `src/BoxLabelPanel.cs`, `src/BoxButtons.cs` | Category panel (the game's CustomUI framework); the 分类 button left of × (a TreeNodeRender on the window, like ×, using the game's mouse events) |
| `src/BoxIcons.cs` | Content badge in each box's bottom-right corner (a child node under the box's render node `GameItemElement.handler`) |
| `src/SeenItems.cs` | Per-save record of items you have seen (`ModHelper.SetModData`, stored in the save) |
| `src/SortBehaviour.cs` | Hotkeys and notifications |
| `src/SortConfig.cs` | Settings via the game's ModConfig |

**Status**: builds and passes the game's mod scanner. 1.1.0 was tried in game: F7 on the counter, the 分类 button and box icons work (the button sat too far from the window and the icon was too big; both changed in 1.2.0). The 1.2.0 changes are **not yet tested in game**. The 分类 button is attached to the box window itself (same parent as the × button); if it looks wrong, turn off `boxButtons` and use Ctrl+F7. If the box icons look wrong, turn off `boxIcons`.

## Changelog

- **1.2.1**: the 分类 button moved into the box window's title bar, right next to the close button × (attached to the window itself, so no more gap).
- **1.2.0**: the category panel now lists items: seen items grouped by type, with icons, tick a whole type or single items; only items seen in this save are listed; the 分类 button sits right against the box window; the box icon is now a small badge in the bottom-right corner.
- **1.1.0**: sorting now uses the first item type; new box category panel and 分类 button; content icons on boxes; new selling helpers (F4 for the buyer's items, F4 on an item for identical items, bring identical items when placing one); F7 on the counter sends items without a matching box back to storage.
- **1.0.0**: first version: F7 sorting (by type overlap), Shift+F7 auto-sort on purchase.
