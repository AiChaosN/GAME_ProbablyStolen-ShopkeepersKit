"""
准备上传到 Steam 创意工坊：把每个 mod 的 dist/<名字>/ 复制到 Windows 盘上的上传目录，并生成 SteamCMD 用的 VDF。
本脚本只整理本地文件，不登录、不上传；上传由你自己运行 SteamCMD 完成（见 workshop/README.zh-CN.md）。

用法（在仓库根目录）：
    python3 -I workshop/prepare.py                       # 所有 mod，新建的物品私密（只有自己看得见）
    python3 -I workshop/prepare.py SortToBoxes           # 只准备一个
    python3 -I workshop/prepare.py --visibility public --note "1.4.2：……"

可见性：新建物品默认私密；更新已有物品时不写可见性（保留你在网页上设的），除非用 --visibility 指定。

两种 SteamCMD 都支持（--steamcmd 指定；不指定时：local.env 里 STEAMCMD 指向的 steamcmd.sh 存在就用 WSL 版，否则用 Windows 版）：
  linux：WSL 里的 Linux 版 steamcmd（local.env 的 STEAMCMD，例如 /home/<你>/Tools/steamcmd/steamcmd.sh），VDF 里写 WSL 路径；
         上传目录默认 ~/ShopkeepersKit-upload。
  windows：Windows 版 C:\steamcmd\steamcmd.exe，VDF 里写 Windows 路径；上传目录必须在 Windows 盘上，默认 /mnt/c/ShopkeepersKit-upload。
上传目录也可以用 local.env 里的 UPLOAD_DIR 指定。
物品编号：workshop/ids.json。steamcmd 第一次上传成功后会把新物品的编号写回上传目录里的 VDF；
下次运行本脚本时会先把它读回来存进 ids.json（记得提交），以后就是更新同一项，不会重复新建。
做法与官方模组指南一致：https://github.com/dragonpan2/probably-stolen-modding（ModdingGuide “用 steamcmd 发布”）。
"""
import argparse, json, pathlib, re, shutil, sys, xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parent.parent
WS = ROOT / "workshop"
APPID = "4348910"   # Probably Stolen 的 Steam appid（创意工坊内容在 steamapps/workshop/content/4348910/）
MODS = ["SortToBoxes", "CounterOverflow", "VisitorForecast", "Conveniences", "FilterRecharge", "AdvancedScientist"]
VIS = {"public": "0", "friends": "1", "private": "2", "unlisted": "3"}


def local_env():
    env = {}
    f = ROOT / "local.env"
    if f.exists():
        for line in f.read_text(encoding="utf-8").splitlines():
            m = re.match(r'\s*([A-Z_]+)\s*=\s*"?(.*?)"?\s*$', line)
            if m and not line.lstrip().startswith("#"):
                env[m.group(1)] = m.group(2)
    return env


def win_path(p: pathlib.Path) -> str:
    """/mnt/c/a/b -> C:\\a\\b（SteamCMD 是 Windows 程序，读 WSL 路径不可靠）"""
    s = str(p)
    m = re.match(r"^/mnt/([a-zA-Z])/(.*)$", s)
    if not m:
        sys.exit(f"上传目录必须在 Windows 盘上（/mnt/<盘符>/...），现在是：{s}")
    return m.group(1).upper() + ":\\" + m.group(2).replace("/", "\\")


