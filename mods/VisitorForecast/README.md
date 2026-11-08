# [Info] Shopkeeper's Kit: Visitor Forecast

**English** | [中文](README.zh-CN.md)

> Part of the **Shopkeeper's Kit** (掌柜帮手) series · category **[Info]**: changes no stats, but tells you in advance who will visit today, which makes the game somewhat easier. See the [repository home page](../../README.md) for the series and its categories.
> By chaos5090 · developed with assistance from [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding assistant). Mod ID: `chaos5090.visitor-forecast`.

**The in-game UI is Chinese only.**

A code mod for *Probably Stolen* (v0.46D, Unity 2021.3 Mono). **Each morning before you open the shop**, press **F9**: the mod runs the game's opening-time customer generation a few hundred times in a "sandbox" and tells you, for today:

- who **must come** (100%);
- **other visitors**: people who may or may not come, grouped by **source** (random customers, information brokers, new faces, reputation perks…), each with their own chance of showing up;
- who is **waiting at the door in the morning** (the people who stand right at the counter as soon as you open, without queuing — e.g. Lou or Security officers);
- for each person: **faction, what they're here for (buy / sell / buy & sell / talk / inspect…), what kind of goods they want (for sellers, what they're selling), budget range, source (story / weekly schedule / display-case buyer / random customer…), and roughly where they'll be in the queue**;
- the expected total number of visitors today (mean, plus min/max).

