#!/usr/bin/env bash
# 编译 VisitorForecast.dll 并组装 dist/VisitorForecast/（可直接复制到游戏的 Mods 目录）。
#   ./build.sh                 只编译 + 打包
#   ./build.sh install         另外复制到 "$GAME_DIR/Mods/VisitorForecast"（不会启动游戏）
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

# 卖什么/买什么的中文表由数值手册的数据生成（HANDBOOK_ROOT 见 local.env.example；数据不在时沿用已生成的文件）
if [[ -n "${HANDBOOK_ROOT:-}" && -f "$HANDBOOK_ROOT/data/clients.json" && -f "$HANDBOOK_ROOT/data/player/clients.json" ]]; then
  python3 -I tools/gen_client_tables.py "$HANDBOOK_ROOT" src/Generated/ClientTables.cs
fi

# 游戏类型字段表（每个类型一行 typeof(X).GetFields(...)，见 src/Sandbox/GameTypeRegistry.cs）由游戏的 Assembly-CSharp.dll 生成；
# 找不到游戏目录时沿用已生成的文件
MANAGED="$GAME_DIR/Probably Stolen_Data/Managed"
if [[ -f "$MANAGED/Assembly-CSharp.dll" ]]; then
  dotnet run --project tools/RegistryGen -c Release -- "$MANAGED" src/Generated/GameTypeRegistry.cs
fi

dotnet build -c Release -p:GameDir="$GAME_DIR"

rm -rf dist/VisitorForecast
mkdir -p dist/VisitorForecast
cp bin/Release/VisitorForecast.dll dist/VisitorForecast/
cp manifest.xml dist/VisitorForecast/
echo "dist: $(pwd)/dist/VisitorForecast"
ls -la dist/VisitorForecast

if [[ "${1:-}" == "install" ]]; then
  dest="$GAME_DIR/Mods/VisitorForecast"
  mkdir -p "$dest"
  cp dist/VisitorForecast/* "$dest/"
  echo "installed to: $dest"
fi
