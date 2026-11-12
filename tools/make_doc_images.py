"""生成说明图（README 和创意工坊介绍用）。
用法（仓库根目录）：python3 -I tools/make_doc_images.py
需要 Pillow 和两种字体：中文 Droid Sans Fallback（只有汉字）+ 英文 DejaVu Sans；Ubuntu 上分别在
fonts-droid-fallback、fonts-dejavu-core 包里。中英文混排时按字符分段，各用各的字体。
"""
import pathlib
from PIL import Image, ImageDraw, ImageFont

ROOT = pathlib.Path(__file__).resolve().parent.parent
CJK = "/usr/share/fonts/truetype/droid/DroidSansFallbackFull.ttf"
LAT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
LATB = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

BG = (49, 44, 59)
PANEL = (62, 57, 75)
INK = (236, 232, 240)
SUB = (176, 170, 190)
DIM = (130, 124, 145)
KEY = (232, 228, 238)
KEY_INK = (40, 36, 48)
GREEN = (120, 220, 140)
RED = (235, 100, 100)
AMBER = (240, 180, 60)

_fonts = {}


def font(path, size):
    k = (path, size)
    if k not in _fonts:
        _fonts[k] = ImageFont.truetype(path, size)
    return _fonts[k]


# 中文字体里没有这些符号（会画成方框），交给英文字体
LATIN_PUNCT = set("·“”‘’…—–×→↓±")


def runs(text):
    """按字符分段：ASCII 用英文字体，其余（汉字、全角标点）用中文字体。"""
    out, cur, ascii_ = [], "", None
    for ch in text:
        a = ord(ch) < 128 or ch in LATIN_PUNCT
        if ascii_ is None or a == ascii_:
            cur += ch
        else:
            out.append((cur, ascii_))
            cur = ch
        ascii_ = a
    if cur:
        out.append((cur, ascii_))
    return out


def text_width(d, text, size, bold=False):
    return sum(d.textlength(t, font=font((LATB if bold else LAT) if a else CJK, size)) for t, a in runs(text))


def draw_text(d, xy, text, size, fill, bold=False, anchor="l"):
    """混排文字；y 是这一行的竖直中线。anchor：l 左对齐 / m 居中 / r 右对齐。"""
    x, y = xy
    w = text_width(d, text, size, bold)
    if anchor == "m":
        x -= w / 2
    elif anchor == "r":
        x -= w
    for t, a in runs(text):
        f = font((LATB if bold else LAT) if a else CJK, size)
        d.text((x, y), t, font=f, fill=fill, anchor="lm")
        x += d.textlength(t, font=f)
    return x


def keycap(d, x, y, label, size=26):
    """画一个键帽，(x, y) 是左边缘、竖直中线。返回右边缘。"""
    w = max(44, text_width(d, label, size, bold=True) + 24)
    h = 42
    d.rounded_rectangle([x, y - h / 2 + 3, x + w, y + h / 2 + 3], radius=8, fill=(150, 146, 160))
    d.rounded_rectangle([x, y - h / 2, x + w, y + h / 2], radius=8, fill=KEY)
    draw_text(d, (x + w / 2, y), label, size, KEY_INK, bold=True, anchor="m")
    return x + w


def keys(d, x, y, combo):
    """combo 例如 "Ctrl+G"：拆成多个键帽，中间画 +。"""
    parts = combo.split("+")
    for i, k in enumerate(parts):
        if i:
            x = draw_text(d, (x + 8, y), "+", 24, SUB) + 8
        x = keycap(d, x, y, k)
    return x


# ---------------------------------------------------------------------------- 归类入箱：按键一览

def sort_to_boxes_keys():
    rows = [
        ("收货（归箱）", "Storing", [
            ("G", "", "仓库里散放的货放进对应的箱子", "sort loose items in storage into their boxes"),
            ("G", "指着柜台", "柜台上你的货收回箱子 / 仓库", "take your goods on the counter back"),
            ("Ctrl+G", "指着箱子", "打开这个箱子的分类面板", "open the box's category panel"),
            ("Shift+G", "", "开关“买下时自动归箱”（默认关）", "toggle auto-sort on purchase (off)"),
        ]),
        ("卖货（上柜台）", "Selling", [
            ("C", "", "柜台前的买家要的货全部摆上柜台", "put everything the buyer wants on the counter"),
            ("C", "指着库存里的货", "它的同款全部摆上柜台", "put all identical items on the counter"),
            ("C", "指着柜台上的货", "同款全部收回", "take the identical items back"),
            ("Shift+C", "", "开关“放一件带同款”（默认关）", "toggle bring identical items (off)"),
        ]),
    ]
    W = 1280
    row_h, head_h = 74, 64
    H = 130 + sum(head_h + len(r[2]) * row_h + 24 for r in rows) + 70
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    draw_text(d, (W / 2, 62), "归类入箱 · 按键一览", 46, INK, anchor="m")
    draw_text(d, (W / 2, 106), "Sort To Boxes · Keys", 24, SUB, anchor="m")
    y = 140
    for title_zh, title_en, items in rows:
        d.rounded_rectangle([40, y, W - 40, y + head_h + len(items) * row_h + 8], radius=14, fill=PANEL)
        draw_text(d, (68, y + head_h / 2 + 4), title_zh, 32, AMBER)
        draw_text(d, (68 + text_width(d, title_zh, 32) + 16, y + head_h / 2 + 6), title_en, 22, DIM)
        yy = y + head_h + row_h / 2 - 6
        for combo, where, zh, en in items:
            x = keys(d, 80, yy, combo)
            if where:
                draw_text(d, (x + 14, yy), "（" + where + "）", 24, SUB)
            draw_text(d, (520, yy - 14), zh, 28, INK)
            draw_text(d, (520, yy + 20), en, 20, DIM)
            yy += row_h
        y += head_h + len(items) * row_h + 32
    draw_text(d, (W / 2, H - 40), "所有按键都能在游戏“选项”里改  ·  all keys can be rebound in the game's Options", 22, SUB, anchor="m")
    out = ROOT / "mods" / "SortToBoxes" / "docs" / "images" / "keys.png"
    im.save(out, optimize=True)
    return out