After each forecast the mod **automatically verifies** that the game state is exactly the same as before. Any mismatch triggers a red alarm, and further forecasts are refused until you reload (see [Safety net](#safety-net)).

> The forecast is based on your display case, reputation, events and save state **at the moment you press the key**. If you change the display case or sign, press F9 again (or **Re-forecast** (重新预测) in the window).

---

## Installation

1. Copy the whole `dist/VisitorForecast/` folder into the `Mods` folder inside the game directory, so you end up with:
   ```
   <game dir>/Mods/VisitorForecast/manifest.xml
   <game dir>/Mods/VisitorForecast/VisitorForecast.dll
   ```
   The game directory is `steamapps\common\Probably Stolen` in your Steam library (in Steam, right-click the game → Manage → Browse local files).
   Under WSL you can also just run `./build.sh install` (it only copies files; it does not launch the game).
2. Launch the game, open the **Mods** screen from the main menu, select **【情报】掌柜帮手·访客预测 | [Info] Shopkeeper's Kit: Visitor Forecast**, and click **Review & Enable** (审阅并启用).
3. Since 1.3.0 the mod **requests no special permissions** (the game's built-in scanner reports "no permissions, no findings" for it; 1.2 and earlier needed `reflection-external`).
   The mod reads and writes no files, makes no network connections, launches no programs and doesn't touch Steam.
4. **Restart the game** when prompted; the mod takes effect after the restart.

> Whenever you replace the DLL or manifest later (e.g. to update), the game treats the mod as "changed" and you need to click Review & Enable (审阅并启用) again.

## Usage

- Load a save and press **F9** **in the morning, before you raise the shutter**: the **Visitor Forecast** (访客预测) tablet pops up, shows **Forecasting…** (正在预测…) at first, and has results after one to a few seconds. Forecasting again on the same day with nothing changed gives essentially the same result (the random seed is derived from the day number).
- To close the window, press F9 again, click the ✕ in the tablet's top-right corner, or click **Close** (关闭) at the bottom; **Re-forecast** (重新预测) reruns it immediately. You can drag the window around (the list area works as a handle too), and while the mouse is over the window, clicks don't pass through to the goods or counter behind it. Esc usually closes it as well, like the game's other tablets.
- Press F9 **after opening**: the title changes to **Actual queue** (实际队列) and shows the game's **real queue**, refreshed every second. This is live data, not a forecast.
- After closing time (evening), or before you've loaded a save, F9 just shows a one-line message in the window.
- The hotkey doesn't fire while you're typing (e.g. in a text field).

> Note: with the game's developer tools enabled, F9 also opens the game's built-in debug panel; in that case, rebind the hotkey to another key.

### Window contents

The window uses the game's own tablet UI (the same frame, colors and Chinese font as the end-of-day and negotiation tablets). From top to bottom:

1. **Title**: `访客预测` (Visitor Forecast); after opening, `实际队列` (Actual queue).
2. **Info line**: `第 N 天 · 星期X` (Day N · weekday). The weekday follows the game calendar: day % 7, 0 = Sunday … 6 = Saturday.
3. **Two stat boxes**: `必来 N 人` (N must come) and `其他 a–b 人` (a–b others: visitors besides the must-comes, min–max; hover to see the mean).
4. **List** (scrollable):
   - If the safety net raises an alarm, a **red banner** sits at the very top; extra notes (e.g. "N simulations failed" (有 N 次模拟出错), "the Revolution will plant a bomb on the counter" (革命军会在柜台放炸弹)) appear below it.
   - **Must come** (必来): one row per person, sorted by queue position; people **waiting at the door in the morning** come first, with a green `门口等候` (Waiting at door) tag. If there's nobody, it shows "No one is certain to come today" (今天没有一定会来的人).
   - **Other visitors** (其他客人): grouped by source. Each group header shows the source and its expected head count (e.g. `随机顾客 · 约 1.8 人` = Random customer · ~1.8 people), and groups are sorted by expected count, highest first. Within a group, people are sorted by probability, highest first, with a probability bar and percentage on the right. Anyone below 3% is folded into a single row `其他 N 种（各 <3%）` (N others, each <3%); hover over it to see the full list.
5. **Footer**: number of simulations and time taken, plus usage hints; below it are the **Re-forecast** (重新预测) / **Close** (关闭) buttons.

What each customer row shows:

| Where | What |
|---|---|
| Line 1, left | Name (a trailing `等` ("etc.") means this kind of customer had different names in different simulations; `×2` = two of them came in the same run) + tags: `门口等候` Waiting at door (green); `改造人` Augmented / `可能是改造人` Possibly augmented / `通缉` Wanted (red); `插队` Cuts in line; **faction** (in each faction's in-game color: Lower Levels (下层区), Upper Levels (上层区), Security (治安部), Black Market (黑市), Revolution (革命军), District 13 Cartel (13区卡特尔); other factions in gray) |
| Line 1, right | Must come: queue position (`第 2–4 位` = positions 2–4, the 10th–90th percentile; people waiting at the door show `柜台前` (At the counter)); Other visitors: probability bar + percentage; Actual queue: `柜台前` (At the counter) / `排第 k 位` (k-th in line) |
| Line 2 | What they're here for, as small green tags: Buy (买), Sell (卖), Buy & sell (买卖), Talk (对话), Inspect (检查), Barter (以物易物), Collect rent (收租), Sell information (卖情报), Place an order (委托订货)… + **Wants** (想要): for buyers, the item category they want (the game's Chinese category name) or a specific item; for sellers, `卖：…` (Sells: …), taken from the stock lists compiled in the stats handbook, or `卖货` (Selling goods) when not listed. Long text is cut off at three lines |
| Line 3 | Budget (the min–max seen across all simulations, already including bonuses such as reputation perks and shop attractiveness) · source (shown only under Must come; Other visitors are already grouped by source) |

Hover over a row to pop up that customer's full details (including any "Wants" text that was cut off).

The source is the generation step that produced the customer: Story (剧情), Tutorial story (教程剧情), Weekly schedule (每周日程), Display-case buyer (展示柜买家), Signs & certificates (招牌与证书), Random customer (随机顾客), Reputation perk (声望特权), Market shortage/surplus (市场短缺过剩), Market event (市场事件), Wild's services (王尔德服务), Wanted criminal (通缉犯), Augmented (改造人), Security inspection (治安检查), Information broker (情报贩子), Inventor (发明家), New face (新面孔), Chemical feedback (化学品反馈), Scheduled return visit (预约回访), Rent day (房租日), Already in queue (已在队列), Waiting at the door in the morning (早上在门口等)…

> If the native window can't be built (e.g. at the main menu, after a game update changed the UI structure, or with `nativeUI` turned off in the settings), the mod automatically falls back to the simple window from 1.1 (system font, same content, other visitors likewise grouped by source) and logs the reason.

## Configuration

The mod uses the game's built-in mod settings and doesn't put a config file next to the DLL (so it doesn't need the file read/write permission):

- In game: on the **Mods** screen, select this mod → **Settings**;
- Or edit `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModConfigs\chaos5090.visitor-forecast.json` directly (created automatically on first run).

| Key | Default | Description |
|---|---|---|
| `hotkey` | `F9` | Unity KeyCode name, e.g. `F7`, `F10`, `Insert`, `Home` (default was F8 in 1.1 and earlier) |
| `runs` | `300` | Simulations per forecast (pick from 100–2000 in a dropdown). More runs are more accurate but stall the game longer when you press the key |
| `includeOtherMods` | `false` | Whether the sandbox also runs other mods' code hooked into customer generation (see [Limitations](#limitations-and-caveats)) |
| `nativeUI` | `true` | Use the game's native tablet window; turn off to use the old simple window |

## How it works

When the shop opens, the game runs `PlayerStore.OnShutterOpened()`: `CheckCrime()` (settles your crime record, which affects security inspections) → `SoldItem.HandleSoldItem()` (chemical feedback, which can bring customers back to complain) → `StoreClientManager.GenerateClient()` (about 30 `Handle*` steps) → `HandleFutureClientQueue()` (scheduled return visits). After that, `OnLateShutterOpen()` arranges the people waiting at the door.

Each forecast:

1. **Snapshot**: starting from the **static fields of game types** in Assembly-CSharp, do a breadth-first traversal of every reachable game data object (`PlayerStore`, `StoreClientManager`, `StoreClientData`, `SecData`, reputation, network upgrades, events, static caches…) and the List/Dictionary/HashSet/Queue/arrays inside them, recording each object's **own field values** (shallow copy; references are stored as-is). The item/inventory/window UI graph and Unity objects are not snapshotted; they're handled separately (see below).
   - Which fields each game type has comes from a **type table generated at build time** (`src/Generated/GameTypeRegistry.cs`, produced by `tools/RegistryGen` from the game's `Assembly-CSharp.dll`): one `typeof(X).GetFields(...)` line per type, so the receiver of every field query is a literal `typeof(<game type>)`. At runtime the mod only looks up the table by an object's `GetType()` and never runs member queries on computed types — which is why it doesn't need the `reflection-external` permission.
   - Collections are read and written only through public interfaces: List/arrays via `IList` (only changed positions are touched; extra items are removed and missing ones appended, never a full clear), Dictionary via `IDictionary` (if the keys are unchanged, only values are updated; if the sandbox only added keys, they're removed in reverse order; otherwise it's rebuilt in snapshot order), HashSet/Queue/Stack via strongly typed restorers generated for the element types that appear in game fields, and ValueTuple items are read via `ITuple`. **Contents and enumeration order are always restored exactly**; internal details such as collection capacity are no longer restored bit-for-bit (see [Limitations](#limitations-and-caveats)).
   - Objects not in the type table are treated as opaque references: the game's **private nested types** (about 40, nearly all UI-related, such as `NegociationUIManager.CachedRow` and `CustomUIDebugPanel.PanelDef`, plus the read-only constant table `ShareholderHelper.Dividend`) and compiler-generated types (lambda caches, closures). None of them take part in customer generation.
2. **Run the sandbox N times**: each run reseeds `UnityEngine.Random` with a different seed and replaces the `System.Random` instances in the game's writable fields (`RNG.random` etc.) with fresh stand-ins, so the originals' internal state is never consumed and never needs to be read or restored. The game also has two `static readonly` `System.Random` instances (`BetaGenerator`, `BoxMullerGenerator`) that can't be replaced, but game code never calls them, so they affect neither the forecast nor your save. The mod then calls the game's own `OnShutterOpened()` directly, records the generated queue, and works out who's waiting at the door. After each run it destroys the GameObjects of items/windows created during that run, then writes the snapshot **back into the same objects** (only fields/collections that changed). If full checks get too slow, it automatically switches to "hot nodes + a full pass every 16 runs", and the final write-back is always a full one.
3. **Side effects intercepted during the sandbox** (Harmony prefixes that pass straight through at other times): putting things on the counter (`TryAddDirectSellingItemToTable` etc.), destroying existing items (`GameItemElement.Destroy`, e.g. clearing the sold area), changing tags/features of existing items (`EnableTag/DisableTag/ModifyTag/AddItemFeature`, `StolenHelper.SetHeat`), `PlantList.FreeNutrifruit`, reputation popups (`ReputationUIManager.NotifyReputation/Notify`), Steam achievements (`AchievementTracker.Unlock/IncrementCountAndUnlock`), dialogs (`DialogUIManager.StartDialogue`), and counter item refresh (`RefreshCounterItem`); by default, other mods' generation hooks aren't run either. Unity logging is turned off for the duration so hundreds of generations don't flood Player.log.
4. Restore `UnityEngine.Random.state` and turn logging back on.

### State that customer generation was found to modify (all covered by the snapshot)

`StoreClientManager`: `clientStack`, `clientTracker` (cooldowns), `chemicalFeedbackStack`, `daySinceInformationClient`, `dayUntilInspection`/`inspectionSeeded`, `isTodayWantedQueued`/`daySinceWantedWasQueued`, `gunBuyerDaysMissed*`, `appraisalDaysMissed`, `wealthHistory`/`wildBailoutGiven`, `isAugSpawnHandledToday`; `PlayerStore`: the future visit queue (`futurStoreClientIdQueue/DayQueue`), `unseenMinorClient`, `nextAugSpawnDay`/`isAugEnded`, `daySinceLastBrowseUpgrade`, `displayCaseSetupMissedDays`, assorted `*Introduced`/`*Shown` story flags, `soldItemsTracker`, `currentUniqueId` (IDs for newly created items), `lockShutterButtonFlag`; `StoreClientData`: story fields (Lou, Eleanor, John Smith, the Collector, the business license bribe amount, etc.); `SecData`: crime record and tracking values; expedition hireling state (`Hireling.Rest()`); reputation (the crime network service gives Black Market +2 during generation); the static `ShowcaseHelper.pendingSupplierPicks`, `GraphHandler.windowGraphNodeRoots`, and so on.

On top of that, many customers' factory methods create new items **at creation time** via calls like `DirectoryMaster.Item("$")` (dialog-option icons), and each item instantiates a dozen or so GameObjects. The sandbox tracks these (prefixes on the `GameItem`/`PixelWindow` constructors + diffing the children of the render root) and destroys them at the end of each run.

### Safety net

Before the forecast: take the snapshot and record a fingerprint of every item in the shop, including the sold area (id, unique ID, quantity, unit price, feature count, containing inventory, and the values of all enabled tags).
After the forecast: **traverse the game state again independently** and compare it against the pre-forecast snapshot, object by object and field/element by field/element, then compare the item fingerprints and `Random.state`.

- Everything matches: results are shown normally.
- Any difference: a **red alarm** appears at the top of the window and the differing paths (e.g. `PlayerStore.instance.storeClientManager.clientTracker[xxx]: 2 -> 3`) are written to Player.log; **no more forecasts are allowed until you reload (or start a new save)**. If this happens, it's best **not to save** — just reload.

## Limitations and caveats

- **Only the queue generated when the shop opens is forecast.** Customers added during the day by events, phone calls, deals, etc. (e.g. `AddNextClient` mid-day) aren't included.
- The forecast uses the state at the moment you press the key. If before opening you change the display case or sign, buy a network service, or your reputation changes, press F9 again or click **Re-forecast** (重新预测).
- Who waits at the door is worked out from reputation perks and story conditions; the newspaper, hint notes, secret admirer gifts and so on in `OnShutterDoorOpenMorning` aren't customers and aren't shown.
- The "what they sell" text comes from `data/player/clients.json` (the stats handbook, compiled offline) and may go out of date after game updates; the categories buyers want are real runtime data.
- With `includeOtherMods=false` (the default), customers added by other mods via `ModHook.OnGenerateCustomer*` won't appear in the forecast. Turn it on and they will, but those mods' own internal state isn't covered by the snapshot, so being called repeatedly may change it (and the safety net can't detect that).
- The native window blocks mouse clicks. The fallback simple window only blocks in-game dragging/box-selecting of items, and some clicks may still pass through, so watch out for misclicks.
- The first forecast reads the static fields of every game type, which may trigger a few types' static initialization earlier than usual (the game would run it sooner or later anyway).
- The game freezes briefly during a forecast (300 runs take roughly one to a few seconds, depending on save size; the actual time is logged each time). Lower `runs` if it's too slow.
- If a key interception target can't be found after a game update, the mod disables forecasting and explains why in the log, rather than risk running.
- The type table used for the snapshot is generated for the current game version. Types added by a game update aren't in the table, so their state isn't protected by the snapshot or the safety net. The mod checks for this on its first forecast and, if it finds any, shows a "please update the mod" (请更新 mod) message in the log and the window (re-running `./build.sh` regenerates the table).
- Game updates may also extend the scanner's list of guarded types (`ModScanner.GuardTypes`). If a type on that list shows up in the type table, the game will **refuse to load** this mod (`loader-target: ... Type.GetFields on ...`). Since 1.3.1, the generator reads the game's current list at build time and excludes those types, so if you hit this rejection, just re-run `./build.sh`. You can use `../../tools/ScanCheck` to check outside the game first.
  - That's exactly how 1.3.0 got rejected: a new game version added `PaperBookRegistry` (the registry of mod book contents) and `ModSteamAuth` to the list. Both belong to the mod-loader layer and have nothing to do with customer generation; leaving them out of the snapshot affects neither the forecast nor the safety net.
- Collection contents and order are always restored exactly, but internal arrays are no longer restored bit-for-bit (1.2 and earlier did that). The only possible difference: if a Dictionary/HashSet already had empty slots left by deletions before the forecast, and the sandbox added keys to it, then keys you add to that collection later may land in a different spot in enumeration order than they would have without the forecast. This only affects the iteration order of a few collections; no data is lost or changed.

## Reporting issues

Please attach the Unity log: `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log` (the previous session's is `Player-prev.log`), and search for `[VisitorForecast]`. Every forecast writes one stats line (snapshot node count, time taken, number of write-backs, which calls were intercepted), and alarms list the differing paths.

## Building from source

```bash
cd mods/VisitorForecast
./build.sh            # builds bin/Release/VisitorForecast.dll and assembles dist/VisitorForecast/
./build.sh install    # also copies it into the game's Mods folder (does not launch the game)
```

Requires the .NET SDK (`~/.dotnet`; verified with 8.0; the target framework is netstandard2.1, matching the game's Assembly-CSharp) and references the assemblies in the game's `Probably Stolen_Data/Managed` (not distributed with the mod). The game directory is set in `local.env` at the repo root (see `local.env.example`), or can be overridden for one run with `GAME_DIR=... ./build.sh`. `tools/gen_client_tables.py` generates the Chinese "sells/buys" tables from the stats handbook data (`HANDBOOK_ROOT` in `local.env`, not in the repo); without that data, the already-generated `src/Generated/ClientTables.cs` in the repo is used. `tools/RegistryGen` (a small .NET 8 tool) generates the snapshot type table from the game's `Assembly-CSharp.dll` — after a game update, just re-run `./build.sh` (if the game directory can't be found, the existing generated files are used).

| File | Purpose |
|---|---|
| `src/VisitorForecastMod.cs` | `IMod` entry point: reads config, applies patches, creates the UI objects, subscribes to save-load events |
| `src/Sandbox/StateGraph.cs` | Snapshot / write-back / before-after comparison (fields come from the type table; collections go through public interfaces) |
| `src/Sandbox/GameTypes.cs` | Static roots, instance fields, which types belong to the item/UI graph |
| `src/Sandbox/GameTypeRegistry.cs` | Runtime part of the type table, plus the strongly typed HashSet/Queue/Stack restorers |
| `src/Generated/GameTypeRegistry.cs`, `tools/RegistryGen/` | The type table generated at build time from the game's `Assembly-CSharp.dll` (one `typeof(X).GetFields(...)` line per type), and its generator |
| `src/Sandbox/SandboxGuard.cs` | Interception patches active during the sandbox; tracking and destroying newly created items/windows |
| `src/Sandbox/SourceTagger.cs` | Source tag for each `Handle*` step |
| `src/Sandbox/Patches.cs` | Manual patching (if a target is missing, only the corresponding feature is disabled) |
| `src/Sandbox/WorldFingerprint.cs` | Fingerprints of items in the shop |
| `src/Forecast/ForecastEngine.cs` | Main forecast flow and statistics aggregation |
| `src/Forecast/Text.cs`, `src/Generated/ClientTables.cs` | Chinese names, category names, what customers sell/buy |
| `src/UI/ForecastBehaviour.cs` | Hotkey, forecast scheduling, choosing between the native and fallback windows |
| `src/UI/NativeForecastWindow.cs` | Native tablet window (CustomUIManager + TabletUI, the game's Chinese font) |
| `src/UI/ImguiForecastWindow.cs` | Fallback IMGUI window (system Chinese font) |
| `src/UI/ForecastView.cs` | Sorting, grouping by source and faction colors shared by both windows |
| `src/UI/UiPatches.cs` | Makes the game treat the mouse as "over UI" while it's over the window (`StoreUIManager.CheckRaycast` postfix) |
