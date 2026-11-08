# 【情报】掌柜帮手·访客预测 Visitor Forecast

> **掌柜帮手 / Shopkeeper's Kit** 系列 · 分类 **【情报】**：不改任何数值，但会提前告诉你今天谁会来，游戏会因此简单一些。系列与分类说明见 [仓库首页](../../README.md)。
> 作者 chaos5090 · 由 [Claude Code](https://claude.com/claude-code)（Anthropic 的 AI 编程助手）协助开发。mod ID：`chaos5090.visitor-forecast`。

《Probably Stolen》（v0.46D，Unity 2021.3 Mono）的代码 mod。**每天开门前**按 **F9**，它会在一个“沙盒”里把开门那一刻的顾客生成流程跑几百遍，告诉你今天：

- **必来**的人（100%）；
- **其他客人**：不一定来的人，按**来源**分组（随机顾客、情报贩子、新面孔、声望特权…），每人各自的出现概率；
- **早上在门口等**的人（开门后直接站在柜台前、不排队的那种，比如卢、治安部的人）；
- 每个人的 **阵营、来做什么（买/卖/买卖/对话/检查…）、想要什么类型的货（卖家则写卖什么）、预算范围、来源（剧情/每周日程/展示柜买家/随机顾客…）、大概排第几位**；
- 预计今天总共来多少人（平均值与最少/最多）。

预测完会**自动校验**游戏状态与预测前一字不差；发现任何不一致都会红字报警，并在重新读档前拒绝再次预测（见“安全网”）。

> 预测基于**按键那一刻**的展示柜、声望、事件和存档状态。改动展示柜/招牌后请再按 F9（或窗口里的“重新预测”）。

---

## 安装

1. 把 `dist/VisitorForecast/` 整个文件夹复制到游戏目录的 `Mods` 文件夹里，得到：
   ```
   <游戏目录>/Mods/VisitorForecast/manifest.xml
   <游戏目录>/Mods/VisitorForecast/VisitorForecast.dll
   ```
   游戏目录在 Steam 库的 `steamapps\common\Probably Stolen`（Steam 里右键游戏 → 管理 → 浏览本地文件）。
   在 WSL 里也可以直接 `./build.sh install`（只复制文件，不会启动游戏）。
2. 启动游戏，主菜单进入 **改装件**（Mods）界面，选中 **【情报】掌柜帮手·访客预测**，点 **审阅并启用**。
3. 从 1.3.0 起本 mod **不申请任何特殊权限**（游戏自带的扫描器对它报告“无权限、无发现”；1.2 及以前需要 `reflection-external`）。
   本 mod 不读写任何文件、不联网、不启动程序、不碰 Steam。
4. 按提示**重启游戏**后生效。

> 以后替换了 DLL 或 manifest（例如更新版本），游戏会认为 mod “已更改”，需要再点一次“审阅并启用”。

## 使用

- 进入存档，在**早上拉起卷帘门之前**按 **F9**：弹出“访客预测”平板，先显示“正在预测…”，一到几秒后出结果。同一天、状态没变时重复预测的结果基本一致（随机种子由天数决定）。
- 再按 F9、点平板右上角的 ✕ 或下方的 **关闭** 都能关掉窗口；**重新预测** 按钮立刻重跑一遍。窗口可以按住拖动（列表区域也能拖），鼠标在窗口上时点击不会穿透到后面的货物/柜台。按 Esc 一般也会像游戏的其他平板一样把它关掉。
- **开门之后**按 F9：标题变成 **实际队列**，显示游戏里**真实的排队名单**，每秒自动刷新——这不是预测，是真实数据。
- 已打烊（晚上）或还没进存档时按 F9，窗口里只显示一句提示。
- 输入文字时（例如在输入框里打字）热键不会触发。

> 从 1.1 升级：热键默认改成了 **F9**（创意工坊的“作弊菜单”默认占用 F8）。如果你之前保存的热键是 F8，而作弊菜单（`chezhou.cheat-menu`）已加载且也在用 F8，第一次加载 1.2.0 时会**自动改成 F9 一次**并在日志里写明；其他情况保留你原来的设置。之后随时可以在 mod 设置里改。
>
> 注意：开启了游戏的开发者工具时，F9 也会打开游戏自带的调试面板；那种情况下请把热键换成别的键。

### 窗口内容

窗口用的是游戏自己的平板界面（与“结束一天”“谈判”平板同款的外框、配色和中文字体），从上到下：

1. **标题**：`访客预测`（开门后为 `实际队列`）。
2. **信息行**：`第 N 天 · 星期X`。星期按游戏日历：天数 % 7，0 = 星期日 … 6 = 星期六。
3. **两块统计**：`必来 N 人`、`其他 a–b 人`（除必来者以外的来客人数，最少–最多；鼠标悬停可看平均值）。
4. **列表**（可滚动）：
   - 如果安全网报警，最上面是一条**红色横幅**；附加说明（如“有 N 次模拟出错”“革命军会在柜台放炸弹”）在它下面。
   - **必来**：每人一行，按排队位置排序；**早上在门口等**的人排在最前，带绿色 `门口等候` 标签。没有时显示“今天没有一定会来的人”。
   - **其他客人**：按来源分组，组标题写来源和该来源的预计人数（如 `随机顾客 · 约 1.8 人`），组按预计人数从多到少；组内按概率从高到低，右侧是概率条和百分比。概率低于 3% 的人折叠成一行 `其他 N 种（各 <3%）`，悬停可看全部名单。
5. **页脚**：模拟次数与用时、使用说明；下方是 **重新预测** / **关闭** 按钮。

每一行顾客的内容：

| 位置 | 内容 |
|---|---|
| 第一行左 | 名字（`等` = 同一类顾客在不同模拟里名字不同；`×2` = 同一轮里来了两个）＋标签：`门口等候`（绿）、`改造人`/`可能是改造人`/`通缉`（红）、`插队`、**阵营**（用游戏里各阵营的颜色：下层区、上层区、治安部、黑市、革命军、13区卡特尔；其他阵营为灰色） |
| 第一行右 | 必来：排队位置（`第 2–4 位` 为 10%–90% 分位；门口等候的写“柜台前”）；其他客人：概率条＋百分比；实际队列：`柜台前` / `排第 k 位` |
| 第二行 | 来做什么（买、卖、买卖、对话、检查、以物易物、收租、卖情报、委托订货…，绿色小标签）＋**想要**：买家是想要的商品类型（游戏里的中文类型名）或指定物品；卖家是 `卖：…`（来自数值手册整理的出货清单，查不到时写“卖货”）。太长时最多显示三行 |
| 第三行 | 预算（所有模拟里出现过的最小–最大值，已含声望特权、店铺吸引力等加成）· 来源（只在必来里显示；其他客人已按来源分组） |

鼠标悬停在一行上会弹出这位顾客的完整信息（包括没显示全的“想要”）。

来源是指这位顾客由哪一步生成：剧情、教程剧情、每周日程、展示柜买家、招牌与证书、随机顾客、声望特权、市场短缺过剩、市场事件、王尔德服务、通缉犯、改造人、治安检查、情报贩子、发明家、新面孔、化学品反馈、预约回访、房租日、已在队列、早上在门口等…

> 原生窗口建不起来时（例如在主菜单、游戏更新后界面结构变了，或在设置里关掉了 `nativeUI`），会自动退回 1.1 版的简易窗口（系统字体，内容相同，其他客人同样按来源分组），并在日志里写明原因。

## 配置

用的是游戏自带的模组设置，不在 DLL 旁边放配置文件（这样 mod 不需要申请“读写文件”权限）：

- 游戏内：改装件界面选中本 mod → **设置**；
- 或直接编辑 `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModConfigs\chaos5090.visitor-forecast.json`（首次运行后自动生成）。

| 键 | 默认 | 说明 |
|---|---|---|
| `hotkey` | `F9` | Unity KeyCode 名，例如 `F7`、`F10`、`Insert`、`Home`（1.1 及以前默认 F8） |
| `runs` | `300` | 每次预测模拟多少遍（20–2000）。越多越准，按键时卡顿越久 |
| `includeOtherMods` | `false` | 沙盒里是否也运行其他 mod 挂在“生成顾客”钩子上的代码（见限制） |
| `nativeUI` | `true` | 用游戏原生的平板窗口；关掉则用旧版简易窗口 |
| `checkCheatMenuHotkey` | `true` | 一次性检查：热键是 F8 且作弊菜单已加载并也用 F8 时改成 F9；检查完自动变成 `false`。想再检查一次就重新打开 |

## 原理

开门时游戏依次执行 `PlayerStore.OnShutterOpened()`：`CheckCrime()`（犯罪记录结算，影响治安检查）→ `SoldItem.HandleSoldItem()`（化学品反馈，可能产生回来投诉的顾客）→ `StoreClientManager.GenerateClient()`（约 30 个 `Handle*` 步骤）→ `HandleFutureClientQueue()`（预约回访）。之后 `OnLateShutterOpen()` 再安排“门口等候的人”。

每次预测：

1. **快照**：从 Assembly-CSharp 里**游戏类型的静态字段**出发，广度优先遍历所有可达的游戏数据对象（`PlayerStore`、`StoreClientManager`、`StoreClientData`、`SecData`、声望、网络升级、事件、静态缓存……）以及其中的 List/Dictionary/HashSet/Queue/数组，记录每个对象**自己的字段值**（浅拷贝，引用原样保存）。物品/背包/窗口这张 UI 图和 Unity 对象不进入快照，单独处理（见下）。
   - 每个游戏类型有哪些字段，来自**构建时生成的类型表**（`src/Generated/GameTypeRegistry.cs`，由 `tools/RegistryGen` 读取游戏的 `Assembly-CSharp.dll` 生成）：每个类型一行 `typeof(X).GetFields(...)`，所有字段查询的接收者都是字面量 `typeof(游戏类型)`。运行时只按对象的 `GetType()` 查表，不对计算出来的类型做成员查询——这就是不需要 `reflection-external` 权限的原因。
   - 集合只通过公开接口读写：List/数组用 `IList`（只改有变化的位置，多删少补，不清空），Dictionary 用 `IDictionary`（键没变只改值；沙盒只新增了键时倒序删掉；否则按快照顺序重建），HashSet/Queue/Stack 用按游戏字段里出现的元素类型生成的强类型还原器，ValueTuple 用 `ITuple` 读取各项。**内容和枚举顺序总是完全还原**；集合的容量等内部细节不再逐位还原（见“限制”）。
   - 不在类型表里的对象按不透明引用处理：游戏的**私有嵌套类型**（约 40 个，几乎都是界面用的，如 `NegociationUIManager.CachedRow`、`CustomUIDebugPanel.PanelDef`，以及只读的常量表 `ShareholderHelper.Dividend`）和编译器生成的类型（lambda 缓存、闭包）。它们都不参与顾客生成。
2. **沙盒运行 N 次**：每次用不同种子重置 `UnityEngine.Random`，把游戏可写字段里的 `System.Random`（`RNG.random` 等）换成新的替身实例（原实例的内部状态因此从不被消耗，也就不需要读取或还原它的内部）；游戏里另有两个 `static readonly` 的 `System.Random`（`BetaGenerator`、`BoxMullerGenerator`），换不掉，但游戏代码从不调用它们，对预测和存档都没有影响。然后直接调用游戏自己的 `OnShutterOpened()`，记录生成的队列；再判断门口等候的人。每次跑完：销毁本轮新建的物品/窗口的 GameObject，再把快照**写回同一批对象**（只写有变化的字段/集合）。全量检查太慢时自动改为“热点节点 + 每 16 轮一次全量”，最后一定全量写回。
3. **沙盒期间拦截的副作用**（Harmony 前缀，平时直接放行）：往柜台放东西（`TryAddDirectSellingItemToTable` 等）、销毁已有物品（`GameItemElement.Destroy`，例如已售区清理）、修改已有物品的标签/特性（`EnableTag/DisableTag/ModifyTag/AddItemFeature`、`StolenHelper.SetHeat`）、`PlantList.FreeNutrifruit`、声望弹窗（`ReputationUIManager.NotifyReputation/Notify`）、Steam 成就（`AchievementTracker.Unlock/IncrementCountAndUnlock`）、对话框（`DialogUIManager.StartDialogue`）、柜台物品刷新（`RefreshCounterItem`）；默认也不运行其他 mod 的生成钩子。期间 Unity 日志暂时关闭，避免几百遍生成把 Player.log 刷屏。
4. 恢复 `UnityEngine.Random.state`，打开日志。

### 发现的“生成顾客时会被改动的状态”（都在快照范围内）

`StoreClientManager` 的 `clientStack`、`clientTracker`（冷却）、`chemicalFeedbackStack`、`daySinceInformationClient`、`dayUntilInspection`/`inspectionSeeded`、`isTodayWantedQueued`/`daySinceWantedWasQueued`、`gunBuyerDaysMissed*`、`appraisalDaysMissed`、`wealthHistory`/`wildBailoutGiven`、`isAugSpawnHandledToday`；`PlayerStore` 的未来预约队列（`futurStoreClientIdQueue/DayQueue`）、`unseenMinorClient`、`nextAugSpawnDay`/`isAugEnded`、`daySinceLastBrowseUpgrade`、`displayCaseSetupMissedDays`、各种 `*Introduced`/`*Shown` 剧情标记、`soldItemsTracker`、`currentUniqueId`（新建物品的编号）、`lockShutterButtonFlag`；`StoreClientData` 的剧情字段（卢、埃莉诺、约翰·史密斯、收藏家、营业执照贿赂额等）；`SecData` 的犯罪记录与追踪值；远征雇员状态（`Hireling.Rest()`）；声望（犯罪网络服务在生成时给黑市 +2）；静态的 `ShowcaseHelper.pendingSupplierPicks`、`GraphHandler.windowGraphNodeRoots` 等。

另外，很多顾客的工厂方法在**创建时**就会 `DirectoryMaster.Item("$")` 之类地新建物品（对话选项图标），每件物品都会实例化十几个 GameObject——沙盒会跟踪（`GameItem`/`PixelWindow` 构造函数前缀 + 渲染根节点子物体比对）并在每轮结束时销毁。

### 安全网

预测前：抓快照 + 记录店内所有物品（含已售区）的指纹（id、唯一编号、数量、单价、特性数、所在背包、所有已启用标签的值）。
预测后：**重新独立遍历一遍**游戏状态，与预测前的快照逐个对象、逐个字段/元素对比，再比对物品指纹和 `Random.state`。

- 全部一致：正常显示结果。
- 有任何差异：窗口顶部**红字报警**，差异路径（如 `PlayerStore.instance.storeClientManager.clientTracker[xxx]: 2 -> 3`）写入 Player.log；在**重新读档（或开新档）之前不再允许预测**。此时建议**不要存档**，直接读档。

## 限制与注意事项

- **只预测开门那一刻生成的队列**。白天里由事件、电话、交易等临时插入的顾客（比如营业中途的 `AddNextClient`）不在预测内。
- 预测用的是按键那一刻的状态。开门前你改了展示柜、招牌、买了网络服务、换了声望，都要重新按 F9 或点“重新预测”。
- 门口等候的人是根据声望特权/剧情条件判断的；`OnShutterDoorOpenMorning` 里的报纸、提示纸条、秘密仰慕者礼物等不是顾客，不显示。
- 顾客的“卖什么”文本来自 `data/player/clients.json`（离线整理的数值手册），游戏更新后可能过时；买家想要的类型是运行时真实数据。
- `includeOtherMods=false`（默认）时，其他 mod 通过 `ModHook.OnGenerateCustomer*` 加的顾客不会出现在预测里；打开后会显示，但那些 mod 自己的内部状态不在快照范围内，被反复调用可能改变它们的状态（安全网也查不到）。
- 原生窗口会挡住鼠标点击；退回简易窗口时只挡住游戏里拖动/框选物品等操作，个别点击仍可能穿透，注意别误点。
- 首次预测会读取所有游戏类型的静态字段，可能提前触发少数类型的静态初始化（游戏迟早也会做）。
- 预测时游戏会卡顿一下（300 次预计一到几秒，取决于存档大小；每次的实际耗时会写进日志）；嫌慢可以把 `runs` 调小。
- 游戏更新后如果找不到关键的拦截目标，mod 会自动禁用预测功能并在日志里说明，而不会冒险运行。
- 快照用的类型表是按当前游戏版本生成的。游戏更新后新增的类型不在表里，它们的状态不受快照和安全网保护：mod 第一次预测时会检查，发现这种情况会在日志和窗口里提示“请更新 mod”（重新运行 `./build.sh` 即可重新生成）。
- 游戏更新还可能扩充扫描器的“受保护类型”名单（`ModScanner.GuardTypes`）。名单里的类型一旦出现在类型表里，游戏会**拒绝加载**本 mod（`loader-target: ... Type.GetFields on ...`）。1.3.1 起，生成器在构建时直接读取游戏当前的名单并排除这些类型，所以遇到这种拒绝时，重新 `./build.sh` 即可。可以用 `../../tools/ScanCheck` 在游戏外先检查一遍。
  - 1.3.0 就是这样被拒绝的：新版游戏把 `PaperBookRegistry`（mod 书本内容的登记表）和 `ModSteamAuth` 加进了名单。它们属于 mod 加载器这一层，与顾客生成无关，不进快照不影响预测和安全网。
- 集合的内容和顺序总能完全还原，但不再逐位还原内部数组（1.2 及以前会）。唯一可能的差别：某个 Dictionary/HashSet 在预测前就有被删除留下的空槽，而沙盒又往里加过键，那么以后再往这个集合加新键时，新键在枚举顺序里的位置可能与不预测时不同。这只影响少数集合的遍历顺序，不会丢失或改变任何数据。

## 反馈问题

请附上 Unity 日志：`%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log`（上一次运行的是 `Player-prev.log`），搜索 `[VisitorForecast]`。每次预测都会写一行统计（快照节点数、耗时、写回次数、拦截了哪些调用），报警时会列出差异路径。

## 从源码构建

```bash
cd mods/VisitorForecast
./build.sh            # 生成 bin/Release/VisitorForecast.dll 并组装 dist/VisitorForecast/
./build.sh install    # 另外复制到游戏的 Mods 目录（不启动游戏）
```

需要 .NET SDK（`~/.dotnet`，已用 8.0 验证；目标框架 netstandard2.1，与游戏的 Assembly-CSharp 一致），引用游戏 `Probably Stolen_Data/Managed` 下的程序集（不随 mod 分发）。游戏目录写在仓库根目录的 `local.env`（参考 `local.env.example`），也可用 `GAME_DIR=... ./build.sh` 临时覆盖。`tools/gen_client_tables.py` 从数值手册的数据（`local.env` 里的 `HANDBOOK_ROOT`，不在仓库里）生成“卖什么/买什么”的中文表，没有这份数据时沿用仓库里已生成的 `src/Generated/ClientTables.cs`；`tools/RegistryGen`（.NET 8 小工具）从游戏的 `Assembly-CSharp.dll` 生成快照用的类型表——游戏更新后重新 `./build.sh` 即可（找不到游戏目录时沿用已生成的文件）。

| 文件 | 作用 |
|---|---|
| `src/VisitorForecastMod.cs` | `IMod` 入口：读配置、打补丁、创建界面对象、订阅读档事件 |
| `src/Sandbox/StateGraph.cs` | 快照 / 写回 / 前后对比（字段来自类型表，集合走公开接口） |
| `src/Sandbox/GameTypes.cs` | 静态根、实例字段、哪些类型属于物品/界面图 |
| `src/Sandbox/GameTypeRegistry.cs` | 类型表的运行时部分与 HashSet/Queue/Stack 强类型还原器 |
| `src/Generated/GameTypeRegistry.cs`、`tools/RegistryGen/` | 构建时由游戏的 `Assembly-CSharp.dll` 生成的类型表（每个类型一行 `typeof(X).GetFields(...)`）及其生成器 |
| `src/Sandbox/SandboxGuard.cs` | 沙盒期间的拦截补丁、新建物品/窗口的跟踪与销毁 |
| `src/Sandbox/SourceTagger.cs` | 每个 `Handle*` 步骤的来源标签 |
| `src/Sandbox/Patches.cs` | 手动打补丁（找不到目标时只禁用对应功能） |
| `src/Sandbox/WorldFingerprint.cs` | 店内物品指纹 |
| `src/Forecast/ForecastEngine.cs` | 预测主流程与统计汇总 |
| `src/Forecast/Text.cs`、`src/Generated/ClientTables.cs` | 中文名称、类型名、卖什么/买什么 |
| `src/UI/ForecastBehaviour.cs` | 热键、预测调度、选择原生窗口或备用窗口 |
| `src/UI/NativeForecastWindow.cs` | 原生平板窗口（CustomUIManager + TabletUI，游戏中文字体） |
| `src/UI/ImguiForecastWindow.cs` | 备用的 IMGUI 窗口（系统中文字体） |
| `src/UI/ForecastView.cs` | 两种窗口共用的排序、按来源分组、阵营颜色 |
| `src/UI/UiPatches.cs` | 鼠标在窗口上时让游戏视为“在界面上”（`StoreUIManager.CheckRaycast` 后置补丁） |
