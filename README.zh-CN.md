# 掌柜帮手 / Shopkeeper's Kit

[English](README.md) | **中文**

chaos5090 为《Probably Stolen》做的 mod 系列。每个 mod 名字前标出**分类**，看一眼就知道它会不会影响游戏平衡。

由 [Claude Code](https://claude.com/claude-code)（Anthropic 的 AI 编程助手）协助开发：设计与取舍由作者决定，代码、逆向分析和文档由 Claude Code 协助完成。

## 分类

| 标签 | 含义 | 对平衡的影响 |
|---|---|---|
| **【便利】** QoL | 不改任何数值，也不给额外信息，只帮你省操作 | 无 |
| **【情报】** Info | 不改数值，但会显示游戏本来不直接告诉你的信息 | 有一些：信息更多，游戏会变简单 |
| **【修复】** Fix | 修游戏 bug，让游戏按它本来的意图运行 | 有一些：修好后可能多出原本被 bug 吃掉的东西 |
| **【玩法】** Gameplay | 改数值或游戏机制 | 有（目前还没有这类 mod） |

## 系列 mod

| 名称 | 分类 | 热键 | 简介 | mod ID |
|---|---|---|---|---|
| [掌柜帮手·归类入箱 Sort To Boxes](mods/SortToBoxes/README.zh-CN.md) | 【便利】 | F7 / F4 | 收货：仓库散件按第一个标签归进对应的箱子，箱子可勾选分类、显示里面物品的小图标，可选买下时自动归箱。卖货：一键把买家会买的货摆上柜台，放一件自动带上同款 | `chaos5090.sort-to-boxes` |
| [掌柜帮手·柜台不丢货 Counter Overflow](mods/CounterOverflow/README.zh-CN.md) | 【修复】 | , / . | 卖家摆货时柜台放不下的货不再被销毁，分到后面的页：柜台上方有翻页条，每页记住货的位置，拿下来的货随时能放回去 | `chaos5090.counter-overflow` |
| [掌柜帮手·访客预测 Visitor Forecast](mods/VisitorForecast/README.zh-CN.md) | 【情报】 | F9 | 开门前预测今天谁会来（阵营、来意、想要什么、预算、概率） | `chaos5090.visitor-forecast` |

所有 mod 的界面目前只有中文。

## 安装

每个 mod 的说明里都有安装步骤。简单说：把 `dist/<名字>/` 文件夹复制到游戏目录的 `Mods` 文件夹，进游戏在 **改装件** 界面对它点 **审阅并启用**，按提示重启。

## 命名规则

- 游戏内名称与创意工坊标题：`【分类】掌柜帮手·<中文名> | [<英文分类>] Shopkeeper's Kit: <英文名>`
- mod ID：`chaos5090.<英文名小写连字符>`。发布后不要再改，因为游戏按 ID 保存配置（`ModConfigs/<ID>.json`）
- 作者字段统一写 `chaos5090`；Claude Code 协助的说明写在简介和 README 里
- 热键避开常见 mod 已占用的键：F5 客户需求高亮、F6 智能整理、F8 作弊菜单、F10 物品名称常显

## 共同约定

- 不申请任何特殊权限（manifest 的 `<Capabilities />` 为空）。发布前用 [`tools/ScanCheck`](tools/ScanCheck) 跑一遍游戏自带的扫描器，必须是“不拒绝、无权限、无发现”
- 补丁都手动打：游戏更新后找不到目标时，只禁用对应的功能，不让整个 mod 加载失败
- 配置走游戏自带的 ModConfig，可以在改装件界面里修改

## 从源码构建

1. 装 .NET SDK（已用 8.0 验证；mod 本身的目标框架是 netstandard2.1，与游戏一致）。
2. 把 `local.env.example` 复制成 `local.env`，填上你的游戏安装目录（`GAME_DIR`）。`local.env` 不进仓库。
3. 进入 mod 目录运行 `./build.sh`，编译并组装 `dist/<名字>/`；`./build.sh install` 会再复制到游戏的 `Mods` 目录（不会启动游戏；游戏开着时 DLL 被锁会失败）。

编译时引用本机游戏 `Probably Stolen_Data/Managed` 下的程序集，这些程序集不随仓库或 mod 分发。

## 许可证

[MIT](LICENSE)。
