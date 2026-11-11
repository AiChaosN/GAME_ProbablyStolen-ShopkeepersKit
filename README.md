# Shopkeeper's Kit (掌柜帮手)

**English** | [中文](README.zh-CN.md)

A series of mods for *Probably Stolen* by ChaosN. Every mod name starts with a **category** tag, so you can tell at a glance whether it affects game balance.

Developed with assistance from [Claude Code](https://claude.com/claude-code) (Anthropic's AI coding assistant): the author makes the design calls; Claude Code helps with the code, the reverse engineering and the documentation.

## Categories

| Tag | Meaning | Effect on balance |
|---|---|---|
| **[QoL]** 【便利】 | No stat changes and no extra information; only saves you clicks | None |
| **[Info]** 【情报】 | No stat changes, but shows information the game doesn't tell you directly | Some: more information makes the game easier |
| **[Fix]** 【修复】 | Fixes a game bug so the game works as intended | Some: you may get things the bug used to eat |
| **[Gameplay]** 【玩法】 | Changes numbers or game mechanics | Yes |

## Mods

| Name | Category | Keys | Summary | Mod ID | Workshop |
|---|---|---|---|---|---|
| [Sort To Boxes](mods/SortToBoxes/README.md) | [QoL] | G / C | Storing: loose items in storage go into the box for their category (13 categories, each item in exactly one); tag boxes with whole categories or single items; a badge shows each box's contents; optional auto-sort on purchase. Selling: one key puts everything the buyer wants on the counter; placing one item brings its identical copies | `chaos5090.sort-to-boxes` | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3835916464) |
| [Counter Overflow](mods/CounterOverflow/README.md) | [Fix] | Z / X | Items a seller can't fit on the counter go onto extra pages instead of being destroyed: page bar above the counter, each page keeps its layout, items you take off can always go back | `chaos5090.counter-overflow` | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3835916532) |
| [Quick Swap](mods/Conveniences/README.md) | [QoL] | W | A collection of small QoL features with no balance impact. Currently: swap batteries and water filters without opening anything by dropping the new part on the machine (or pitcher); hold a bottle over a container and press W to fill it | `chaos5090.conveniences` | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3837822167) |
| [Filter Recharge](mods/FilterRecharge/README.md) | [Gameplay] | — | Drop water filters on an Advanced Water Filter to move their durability over (box-select for batches); the advanced filter's cap rises to 20× a regular one (adjustable); drop the advanced filter on a big container to filter its water in place | `chaos5090.filter-recharge` | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3837823741) |
| [Advanced Scientist](mods/AdvancedScientist/README.md) | [Gameplay] | — | New profession: module crafting (Blank Module Maker, Affix Extractor, Affix Inlayer, each at the click of a button; chip merging to come); machines in the Machine Bay run two days per day | `chaos5090.advanced-scientist` | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3837934061) |
| [Chef](mods/Chef/README.md) | [Gameplay] | — | New profession running a fast-food kitchen (deep fryer, food processor, fries / fried chicken / burger / super combo in five grades up to Michelin); cooking oil gets dirty and needs filtering; other professions can buy Wild's "Retired Chef" upgrade. In development | `chaos5090.chef` | Not uploaded yet |
| [Visitor Forecast](mods/VisitorForecast/README.md) | [Info] | V | Before opening, forecasts who will visit today (faction, intent, wanted goods, budget, probability) | `chaos5090.visitor-forecast` | [Subscribe](https://steamcommunity.com/sharedfiles/filedetails/?id=3835916583) |

Sort To Boxes, Counter Overflow, Quick Swap, Filter Recharge, Advanced Scientist and Chef show their UI in Chinese or English, following the game's language; Visitor Forecast is currently Chinese only.

## Install

**Recommended: subscribe on the Steam Workshop** (links in the last column above), then in game open **Mods**, select the mod, click **Review and enable** and restart when asked. Steam keeps it updated.

Manual install: build `dist/<name>/` as described under "Build from source", copy that folder into the game's `Mods` folder, then review and enable it the same way. Each mod's page has the full steps.

## Naming

- In-game name and Workshop title: `【分类】掌柜帮手·<Chinese name> | [<category>] Shopkeeper's Kit: <English name>`
- Mod ID: `chaos5090.<english-name-in-kebab-case>`. Never change it after release: the game stores settings by ID (`ModConfigs/<ID>.json`)
- New small QoL features with no balance impact go into Quick Swap (`chaos5090.conveniences`) instead of new mods. Its ID is the generic `conveniences`, so the name can follow its contents without changing the ID; Sort To Boxes, already released, stays separate
- The author field is always `ChaosN`; the Claude Code credit goes in the description and README
- Default hotkeys sit under the left hand (sort G, counter C, forecast V, pages Z/X), avoiding the game's own keys (Q E R F S P K O B, Space, digits) and F5 Client Demand Highlight, F6 Smart Inventory Arrange, F8 Cheat Menu, F10 Show Item Names; they don't fire while you type or rebind keys in Options

## Conventions

- No special capabilities (empty `<Capabilities />` in the manifest). Before release, run the game's own mod scanner via [`tools/ScanCheck`](tools/ScanCheck); the result must be "not refused, no capabilities, no findings"
- All patches are applied manually: if a game update removes a target, only that feature is disabled, not the whole mod
- Settings use the game's own ModConfig and can be changed from the Mods screen

## Build from source

1. Install the .NET SDK (tested with 8.0; the mods themselves target netstandard2.1, like the game).
2. Copy `local.env.example` to `local.env` and set your game folder (`GAME_DIR`). `local.env` is not committed.
3. In a mod's folder run `./build.sh` to build and assemble `dist/<name>/`; `./build.sh install` also copies it into the game's `Mods` folder (it never starts the game; it fails while the game is running because the DLL is locked).

Builds reference the assemblies in your local `Probably Stolen_Data/Managed`; they are not distributed with this repository or the mods.

## Publishing to the Workshop

Uploads go through Valve's SteamCMD; see [`workshop/`](workshop/README.md) for the steps and the prepare script.

## Contributing

Issues and pull requests are welcome, and the mods will keep getting updates:
- Bugs, a mod that stopped working after a game update, feature ideas: open an [issue](https://github.com/AiChaosN/GAME_ProbablyStolen-ShopkeepersKit/issues). For bugs, the game version, mod version and the relevant lines of the game log (`%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log`) help a lot.
- Fixes, improvements, translations and new mod ideas are welcome as [pull requests](https://github.com/AiChaosN/GAME_ProbablyStolen-ShopkeepersKit/pulls). Before submitting, run `tools/ScanCheck` as described under Conventions and try the change in the game.

## License

[MIT](LICENSE).
