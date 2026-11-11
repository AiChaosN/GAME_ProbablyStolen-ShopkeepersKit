# Uploading to the Steam Workshop

**English** | [中文](README.zh-CN.md)

*Probably Stolen* has no upload button; items are published with Valve's command-line tool **SteamCMD** (`workshop_build_item`). The game's appid is **4348910**. This is also what the [official modding guide](https://github.com/dragonpan2/probably-stolen-modding) describes (ModdingGuide, "Publishing with steamcmd").

Logging into Steam needs your own password and Steam Guard code, so you run the final step yourself. This folder prepares everything else:

| File | Contents |
|---|---|
| `descriptions/<mod>.txt` | Workshop page description (BBCode, Chinese first, then English) |
| `previews/<mod>.png` | Preview image (add your own; square or 16:9, under 1 MB). Uploaded as the Workshop preview, and also copied into the mod folder as `preview.png` (the Mods menu uses it when the game is offline) |
| `ids.json` | Each mod's Workshop item ID. 0 = not uploaded yet |
| `prepare.py` | Copies `mods/<mod>/dist/<mod>/` to an upload folder on a Windows drive, writes the SteamCMD VDF and prints the upload command |

## First upload

1. **Install SteamCMD** (once): download Valve's `https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip`, unzip to `C:\steamcmd\`, run `C:\steamcmd\steamcmd.exe` once in PowerShell to let it update, then type `quit`.
2. **Accept the Workshop agreement** (once): log into Steam in a browser and accept `https://steamcommunity.com/sharedfiles/workshoplegalagreement`. Otherwise uploaded items stay hidden.
3. **Add a preview** (optional but recommended): save an in-game screenshot as `workshop/previews/<mod>.png`, e.g. `SortToBoxes.png`.
4. **Build and prepare** (in WSL, at the repo root):
   ```bash
   (cd mods/SortToBoxes && ./build.sh)          # build every mod you upload first
   python3 -I workshop/prepare.py SortToBoxes   # new items are public by default; updates keep the current visibility
   ```
   Without a mod name it prepares all of them. It prints the command for the next step.
5. **Upload** (in Windows PowerShell): run the printed command, e.g.
   ```
   C:\steamcmd\steamcmd.exe +login YOUR_STEAM_LOGIN +workshop_build_item "C:\ShopkeepersKit-upload\SortToBoxes.vdf" +quit
   ```
   It asks for your password and Steam Guard code.
6. **Record the item ID**: after a successful upload, steamcmd writes the new item's ID back into `C:\ShopkeepersKit-upload\<mod>.vdf`. Run `python3 -I workshop/prepare.py` once more and it copies the ID into `workshop/ids.json`; commit that file. **Later uploads then update the same item; without the ID every upload creates a duplicate.**

## Uploading from WSL (no need to switch to Windows)

You can also use the Linux SteamCMD and upload straight from WSL:

1. Download and unpack (once):
   ```bash
   mkdir -p ~/Tools/steamcmd && cd ~/Tools/steamcmd
   curl -fsSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar -xz
   ```
2. Install the 32-bit runtime (once, asks for your sudo password; on Ubuntu 22.04 there's no need to add the i386 architecture):
   ```bash
   sudo apt-get install -y lib32gcc-s1
   ```
3. Put `STEAMCMD="/home/<you>/Tools/steamcmd/steamcmd.sh"` in `local.env`. From then on `prepare.py` uses the WSL SteamCMD by default: WSL paths in the VDF, upload folder `~/ShopkeepersKit-upload`, and the printed command runs in WSL. Add `--steamcmd windows` to use the Windows one instead.
4. The first `steamcmd.sh +login <your Steam account> +quit` asks for your password and Steam Guard code; after a successful login it is remembered, so later uploads ask once or not at all.

## Testing privately first (optional)

New items are public by default: normally you test the local build first and then upload. If you want to upload privately and test the Workshop copy first, add `--visibility private` when uploading, then:

1. Open your item on the Workshop (Steam profile → Workshop Items) and **subscribe**.
2. **Move the local copy out of the game's `Mods` folder first** (e.g. to a backup on the desktop). With both present the game prefers the local copy, and a changed source makes the game ask for review again.
3. Start the game → Mods → Review and enable → restart, and check it works.
4. Then set the item's visibility to **Public** on its page, or rerun `prepare.py --visibility public` and upload again. You can add tags and more screenshots on the item page.

## Updating later

```bash
(cd mods/SortToBoxes && ./build.sh)
python3 -I workshop/prepare.py SortToBoxes --visibility public --note "1.4.3: ..."
```
Then run the printed command in PowerShell. With the ID in `ids.json`, it updates the existing item and subscribers get the new version automatically.

## Testing new versions the easy way

From the official guide: once a local mod in `Mods/` has been enabled, its details panel shows a **Developer mode** button. With it on, every recompile of that folder loads directly without another "Review and enable" (per mod, per computer). Also, when a local folder has the same ID as a subscribed Workshop item, the game uses the local copy, so you can test a new version without unsubscribing.

## Notes

- The upload's root folder holds `manifest.xml` and the DLL directly (`prepare.py` arranges this); don't nest another folder.
- Make sure `mods/<mod>/dist/` is a fresh build; `build.sh` checks the manifest is valid XML, and `tools/ScanCheck` confirms the game's scanner won't refuse it (in game: create `dev.txt` to enable the dev tools, then run `mod-scan <mod ID>` in the F8 console).
- Bump `<ModVersion>` for every release; keep `<GameVersions>` at the current game version (currently `0.46D`); the Mods menu uses it to color compatibility.
- The descriptions credit Claude Code and link the GitHub source; edit `descriptions/` to change them.