# ---------------------------------------------------------------------------- 柜台不丢货：原版 vs 本 mod

ITEM_COLORS = [(110, 170, 220), (220, 150, 90), (150, 200, 120), (200, 120, 180), (230, 200, 90), (140, 140, 220)]


def counter(d, x, y, cols, rows, cell, items, dim_missing=False):
    """画一个柜台格子；items = [(col, row, w, h, colorIndex)]。"""
    d.rounded_rectangle([x - 8, y - 8, x + cols * cell + 8, y + rows * cell + 8], radius=8, fill=(88, 74, 64))
    for c in range(cols):
        for r in range(rows):
            d.rectangle([x + c * cell + 1, y + r * cell + 1, x + (c + 1) * cell - 1, y + (r + 1) * cell - 1], fill=(70, 58, 50))
    for c, r, w, h, k in items:
        d.rounded_rectangle([x + c * cell + 3, y + r * cell + 3, x + (c + w) * cell - 3, y + (r + h) * cell - 3], radius=6,
                            fill=ITEM_COLORS[k % len(ITEM_COLORS)])


def counter_overflow_explain():
    W, H = 1280, 760
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    draw_text(d, (W / 2, 56), "柜台不丢货：卖家的货柜台放不下时", 42, INK, anchor="m")
    draw_text(d, (W / 2, 98), "Counter Overflow: when a seller's goods don't fit on the counter", 22, SUB, anchor="m")

    cell = 34
    shown = [(0, 0, 2, 2, 0), (2, 0, 1, 2, 1), (3, 0, 2, 1, 2), (3, 1, 1, 1, 3), (4, 1, 1, 1, 4), (5, 0, 2, 2, 5), (7, 0, 1, 1, 1), (7, 1, 1, 1, 2)]
    extra = [(0, 0, 2, 2, 3), (2, 0, 2, 1, 4), (2, 1, 1, 1, 0), (4, 0, 2, 2, 1), (6, 0, 1, 2, 2)]

    # 左：原版
    lx = 60
    d.rounded_rectangle([40, 130, 620, 700], radius=14, fill=PANEL)
    draw_text(d, (330, 168), "原版", 34, RED, anchor="m")
    draw_text(d, (330, 204), "base game", 20, DIM, anchor="m")
    counter(d, lx + 30, 250, 8, 2, cell, shown)
    draw_text(d, (330, 350), "柜台摆满了，后面的货……", 26, INK, anchor="m")
    for i, (c, r, w, h, k) in enumerate(extra):
        x0 = lx + 60 + i * 96
        d.rounded_rectangle([x0, 400, x0 + w * cell - 6, 400 + h * cell - 6], radius=6, fill=ITEM_COLORS[k])
        d.line([x0 - 6, 394, x0 + w * cell, 400 + h * cell], fill=RED, width=5)
        d.line([x0 + w * cell, 394, x0 - 6, 400 + h * cell], fill=RED, width=5)
    draw_text(d, (330, 510), "直接销毁，没有任何提示", 30, RED, anchor="m")
    draw_text(d, (330, 548), "silently destroyed", 20, DIM, anchor="m")
    draw_text(d, (330, 610), "一次可能丢掉二十多件", 24, SUB, anchor="m")
    draw_text(d, (330, 642), "(20+ items at once)", 18, DIM, anchor="m")

    # 右：本 mod
    rx = 680
    d.rounded_rectangle([660, 130, 1240, 700], radius=14, fill=PANEL)
    draw_text(d, (950, 168), "本 mod", 34, GREEN, anchor="m")
    draw_text(d, (950, 204), "with this mod", 20, DIM, anchor="m")
    # 翻页条
    bx, by = rx + 50, 252
    bar_w = 480
    d.rounded_rectangle([bx, by - 22, bx + bar_w, by + 22], radius=8, fill=(40, 36, 48))
    x = keycap(d, bx + 8, by, "Z", 22)
    draw_text(d, (x + 8, by), "上一页", 22, SUB)
    draw_text(d, (bx + bar_w / 2, by), "第 1/2 页", 24, INK, anchor="m")
    rx2 = bx + bar_w - 8 - max(44, text_width(d, "X", 22, bold=True) + 24)
    draw_text(d, (rx2 - 8, by), "下一页", 22, SUB, anchor="r")
    keycap(d, rx2, by, "X", 22)
    counter(d, rx + 30 + 72, 300, 8, 2, cell, shown)
    draw_text(d, (950, 400), "放不下的货放到第 2 页", 26, INK, anchor="m")
    counter(d, rx + 30 + 72, 440, 8, 2, cell, extra)
    draw_text(d, (950, 540), "每页记住每件货的位置，拿下来的还能放回去", 22, SUB, anchor="m")
    draw_text(d, (950, 590), "批发：所有页一起买下，批发价已包含", 26, AMBER, anchor="m")
    draw_text(d, (950, 626), "wholesale buys every page", 18, DIM, anchor="m")
    draw_text(d, (950, 664), "晚上杰克逊博士的店里也一样", 22, SUB, anchor="m")

    out = ROOT / "mods" / "CounterOverflow" / "docs" / "images" / "how-it-works.png"
    im.save(out, optimize=True)
    return out


