# [Gameplay] Shopkeeper's Kit: Advanced Scientist

**English** | [中文](README.zh-CN.md)

> Part of the **Shopkeeper's Kit** series · category **[Gameplay]**: a new profession that changes game mechanics. See the [repository home](../../README.md) for the series and categories.
> By ChaosN · developed with assistance from [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding assistant). Mod ID: `chaos5090.advanced-scientist`.
>
> **Steam Workshop: [subscribe here](https://steamcommunity.com/sharedfiles/filedetails/?id=3837934061)**
>
> 0.3.3 (step two); the chip merger comes later.

UI in Chinese or English, following the game's language (Chinese shows Chinese, every other language shows English).

## Profession: Advanced Scientist

Found on **page 2** of the profession screen when starting a new game. A module-crafting expert.

Starts with:

- a **Machine Bay** (the game's area for placing machines);
- the **Blank Module Maker** and the **Affix Inlayer**, each with a battery (100 charge), plus materials for one low-grade blank module (2 each of Common Electronic Parts, Metal Ingots and Wire);
- three random modules (performance / efficiency / quality);
- 200 less starting cash (the starting equipment is valuable; the base-game chemist has 300 less).

### Ability: machines in the Machine Bay run two days per day

- Machines placed **directly in a Machine Bay** run one more cycle after each night's processing: output and consumption count twice, and the battery is drawn again (a machine without enough charge skips that cycle).
- It works like the game's own turbo booster (one extra nightly cycle).
- Not doubled: fridges (would only use more power), alarms and mirage projectors (their bonus doesn't stack), power generators (recharger, PowerBloc, reactor, breeder battery — doubling would create free power), broken machines.
- Not covered: devices that run per second during the day (blender, heating plate) and animals in cages.
- Only in Advanced Scientist saves; can be turned off in the mod settings (`twoDaysInMachineBay`).

## Module crafting

| Machine | What it does | Status |
|---|---|---|
| ① Blank Module Maker | materials → low / mid / high grade blank modules (2 / 4 / 6 slots) | **done in 0.1.0** |
| ② Affix Extractor | module → base chip (performance / efficiency / quality) + affix chips; the module becomes blank | **done in 0.2.0** |
| ④ Affix Inlayer | blank module + chips → custom module | **done in 0.2.0** |
| ③ Chip Merger | two base chips → one, stats summed, each stat capped at ±500 | step three |

All machines work **at the click of a button**: open the machine and click "Make / Extract / Inlay" left of × in its title bar; each click runs once and uses 30 charge, and a message tells you what was made or why not (not enough materials, output full, no power, ...). ② is sold by Dr. Jackson (at his shop at night or when he visits yours; to Advanced Scientists only, and only while you don't own one), and ③ will be too; ④ is starting equipment, and saves started with 0.1.0 get one when loaded.

### ① Blank Module Maker

Put materials in the "Materials" grid (12×10) and a battery in the power slot, then click "Make" for one blank module, the **best grade** the materials allow:

| Output | Slots | Materials | Value |
|---|---|---|---|
| Low-grade blank module | 2 | Common Electronic Parts ×2 + Metal Ingot ×2 + Wire ×2 | 60 |
| Mid-grade blank module | 4 | Common Electronic Parts ×4 + Metal Ingot ×4 + Wire ×4 | 390 |
| High-grade blank module | 6 | Common Electronic Parts ×6 + Metal Ingot ×6 + Wire ×6 | 590 |

- Mid and high values roughly match the material cost (Common Electronic Parts 33, Metal Ingot 50, Wire 16), so crafting them to sell makes no profit; low grade keeps the game's own blank module value of 60 (extracting game modules also yields low-grade blanks).
- All three are the game's own 2×2 "Blank Module"; the slot count is stored on the item and shown in its name. If you uninstall this mod they turn back into ordinary blank modules instead of vanishing.
- Uses 30 charge per click; if the output grid is full or there's no charge, nothing is made and no materials are used.

### Chips

| Chip | Holds | Value |
|---|---|---|
| Base Chip (green) | one set of stats: performance / efficiency / quality, each capped at ±500 | half the sum of the absolute stats (16 performance ≈ 8) |
| Affix Chip (purple) | one of the game's module affixes: Premium, Inferior, Overvolted, Degrading, Negative Feedback, Learning Algorithm, Receiver, Top Mount, Side Mount | 30 |

The chip's name shows its contents, e.g. "Base Chip (Perf +36 / Eff -44 / Qual +0)".

### ② Affix Extractor

Put a module in the "Module" slot and click "Extract":

- **Game modules**: the stats become a base chip, each special affix an affix chip, and the module becomes a **low-grade blank** (2 slots).
  - When the game generates a module, the affix multipliers are already baked into its stats (Premium ×1.2, Inferior ×0.8, Overvolted and Degrading ×2, Negative Feedback ×1.25). Extraction divides them out: the base chip holds the raw stats, the multiplier travels with the affix chip and applies again when inlaid.
  - Learning Algorithm modules: half their fully-learned stats (fully learned is 2× the original in the game).
- **Custom modules**: the inlaid chips come back unchanged and the module becomes a blank of the **same grade**.
- Contraband such as neural cores and stolen goods can be extracted too; the chips and blank module that come out are clean.
- Refused: functional modules (chemistry, farming, furnace, printer, ...), nodes, corrupt / ruined modules; you get a message saying why when you try.
- If the output grid can't fit a blank module nothing happens; chips that don't fit go to storage.

### ④ Affix Inlayer

Put a blank module (or a custom module with free slots) in the "Blank module" slot and chips in the "Chips" grid, then click "Inlay":

- Fills every free slot in one go: **base chips first** (stats add up), **then affix chips**.
- Affixes follow the game's rules: at most 2 per module, no duplicates, no conflicting pairs (e.g. Premium with Inferior, Learning Algorithm with other multiplier affixes). Chips that can't be inlaid stay in the grid.
- The result is called "Custom Module (N/slots used)"; hovering shows the game's own stat and affix tooltip, and it works in machines like any module.
- Value = blank module value + chip values (plus the game's own value adjustment for affixes).

## Install

1. After building (see "Build from source" on the [repository home](../../README.md)), copy the whole `dist/AdvancedScientist/` folder (including the sprites under `Textures/Items/`) into the game's `Mods` folder.
2. Start the game → Mods → select **[Gameplay] Shopkeeper's Kit: Advanced Scientist** → Review and enable → restart.
3. New game → page 2 of the profession screen → Advanced Scientist.
4. This mod requests **no special capabilities** and uses only the game's official mod APIs (new profession, new items, nightly event); the game's own scanner reports no capabilities, not refused, no findings.

Uninstalling: in existing Advanced Scientist saves the three machines and the chips disappear because the game no longer knows them (true for any mod that adds items); blank and custom modules stay (custom modules keep their stats and affixes, they just can't be inlaid further).

## How to test

> Back up your saves first (`save_*.es3` in `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\`) and test with a new game.

1. New game → page 2 of the profession screen shows "Advanced Scientist" (icon: a flask and a module). Start with it.
2. Storage holds the Machine Bay, the Blank Module Maker, the Affix Inlayer, two batteries, three modules, 2 each of Common Electronic Parts, Metal Ingots and Wire.
3. Open the maker, put the battery in its power slot and the parts, ingots and wire in "Materials", then click "Make" in the title bar: a low-grade blank module appears in "Output".
4. Ability: put another machine from the game (e.g. a water purifier) in the Machine Bay and sleep; it should produce two days' worth.
5. Inlaying: put the low-grade blank module into the inlayer's "Blank module" slot. Chips need the extractor first (next step).
6. Extracting: buy the Affix Extractor from Dr. Jackson (once the story has introduced him), put a random module (a neural core works too) in and click "Extract": the output holds a base chip (maybe affix chips too) and a low-grade blank module. Put the base chip in the inlayer and click "Inlay": the blank becomes "Custom Module (1/2 slots used)"; hover it to see the stats.
7. Search `Player.log` for `[AdvancedScientist]`: item registration, the profession start, "空白模组制造机：做出 …", "词条提取机：…", "词条镶嵌机：镶上 …", "机器区多运转一天：N 台".

## Implementation

| File | Contents |
|---|---|
| `src/Scenario.cs` | Profession via `ModHelper.AddStartingScenario`; starting items |
| `src/BlankModuleMaker.cs` | ① Blank Module Maker (built like the game's Age Well: battery slot + materials + output, nightly `onCycleEndSlotItemFunc`) |
| `src/BlankModules.cs` | Blank module grades and recipes |
| `src/Affixes.cs` | Affixes, the two chips, custom modules (the inlaid list is stored in a tag on the module and everything is recomputed from it: stats summed, then affixes added with the game's `ModuleEffectHelper.AddEffectToModule`) |
| `src/AffixMachines.cs` | ② Affix Extractor, ④ Affix Inlayer |
| `src/Progression.cs` | Dr. Jackson sells the extractor (`ModHook.OnPlaceInventorInventoryItemLate`); older saves get the inlayer |
| `src/TwoDays.cs` | Two days per day in the Machine Bay (`ModHook.OnGoingSleepLate`: run the early / main / late passes again for machines in a bay) |
| `src/ScientistMod.cs` | Entry point, settings, new item registration (`ModHook.OnModItemDirectoryInit`) |
| `tools/make_sprites.py` | Generates the pixel art in `Textures/Items/` |

**Status**: builds and passes the game's mod scanner; **not yet tested in game**.

## Changelog

- **0.3.3**: Recipes use Metal Ingots instead of Scrap Metal (2 / 4 / 6 each); mid / high values 390 / 590; the start kit gives 2 Metal Ingots. Fixed stale in-game descriptions: saves store item names and descriptions, so existing machines kept showing the old recipes; names and descriptions of this mod's machines, chips and blank modules are now refreshed on load for the current version and language. First Workshop release.
- **0.3.2**: Recipes add wire: 2 / 4 / 6 each of Common Electronic Parts, Scrap Metal and Wire for low / mid / high; slots are now 2 / 4 / 6 (blank modules from older saves get the new count and name on load); materials grid 9×8 → 12×10; mid / high values 230 / 350; the start kit includes 2 wire.
- **0.3.1**: Recipes now use only Common Electronic Parts + Scrap Metal: 2 / 4 / 6 of each for low / mid / high; materials grid 9×6 → 9×8 (the high recipe is 12 items of 3×2); mid / high blank values 170 / 260 to match the material cost.
- **0.3.0**: The three machines now work at the click of a title-bar button ("Make / Extract / Inlay") instead of overnight, with a message about the result or why not; the extractor accepts contraband such as neural cores and stolen goods, and the results are clean; refused modules show why.
- **0.2.1**: The Blank Module Maker's materials grid grew from 6×3 to 9×6 and its output from 4×2 to 4×4. The materials are big (electronic parts, ingots and scrap are 3×2, wire 2×2, none stack), so even the low-grade recipe didn't fit before.
- **0.2.0**: Step two: base chips, affix chips, Affix Extractor (sold by Dr. Jackson), Affix Inlayer (starting equipment; given to older saves).
- **0.1.0**: Step one: profession, two days per day in the Machine Bay, Blank Module Maker.
