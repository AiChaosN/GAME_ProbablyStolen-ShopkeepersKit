#!/usr/bin/env bash
# 编译 SortToBoxes.dll 并组装 dist/SortToBoxes/（可直接复制到游戏的 Mods 目录）。
#   ./build.sh                 只编译 + 打包
#   ./build.sh install         另外复制到 "$GAME_DIR/Mods/SortToBoxes"（不会启动游戏；游戏开着时 DLL 被锁会失败）
# 游戏目录：环境变量 GAME_DIR > 仓库根目录 local.env > 默认的 Steam 安装位置。
set -euo pipefail
cd "$(dirname "$0")"

# 本机路径放在仓库根目录的 local.env（不进仓库，参考 local.env.example）
ENV_GAME_DIR="${GAME_DIR:-}"
if [[ -f ../../local.env ]]; then
  # shellcheck disable=SC1091
  source ../../local.env
fi
GAME_DIR="${ENV_GAME_DIR:-${GAME_DIR:-/mnt/c/Program Files (x86)/Steam/steamapps/common/Probably Stolen}}"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$PATH:$DOTNET_ROOT"
export DOTNET_ROLL_FORWARD=Major DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

# manifest 必须是合法 XML：游戏解析失败会直接跳过这个 mod（例如 & 要写成 &amp;）
python3 -I -c 'import sys, xml.etree.ElementTree as E; E.parse(sys.argv[1])' manifest.xml || { echo "manifest.xml 不是合法 XML，停止构建" >&2; exit 1; }

dotnet build -c Release -p:GameDir="$GAME_DIR"

rm -rf dist/SortToBoxes
mkdir -p dist/SortToBoxes
cp bin/Release/SortToBoxes.dll dist/SortToBoxes/
cp manifest.xml dist/SortToBoxes/
echo "dist: $(pwd)/dist/SortToBoxes"
ls -la dist/SortToBoxes

if [[ "${1:-}" == "install" ]]; then
  dest="$GAME_DIR/Mods/SortToBoxes"
  mkdir -p "$dest"
  cp dist/SortToBoxes/* "$dest/"
  echo "installed to: $dest"
fi
