#!/usr/bin/env bash
# 编译 Conveniences.dll 并组装 dist/Conveniences/（可直接复制到游戏的 Mods 目录）。
#   ./build.sh                 只编译 + 打包
#   ./build.sh install         另外复制到 "$GAME_DIR/Mods/Conveniences"（不会启动游戏；游戏开着时 DLL 被锁会失败）
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

rm -rf dist/Conveniences
mkdir -p dist/Conveniences
cp bin/Release/Conveniences.dll dist/Conveniences/
cp manifest.xml dist/Conveniences/
echo "dist: $(pwd)/dist/Conveniences"
ls -la dist/Conveniences

if [[ "${1:-}" == "install" ]]; then
  dest="$GAME_DIR/Mods/Conveniences"
  mkdir -p "$dest"
  # 先拷 DLL：游戏开着时 DLL 被锁，拷不进去就停下，别让 manifest 先被换成新版（DLL 和 manifest 版本不一致）
  if ! cp dist/Conveniences/*.dll "$dest/"; then
    echo "DLL 拷不进去（游戏开着时 DLL 被锁住）：没有安装，游戏里的 mod 保持原样。关掉游戏后再运行 ./build.sh install" >&2
    exit 1
  fi
  cp dist/Conveniences/manifest.xml "$dest/"
  echo "installed to: $dest"
fi