def vdf_str(s: str) -> str:
    # SteamCMD 读 VDF 时不认 \" 转义：文本里出现英文双引号会被当成字符串结束，后面的内容被截掉
    # （2026-11-11 归类入箱的创意工坊介绍就这样被截断了）。所以把英文双引号换成中文引号“”，交替成对。
    out, open_q = [], True
    for ch in s:
        if ch == '"':
            out.append("“" if open_q else "”")
            open_q = not open_q
        else:
            out.append(ch)
    return '"' + "".join(out).replace("\\", "\\\\") + '"'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mods", nargs="*", default=MODS)
    ap.add_argument("--visibility", choices=list(VIS), default=None,
                    help="不给时：新建物品用 private；更新已有物品不改可见性")
    ap.add_argument("--note", default=None, help="更新说明（changenote）；不给时写 “v<版本号>”")
    ap.add_argument("--meta-only", action="store_true",
                    help="只更新已有物品的标题和介绍，不上传 mod 文件和预览图（例如修改介绍文字时）")
    ap.add_argument("--description", default=None,
                    help="用这个文件代替 workshop/descriptions/<mod>.txt（例如用某个旧版本的介绍）")
    ap.add_argument("--steamcmd", choices=["linux", "windows"], default=None,
                    help="用 WSL 里的 Linux 版还是 Windows 版 SteamCMD（默认：local.env 的 STEAMCMD 存在就用 linux）")
    a = ap.parse_args()

    env = local_env()
    steamcmd_sh = env.get("STEAMCMD", "")
    mode = a.steamcmd or ("linux" if steamcmd_sh and pathlib.Path(steamcmd_sh).is_file() else "windows")
    if mode == "linux" and not (steamcmd_sh and pathlib.Path(steamcmd_sh).is_file()):
        sys.exit("用 WSL 版 SteamCMD 时，要在 local.env 里写 STEAMCMD=\"<steamcmd.sh 的完整路径>\"")
    default_upload = str(pathlib.Path.home() / "ShopkeepersKit-upload") if mode == "linux" else "/mnt/c/ShopkeepersKit-upload"
    upload = pathlib.Path(env.get("UPLOAD_DIR", default_upload))
    path_of = (lambda p: str(p)) if mode == "linux" else win_path
    ids_file = WS / "ids.json"
    ids = json.loads(ids_file.read_text(encoding="utf-8")) if ids_file.exists() else {}

    # steamcmd 上传成功后会把新物品编号写回 VDF：先读回来存进 ids.json
    changed = []
    for mod in MODS:
        old = upload / f"{mod}.vdf"
        if old.exists() and not ids.get(mod):
            m = re.search(r'"publishedfileid"\s+"(\d+)"', old.read_text(encoding="utf-8", errors="replace"))
            if m and m.group(1) != "0":
                ids[mod] = int(m.group(1))
                changed.append(f"{mod} = {m.group(1)}")
    if changed:
        ids_file.write_text(json.dumps(ids, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print("从上次上传的 VDF 读回物品编号，已写入 workshop/ids.json（记得提交）：" + "，".join(changed) + "\n")

    print("SteamCMD：" + ("WSL 版 " + steamcmd_sh if mode == "linux" else "Windows 版 C:\\steamcmd\\steamcmd.exe"))
    print(f"上传目录：{upload}" + (f"  ({win_path(upload)})" if mode == "windows" else "") + "\n")
    commands = []
    for mod in a.mods:
        if mod not in MODS:
            sys.exit(f"不认识的 mod：{mod}（可选 {', '.join(MODS)}）")
        dist = ROOT / "mods" / mod / "dist" / mod
        manifest = dist / "manifest.xml"
        if not manifest.exists():
            sys.exit(f"{mod}：找不到 {dist}，先在 mods/{mod} 里运行 ./build.sh")
        r = ET.parse(manifest).getroot()   # 不是合法 XML 会直接报错退出
        title, version = r.findtext("Name"), r.findtext("ModVersion")
        desc_file = pathlib.Path(a.description) if a.description else WS / "descriptions" / f"{mod}.txt"
        if not desc_file.exists():
            sys.exit(f"{mod}：缺少创意工坊介绍 {desc_file}")

        pid = str(ids.get(mod, 0) or 0)
        if a.meta_only:
            if pid == "0":
                sys.exit(f"{mod}：还没上传过，不能只更新介绍")
            lines = ['"workshopitem"', "{",
                     f'\t"appid"\t\t{vdf_str(APPID)}',
                     f'\t"publishedfileid"\t{vdf_str(pid)}',
                     f'\t"title"\t\t{vdf_str(title)}',
                     f'\t"description"\t{vdf_str(desc_file.read_text(encoding="utf-8").strip())}']
            if a.visibility:
                lines.append(f'\t"visibility"\t{vdf_str(VIS[a.visibility])}')
            if a.note:
                lines.append(f'\t"changenote"\t{vdf_str(a.note)}')
            lines += ["}", ""]
            vdf = upload / f"{mod}.meta.vdf"
            upload.mkdir(parents=True, exist_ok=True)
            vdf.write_text("\n".join(lines), encoding="utf-8")
            print(f"{mod}：只更新标题和介绍（物品 {pid}）" + (f"，可见性改为 {a.visibility}" if a.visibility else "") + f"，不传文件\n  配置：{path_of(vdf)}")
            commands.append(f'+workshop_build_item "{path_of(vdf)}"')
            continue

        out = upload / mod
        content = out / "content"
        if content.exists():
            shutil.rmtree(content)
        content.mkdir(parents=True)
        for f in dist.iterdir():   # 内容根目录直接放 manifest.xml 和 DLL（不要再套一层文件夹）；贴图等子文件夹整个复制
            if f.is_dir():
                shutil.copytree(f, content / f.name)
            else:
                shutil.copy2(f, content / f.name)
        preview_src = WS / "previews" / f"{mod}.png"
        preview = None
        if preview_src.exists():
            preview = out / "preview.png"
            shutil.copy2(preview_src, preview)
            shutil.copy2(preview_src, content / "preview.png")   # 游戏离线、拿不到创意工坊预览图时用文件夹里这张

        lines = [
            '"workshopitem"', "{",
            f'\t"appid"\t\t{vdf_str(APPID)}',
            f'\t"publishedfileid"\t{vdf_str(pid)}',
            f'\t"contentfolder"\t{vdf_str(path_of(content))}',
        ]
        if preview:
            lines.append(f'\t"previewfile"\t{vdf_str(path_of(preview))}')
        vis = a.visibility or ("private" if pid == "0" else None)
        if vis:
            lines.append(f'\t"visibility"\t{vdf_str(VIS[vis])}')
        lines += [
            f'\t"title"\t\t{vdf_str(title)}',
            f'\t"description"\t{vdf_str(desc_file.read_text(encoding="utf-8").strip())}',
            f'\t"changenote"\t{vdf_str(a.note or ("v" + version))}',
            "}", "",
        ]
        vdf = upload / f"{mod}.vdf"
        vdf.write_text("\n".join(lines), encoding="utf-8")
        state = "新建" if pid == "0" else f"更新 {pid}"
        print(f"{mod} {version}：{state}，{('可见性 ' + vis) if vis else '可见性不变'}，预览图 {'有' if preview else '没有（workshop/previews/' + mod + '.png）'}")
        print(f"  内容：{path_of(content)}\n  配置：{path_of(vdf)}")
        commands.append(f'+workshop_build_item "{path_of(vdf)}"')

    if mode == "linux":
        print("\n在 WSL 里运行（把 你的Steam账号 换成你的登录名；第一次会问密码和手机验证码，之后会记住登录）：")
        print(f'  "{steamcmd_sh}" +login 你的Steam账号 ' + " ".join(commands) + " +quit")
    else:
        print("\n在 Windows 的 PowerShell 里运行（把 你的Steam账号 换成你的登录名；密码和手机验证码会在窗口里问你）：")
        print("  C:\\steamcmd\\steamcmd.exe +login 你的Steam账号 " + " ".join(commands) + " +quit")
    print("\n第一次上传后，steamcmd 会把新物品编号写回 VDF；再运行一次本脚本就会把编号存进 workshop/ids.json，记得提交。")


if __name__ == "__main__":
    main()