def advanced_scientist_machines():
    """高级科学家：三台机器和两种芯片（mod 自己的贴图放大），每台写上槽位和用途，中英双语。"""
    tex = ROOT / "mods" / "AdvancedScientist" / "Textures" / "Items"
    W, H = 1280, 760
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    draw_text(d, (W / 2, 44), "高级科学家的机器 · Advanced Scientist machines", 38, INK, bold=True, anchor="m")
    draw_text(d, (W / 2, 88), "点窗口标题栏的按钮立刻做，每次耗 30 电 · click the title-bar button; 30 charge per use", 21, SUB, anchor="m")

    def sprite(name, scale):
        s = Image.open(tex / (name + ".png")).convert("RGBA")
        return s.resize((s.width * scale, s.height * scale), Image.NEAREST)

    cards = [
        ("as_blank_module_maker", "空白模组制造机", "Blank Module Maker", "制造 · Make",
         ["电源 → 材料 → 产出", "Power → Materials → Output"]),
        ("as_affix_extractor", "词条提取机", "Affix Extractor", "提取 · Extract",
         ["电源 → 模组 → 产出", "Power → Module → Output"]),
        ("as_affix_inlayer", "词条镶嵌机", "Affix Inlayer", "镶嵌 · Inlay",
         ["电源 → 空白模组 → 芯片", "Power → Blank module → Chips"]),
    ]
    cw = (W - 80 - 2 * 20) // 3
    for i, (spr, zh, en, btn, lines) in enumerate(cards):
        cx = 40 + i * (cw + 20)
        d.rounded_rectangle((cx, 120, cx + cw, 560), 14, fill=PANEL)
        s = sprite(spr, 5)
        im.paste(s, (int(cx + (cw - s.width) / 2), 140), s)
        draw_text(d, (cx + cw / 2, 410), zh, 30, INK, bold=True, anchor="m")
        draw_text(d, (cx + cw / 2, 444), en, 20, SUB, anchor="m")
        # 标题栏按钮
        bw = text_width(d, btn, 18, bold=True) + 24
        d.rounded_rectangle((cx + cw / 2 - bw / 2, 462, cx + cw / 2 + bw / 2, 490), 6, fill=KEY)
        draw_text(d, (cx + cw / 2, 476), btn, 18, KEY_INK, bold=True, anchor="m")
        y = 512
        draw_text(d, (cx + cw / 2, y), lines[0], 19, AMBER, bold=True, anchor="m")   # 槽位（中文）
        draw_text(d, (cx + cw / 2, y + 24), lines[1], 16, DIM, anchor="m")            # 槽位（英文）
    # 第二行：两种芯片 + 每台机器的用途
    d.rounded_rectangle((40, 580, W - 40, H - 30), 14, fill=PANEL)
    chips = [("as_chip_base", "基础芯片", "Base chip", "数值（性能 / 效率 / 品质），单项上限 ±500", "stats (perf / eff / quality), ±500 each"),
             ("as_chip_effect", "特殊词条芯片", "Affix chip", "一个特殊词条（优质、过压……）", "one special affix (Premium, Overvolted, ...)")]
    for i, (spr, zh, en, dz, de) in enumerate(chips):
        x = 70 + i * 600
        s = sprite(spr, 6)
        im.paste(s, (x, 610), s)
        draw_text(d, (x + 120, 628), zh + " · " + en, 24, INK, bold=True)
        draw_text(d, (x + 120, 664), dz, 18, SUB)
        draw_text(d, (x + 120, 692), de, 16, DIM)
    out = ROOT / "mods" / "AdvancedScientist" / "docs" / "images" / "machines.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out, optimize=True)
    return out


if __name__ == "__main__":
    for p in (sort_to_boxes_keys(), counter_overflow_explain(), advanced_scientist_machines()):
        print(p.relative_to(ROOT), Image.open(p).size, p.stat().st_size)
