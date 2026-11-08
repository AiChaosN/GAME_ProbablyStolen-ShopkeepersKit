# Shopkeeper's Kit (掌柜帮手)

**English** | [中文](README.zh-CN.md)

A series of mods for *Probably Stolen* by chaos5090. Every mod name starts with a **category** tag, so you can tell at a glance whether it affects game balance.

Developed with assistance from [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding assistant): the author makes the design calls; Claude Code helps with the code, the reverse engineering and the documentation.

## Categories

| Tag | Meaning | Effect on balance |
|---|---|---|
| **[QoL]** 【便利】 | No stat changes and no extra information; only saves you clicks | None |
| **[Info]** 【情报】 | No stat changes, but shows information the game doesn't tell you directly | Some: more information makes the game easier |
| **[Gameplay]** 【玩法】 | Changes numbers or game mechanics | Yes (no such mod yet) |

## Mods

| Name | Category | Keys | Summary | Mod ID |
|---|---|---|---|---|
| [Sort To Boxes](mods/SortToBoxes/README.md) | [QoL] | F7 / F4 | Storing: loose items in storage go into the box matching their first item type; boxes can be tagged with categories and show an icon of their contents; optional auto-sort on purchase. Selling: one key puts everything the buyer wants on the counter; placing one item brings its identical copies | `chaos5090.sort-to-boxes` |
| [Visitor Forecast](mods/VisitorForecast/README.md) | [Info] | F9 | Before opening, forecasts who will visit today (faction, intent, wanted goods, budget, probability) | `chaos5090.visitor-forecast` |

The in-game UI of all mods is currently Chinese only.

## Install

Each mod's page has the full steps. In short: copy the `dist/<name>/` folder into the game's `Mods` folder, then in game open **改装件** (Mods), select the mod, click **审阅并启用** (Review and enable) and restart when asked.

## Naming

- In-game name and Workshop title: `【分类】掌柜帮手·<Chinese name> | [<category>] Shopkeeper's Kit: <English name>`
- Mod ID: `chaos5090.<english-name-in-kebab-case>`. Never change it after release: the game stores settings by ID (`ModConfigs/<ID>.json`)
- The author field is always `chaos5090`; the Claude Code credit goes in the description and README
- Hotkeys avoid keys taken by popular mods: F5 Client Demand Highlight, F6 Smart Inventory Arrange, F8 Cheat Menu

## Conventions

- No special capabilities (empty `<Capabilities />` in the manifest). Before release, run the game's own mod scanner via [`tools/ScanCheck`](tools/ScanCheck); the result must be "not refused, no capabilities, no findings"
- All patches are applied manually: if a game update removes a target, only that feature is disabled, not the whole mod
- Settings use the game's own ModConfig and can be changed from the Mods screen

## Build from source

1. Install the .NET SDK (tested with 8.0; the mods themselves target netstandard2.1, like the game).
2. Copy `local.env.example` to `local.env` and set your game folder (`GAME_DIR`). `local.env` is not committed.
3. In a mod's folder run `./build.sh` to build and assemble `dist/<name>/`; `./build.sh install` also copies it into the game's `Mods` folder (it never starts the game; it fails while the game is running because the DLL is locked).

Builds reference the assemblies in your local `Probably Stolen_Data/Managed`; they are not distributed with this repository or the mods.

## License

[MIT](LICENSE).
