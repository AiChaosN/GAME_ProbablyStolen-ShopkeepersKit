"""职业图标 as_icon.png（19×19）：照游戏职业图标的样式（icons_*：单色 #292234，外面一个圆圈，中间一个小符号）。
中间画一块带针脚的芯片（模组工艺）。用法：python3 tools/make_icon.py Textures/Items/as_icon.png"""
import sys
from PIL import Image

INK = (41, 34, 52, 255)
RING = [
    ".......aaaaa.......",
    ".....aa.....aa.....",
    "...aa.........aa...",
    "..aa...........aa..",
    "..a.............a..",
    ".a...............a.",
    ".a...............a.",
    "a.................a",
    "a.................a",
    "a.................a",
    "a.................a",
    "a.................a",
    ".a...............a.",
    ".a...............a.",
    "..a.............a..",
    "..aa...........aa..",
    "...aa.........aa...",
    ".....aa.....aa.....",
    ".......aaaaa.......",
]
HAT = [          # 11 宽，从 (4, 4) 开始：一块芯片，四边是针脚，中间一个方形核心
    "..a.a.a.a..",
    ".aaaaaaaaa.",
    "aa.......aa",
    ".a.aaaaa.a.",
    "aa.a...a.aa",
    ".a.a.a.a.a.",
    "aa.a...a.aa",
    ".a.aaaaa.a.",
    "aa.......aa",
    ".aaaaaaaaa.",
    "..a.a.a.a..",
]
im = Image.new("RGBA", (19, 19), (0, 0, 0, 0))
px = im.load()
for y, row in enumerate(RING):
    for x, c in enumerate(row):
        if c == "a":
            px[x, y] = INK
for y, row in enumerate(HAT):
    for x, c in enumerate(row):
        if c == "a":
            px[4 + x, 4 + y] = INK
out = sys.argv[1] if len(sys.argv) > 1 else "as_icon.png"
im.save(out)
print("icon:", out)
