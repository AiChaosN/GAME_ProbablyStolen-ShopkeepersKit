# [Fix] Shopkeeper's Kit: Counter Overflow

**English** | [中文](README.zh-CN.md)

> Part of the **Shopkeeper's Kit** (掌柜帮手) series · category **[Fix]** (【修复】): fixes a game bug, with a small effect on balance. See the [repository home](../../README.md) for the series and categories.
> By chaos5090 · developed with assistance from [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding assistant). Mod ID: `chaos5090.counter-overflow`.

The in-game UI is Chinese only.

## The bug

When a seller (thieves, water sellers, the retired chemist, Dr. Jackson offering equipment, ...) visits, they lay their goods on the counter one by one. In the base game:

- if an item **doesn't fit** the counter's free space, it is **destroyed**;
- if the counter has **no free cell at all**, the remaining items are **never created**.

There is no message and you never see those items. The game log records it (`Destroyed unplaced items ... (front counter full)`); a single equipment offer from Dr. Jackson can lose twenty or more items.

The Workshop mod "防止小偷箱子被挤掉" only moves the thief's loot crate to the front of the line; everything else can still be lost. This mod stops all of it.

## How it works

Items that don't fit wait in a **queue** instead:

- as soon as space frees up (you buy an item or move one away), queued items **fill in automatically**;
- press **F3** to **flip**: the customer's items you haven't bought go to the back of the queue and the next batch comes out. Keep pressing to cycle through everything;
- while items are queued, a notification tells you how many are waiting;
- **when the customer leaves**, queued items are handled like leftover counter items in the base game: the customer's items leave with them (destroyed), anything that belongs to you (e.g. story gifts) goes to your storage.

Unchanged: prices, negotiation and appraisal all happen on the real items, exactly as in the base game.

### Balance

You will see goods the base game would have destroyed, such as thieves' loot crates at high Black Market reputation or Dr. Jackson's full equipment offer, so you get more buying opportunities than in the base game. That's why this mod is [Fix] rather than [QoL].

### Notes

- A **wholesale** offer (one price for everything on the counter) only covers items currently shown, not queued ones.
- Queued items are not saved if you save and reload while items are waiting (in the base game they would have been destroyed anyway).

## Install

1. Copy the whole `dist/CounterOverflow/` folder into the game's `Mods` folder:
   ```
   <game folder>/Mods/CounterOverflow/manifest.xml
   <game folder>/Mods/CounterOverflow/CounterOverflow.dll
   ```
   To build from source, see the [repository home](../../README.md).
2. Start the game → main menu **改装件** (Mods) → select **【修复】掌柜帮手·柜台不丢货** → **审阅并启用** (Review and enable) → restart when asked.
3. The mod requests **no special capabilities**. The game's own mod scanner ([`tools/ScanCheck`](../../tools/ScanCheck)) reports no capabilities, no blocks and no findings.

## Settings

In game: Mods screen → select this mod → **设置** (Settings). Or edit `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModConfigs\chaos5090.counter-overflow.json`.

| Key | Default | Meaning |
|---|---|---|
| `enabled` | `true` | Queue items that don't fit; off = base game behavior |
| `hotkey` | `F3` | Flip key (Unity KeyCode name) |
| `testMode` | `false` | Test mode, see below |

## Testing

The overflow only happens naturally with certain sellers, so the mod has a **test mode** that triggers it on demand.

> Back up your saves first: the `save_*.es3` files in `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\`. Ideally test on a new or unimportant save.

1. Mods → this mod → Settings → turn on **测试模式** (test mode).
2. Load a save, open the shop and wait for any customer to reach the counter (a seller is best, so the items can be bought).
3. Press **Ctrl+F3**: the customer lays out an extra batch of large equipment (fridge, water tank, moisture farm, water purifier, ...) through the normal game code. The counter will overflow.
4. Check:
   - a notification says items are queued;
   - buy or move an item: the freed space fills with a queued item;
   - press **F3**: the counter flips to the next batch;
   - after closing, open the game log `Player.log` (same folder) and search for `[CounterOverflow]`: you should see queue, flip and "customer left, cleared the queue" entries, and **no new** `(front counter full)` destroy entries.
5. Turn test mode off afterwards. The test items are real and can be bought, which amounts to cheating.

Without test mode: Dr. Jackson's equipment offers and thieves at Black Market reputation 100+ overflow the counter easily.

## Implementation

| File | Contents |
|---|---|
| `src/Overflow.cs` | Patches (`GraphUtils.DestroyIfUnplaced` queues instead of destroying for "front counter full"; `PlayerStore.TryAddDirectSellingItemToTable(string, …)` still creates the item when the counter is full), queue, refill, flip, cleanup when the customer leaves, test mode |
| `src/OverflowBehaviour.cs` | Queue check every 0.2 s, hotkeys, notifications |
| `src/OverflowMod.cs` | Entry point and settings |

Queued items are parked in an invisible grid (not left dangling), so their state stays valid. Both patches run last: if another mod (e.g. Visitor Forecast simulating in its sandbox) asks to skip the original logic, this mod does nothing.

**Status**: builds and passes the game's mod scanner; **not yet tested in game**.

## Changelog

- **1.0.0**: first version.
