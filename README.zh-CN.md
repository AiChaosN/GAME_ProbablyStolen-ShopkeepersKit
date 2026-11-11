# 掌柜帮手 / Shopkeeper's Kit

[English](README.md) | **中文**

ChaosN 为《Probably Stolen》做的 mod 系列。每个 mod 名字前标出**分类**，看一眼就知道它会不会影响游戏平衡。

由 [Claude Code](https://claude.com/claude-code)（Anthropic 的 AI 编程助手）协助开发：设计与取舍由作者决定，代码、逆向分析和文档由 Claude Code 协助完成。

## 分类

| 标签 | 含义 | 对平衡的影响 |
|---|---|---|
| **【便利】** QoL | 不改任何数值，也不给额外信息，只帮你省操作 | 无 |
| **【情报】** Info | 不改数值，但会显示游戏本来不直接告诉你的信息 | 有一些：信息更多，游戏会变简单 |
| **【修复】** Fix | 修游戏 bug，让游戏按它本来的意图运行 | 有一些：修好后可能多出原本被 bug 吃掉的东西 |
| **【玩法】** Gameplay | 改数值或游戏机制 | 有（目前还没有这类 mod） |

## 系列 mod

| 名称 | 分类 | 热键 | 简介 | mod ID | 创意工坊 |
|---|---|---|---|---|---|
| [掌柜帮手·归类入箱 Sort To Boxes](mods/SortToBoxes/README.zh-CN.md) | 【便利】 | G / C | 收货：仓库散件按大类（13 类，每件货只属于一个类）归进对应的箱子，箱子可勾选整类或单件、右下角显示内容角标，可选买下时自动归箱。卖货：一键把买家会买的货摆上柜台，放一件自动带上同款 | `chaos5090.sort-to-boxes` | [订阅](https://steamcommunity.com/sharedfiles/filedetails/?id=3835916464) |
| [掌柜帮手·柜台不丢货 Counter Overflow](mods/CounterOverflow/README.zh-CN.md) | 【修复】 | Z / X | 卖家摆货时柜台放不下的货不再被销毁，分到后面的页：柜台上方有翻页条，每页记住货的位置，拿下来的货随时能放回去 | `chaos5090.counter-overflow` | [订阅](https://steamcommunity.com/sharedfiles/filedetails/?id=3835916532) |
| [掌柜帮手·拖放换件 Drop Swap](mods/DropSwap/README.zh-CN.md) | 【便利】 | — | 换电池、换滤芯不用打开机器：新电池直接拖到机器上（新滤芯拖到滤水壶上）就换下旧件，旧件放回新件原来的位置 | `chaos5090.drop-swap` | 还没上传 |
| [掌柜帮手·访客预测 Visitor Forecast](mods/VisitorForecast/README.zh-CN.md) | 【情报】 | V | 开门前预测今天谁会来（阵营、来意、想要什么、预算、概率） | `chaos5090.visitor-forecast` | [订阅](https://steamcommunity.com/sharedfiles/filedetails/?id=3835916583) |

归类入箱、柜台不丢货、拖放换件的界面中英双语，跟随游戏的语言设置；访客预测目前只有中文。

## 安装

**推荐：在 Steam 创意工坊订阅**（上表最后一列的链接），进游戏打开 **改装件**，选中它点 **审阅并启用**，按提示重启。以后有新版本 Steam 会自动更新。

手动安装：先按下面“从源码构建”编出 `dist/<名字>/`，把这个文件夹复制到游戏目录的 `Mods` 文件夹，再同样审阅并启用。每个 mod 的说明里有详细步骤。

## 命名规则

- 游戏内名称与创意工坊标题：`【分类】掌柜帮手·<中文名> | [<英文分类>] Shopkeeper's Kit: <英文名>`
- mod ID：`chaos5090.<英文名小写连字符>`。发布后不要再改，因为游戏按 ID 保存配置（`ModConfigs/<ID>.json`）
- 作者字段统一写 `ChaosN`；Claude Code 协助的说明写在简介和 README 里
- 默认热键放在左手区（归箱 G、上柜台 C、访客预测 V、翻页 Z/X），避开游戏自带键（Q E R F S P K O B、空格、数字键）和常见 mod 的 F5 客户需求高亮、F6 智能整理、F8 作弊菜单、F10 物品名称常显；输入文字或在设置里改键时不会触发

## 共同约定

- 不申请任何特殊权限（manifest 的 `<Capabilities />` 为空）。发布前用 [`tools/ScanCheck`](tools/ScanCheck) 跑一遍游戏自带的扫描器，必须是“不拒绝、无权限、无发现”
- 补丁都手动打：游戏更新后找不到目标时，只禁用对应的功能，不让整个 mod 加载失败
- 配置走游戏自带的 ModConfig，可以在改装件界面里修改

## 从源码构建

1. 装 .NET SDK（已用 8.0 验证；mod 本身的目标框架是 netstandard2.1，与游戏一致）。
2. 把 `local.env.example` 复制成 `local.env`，填上你的游戏安装目录（`GAME_DIR`）。`local.env` 不进仓库。
3. 进入 mod 目录运行 `./build.sh`，编译并组装 `dist/<名字>/`；`./build.sh install` 会再复制到游戏的 `Mods` 目录（不会启动游戏；游戏开着时 DLL 被锁会失败）。

编译时引用本机游戏 `Probably Stolen_Data/Managed` 下的程序集，这些程序集不随仓库或 mod 分发。

## 发布到创意工坊

用 Valve 的 SteamCMD 上传，步骤和准备脚本见 [`workshop/`](workshop/README.zh-CN.md)。

## 欢迎参与

欢迎提问题和 PR，后续版本也会继续更新：
- 遇到 bug、游戏更新后 mod 不能用了、想要新功能，都可以在 [Issues](https://github.com/AiChaosN/GAME_ProbablyStolen-ShopkeepersKit/issues) 里说。报 bug 时附上游戏版本、mod 版本和游戏日志里相关的几行（`%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log`），会快很多。
- 修 bug、改进功能、补翻译、新 mod 的点子，都欢迎直接提 [Pull Request](https://github.com/AiChaosN/GAME_ProbablyStolen-ShopkeepersKit/pulls)。提交前请按上面的“共同约定”用 `tools/ScanCheck` 跑一遍，并在游戏里实际试过。

## 许可证

[MIT](LICENSE)。
