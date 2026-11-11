# 上传到 Steam 创意工坊

[English](README.md) | **中文**

《Probably Stolen》游戏里没有上传按钮，要用 Valve 的命令行工具 **SteamCMD**（`workshop_build_item` 命令）发布，游戏的 appid 是 **4348910**。这也是[官方模组指南](https://github.com/dragonpan2/probably-stolen-modding)（ModdingGuide 的“用 steamcmd 发布”一节）给的做法。

登录 Steam 要你本人输入密码和手机验证码，所以最后一步只能你自己运行。本目录准备好了其余所有东西：

| 文件 | 内容 |
|---|---|
| `descriptions/<mod>.txt` | 创意工坊页面的介绍（BBCode，中文在前、英文在后） |
| `previews/<mod>.png` | 预览图（自己放；建议正方形或 16:9，小于 1 MB）。会作为创意工坊预览图上传，同时也放一份 `preview.png` 进 mod 文件夹（游戏离线时改装件菜单用它） |
| `ids.json` | 每个 mod 在创意工坊的物品编号。0 = 还没上传过 |
| `prepare.py` | 把 `mods/<mod>/dist/<mod>/` 复制到 Windows 盘上的上传目录，生成 SteamCMD 用的 VDF，打印上传命令 |

## 第一次上传

1. **装 SteamCMD**（只做一次）：从 Valve 官方下载 `https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip`，解压到 `C:\steamcmd\`，在 PowerShell 里运行一次 `C:\steamcmd\steamcmd.exe`，等它自动更新完，输入 `quit` 退出。
2. **接受创意工坊协议**（只做一次）：浏览器登录 Steam，打开 `https://steamcommunity.com/sharedfiles/workshoplegalagreement` 点同意。不同意的话，上传的物品会一直是隐藏的。
3. **放预览图**（可选，但强烈建议）：截一张游戏里的图，存成 `workshop/previews/<mod>.png`，例如 `SortToBoxes.png`。
4. **构建并准备**（在 WSL 里、仓库根目录）：
   ```bash
   (cd mods/SortToBoxes && ./build.sh)          # 每个要上传的 mod 都先构建
   python3 -I workshop/prepare.py SortToBoxes   # 新建时默认直接公开；更新已有物品时不改可见性
   ```
   不写 mod 名就是全部都准备。它会打印出下一步要运行的命令。
5. **上传**（在 Windows 的 PowerShell 里）：运行上一步打印的命令，例如
   ```
   C:\steamcmd\steamcmd.exe +login 你的Steam账号 +workshop_build_item "C:\ShopkeepersKit-upload\SortToBoxes.vdf" +quit
   ```
   窗口里会问密码和 Steam 令牌验证码。
6. **记下物品编号**：上传成功后，steamcmd 会把新物品的编号写回 `C:\ShopkeepersKit-upload\<mod>.vdf`。再运行一次 `python3 -I workshop/prepare.py`，它会把编号读回来存进 `workshop/ids.json`，然后提交到仓库。**以后再上传就是更新同一项；没记下编号的话每次都会新建一个重复的物品。**

## 在 WSL 里上传（不用切到 Windows）

也可以用 Linux 版 SteamCMD，在 WSL 里直接上传：

1. 下载解压（只做一次）：
   ```bash
   mkdir -p ~/Tools/steamcmd && cd ~/Tools/steamcmd
   curl -fsSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar -xz
   ```
2. 装 32 位运行库（只做一次，要输入你的 sudo 密码；Ubuntu 22.04 不用另外添加 i386 架构）：
   ```bash
   sudo apt-get install -y lib32gcc-s1
   ```
3. 在 `local.env` 里写上 `STEAMCMD="/home/<你>/Tools/steamcmd/steamcmd.sh"`。之后 `prepare.py` 默认用 WSL 版：VDF 里写 WSL 路径，上传目录默认 `~/ShopkeepersKit-upload`，最后打印的是 WSL 里运行的命令。想临时用 Windows 版就加 `--steamcmd windows`。
4. 第一次运行 `steamcmd.sh +login 你的Steam账号 +quit` 会问密码和手机验证码；登录成功后会记住，之后上传一般只问一次或不再问。

## 先私密测试（可选）

新物品默认直接公开：平时先用本地版测好再上传。如果想先私密上传、订阅创意工坊版测一遍，上传时加 `--visibility private`，测完按下面做：

1. 在创意工坊打开你的物品（Steam 个人资料 → 创意工坊物品），点**订阅**。
2. **先把游戏 `Mods` 文件夹里同名的本地版本挪走**（例如移到桌面备份）。本地版和创意工坊版同时存在时游戏优先用本地版；而且来源变了，游戏会要求重新确认。
3. 开游戏 → 改装件 → 审阅并启用 → 重启，确认能正常用。
4. 没问题后在物品页面把可见性改成**公开**；或者重新运行 `prepare.py --visibility public` 再上传一次。物品页面上也可以补标签、加更多截图。

## 以后更新版本

```bash
(cd mods/SortToBoxes && ./build.sh)
python3 -I workshop/prepare.py SortToBoxes --visibility public --note "1.4.3：……"
```
然后在 PowerShell 里运行打印出的命令。`ids.json` 里有编号，就会更新原来那一项，订阅的人会自动收到新版本。

## 自己测试新版本的省事办法

官方指南里说：本地 `Mods/` 里的 mod 启用过一次后，详情面板会出现**开发者模式**按钮。打开后，这个文件夹每次重新编译都会直接加载，不用再“审阅并启用”（只对本机、本 mod 生效）。另外，本地文件夹和已订阅的创意工坊物品 ID 相同时，游戏用本地副本，这样不用取消订阅就能测试新版本。

## 注意

- 上传内容的根目录直接就是 `manifest.xml` 和 DLL（`prepare.py` 已经这样整理），不要再套一层文件夹。
- 上传前确认 `mods/<mod>/dist/` 是刚构建的版本；`build.sh` 会检查 manifest 是不是合法 XML，`tools/ScanCheck` 可以再确认游戏扫描器不会拒绝它（游戏里也能自查：创建 `dev.txt` 打开开发工具后，F8 控制台运行 `mod-scan <mod ID>`）。
- `<ModVersion>` 每次发布都要递增；`<GameVersions>` 写当前游戏版本（现在是 `0.46D`），改装件菜单靠它显示兼容性颜色。
- 介绍里写了“由 Claude Code 协助开发”和 GitHub 源码链接，改介绍就改 `descriptions/` 里的文件。
