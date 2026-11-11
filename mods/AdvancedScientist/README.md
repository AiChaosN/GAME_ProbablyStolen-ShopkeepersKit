# [Gameplay] Shopkeeper's Kit: Advanced Scientist

**English** | [中文](README.zh-CN.md)

> Part of the **Shopkeeper's Kit** series · category **[Gameplay]**: a new profession that changes game mechanics. See the [repository home](../../README.md) for the series and categories.
> By ChaosN · developed with assistance from [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding assistant). Mod ID: `chaos5090.advanced-scientist`.
>
> In development (0.1.0, step one); not on the Steam Workshop yet.

UI in Chinese or English, following the game's language (Chinese shows Chinese, every other language shows English).

## Profession: Advanced Scientist

Found on **page 2** of the profession screen when starting a new game. A module-crafting expert.

Starts with:

- a **Machine Bay** (the game's area for placing machines);
- the **Blank Module Maker**, plus a battery (100 charge) and materials for one low-grade blank module (2 Common Electronic Parts, 2 Scrap Metal);
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
| ① Blank Module Maker | materials → low / mid / high grade blank modules (1 / 2 / 3 slots) | **done in 0.1.0** |
| ② Affix Extractor | module → base chip (performance / efficiency / quality) + special-affix chips; the module becomes blank | step two |
| ④ Inlayer | blank module + chips → a module with stats | step two |
| ③ Chip Merger | two base chips → one, stats summed, each stat capped at ±500 | step three |

All machines work per day: they use power and run once each night. ② and ③ will be sold by Dr. Jackson (to Advanced Scientists only); ④ will be handed to existing Advanced Scientist saves.

### ① Blank Module Maker

Put materials in the "Materials" grid and a battery in the power slot. Each night it makes one blank module, the **best grade** the materials allow:

| Output | Slots | Materials | Value |
|---|---|---|---|
| Low-grade blank module | 1 | Common Electronic Parts ×2 + Scrap Metal ×2 | 60 |
| Mid-grade blank module | 2 | Common Electronic Parts ×4 + Metal Ingot ×1 + Wire ×2 | 180 |
| High-grade blank module | 3 | Rare Electronic Parts ×1 + Metal Ingot ×2 + Wire ×4 | 450 |

- All three are the game's own 2×2 "Blank Module"; the slot count is stored on the item and shown in its name. If you uninstall this mod they turn back into ordinary blank modules instead of vanishing.
- Uses 30 charge per night; if the output grid is full or there's no charge, it skips the night without using materials.
- In a Machine Bay, an Advanced Scientist gets two per night.

## Install

1. After building (see "Build from source" on the [repository home](../../README.md)), copy the whole `dist/AdvancedScientist/` folder (including the sprites under `Textures/Items/`) into the game's `Mods` folder.
2. Start the game → Mods → select **[Gameplay] Shopkeeper's Kit: Advanced Scientist** → Review and enable → restart.
3. New game → page 2 of the profession screen → Advanced Scientist.
4. This mod requests **no special capabilities** and uses only the game's official mod APIs (new profession, new items, nightly event); the game's own scanner reports no capabilities, not refused, no findings.

Uninstalling: in existing Advanced Scientist saves the Blank Module Maker disappears because the game no longer knows it (true for any mod that adds items); blank modules stay (as ordinary blank modules).

## How to test

> Back up your saves first (`save_*.es3` in `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\`) and test with a new game.

1. New game → page 2 of the profession screen shows "Advanced Scientist" (icon: a flask and a module). Start with it.
2. Storage holds the Machine Bay, the Blank Module Maker, a battery, three modules, 2 Common Electronic Parts and 2 Scrap Metal.
3. Put the maker in the Machine Bay, the battery in its power slot, the parts and scrap in "Materials", and sleep: the "Output" grid should have a low-grade blank module.
4. Add two sets of materials and sleep: in the Machine Bay it makes two per night (the ability).
5. Search `Player.log` for `[AdvancedScientist]`: look for the item registration, the profession start, "空白模组制造机：做出 …" and "机器区多运转一天：N 台".

## Implementation

| File | Contents |
|---|---|
| `src/Scenario.cs` | Profession via `ModHelper.AddStartingScenario`; starting items |
| `src/BlankModuleMaker.cs` | ① Blank Module Maker (built like the game's Age Well: battery slot + materials + output, nightly `onCycleEndSlotItemFunc`) |
| `src/BlankModules.cs` | Blank module grades and recipes |
| `src/TwoDays.cs` | Two days per day in the Machine Bay (`ModHook.OnGoingSleepLate`: run the early / main / late passes again for machines in a bay) |
| `src/ScientistMod.cs` | Entry point, settings, new item registration (`ModHook.OnModItemDirectoryInit`) |
| `tools/make_sprites.py` | Generates the pixel art in `Textures/Items/` |

**Status**: builds and passes the game's mod scanner; **not yet tested in game**.

## Changelog

- **0.1.0**: Step one: profession, two days per day in the Machine Bay, Blank Module Maker.
