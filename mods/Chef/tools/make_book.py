"""生成厨师菜谱（游戏的纸质书 JSON：mod 文件夹 Books/*.json，游戏自己读）。

    python3 tools/make_book.py Books/chef_recipes.json

版式照游戏自带的笔记本（鉴定指南用的那种）：底纸 notebook_manual（314×402 设计坐标，显示放大 2 倍），
右上角关闭 / 放大，底部翻页和页码（贴图由 ChefMod.Recipe.EnsurePaperSprites 从游戏里找出来）。
小节标题用游戏的色带 notebook_manual_section（白色横条，用 color 染色），上面压白字。
每段文字 text 是英文，translations 里放中文（游戏按 LocHelper.GetCurrentLocaleCode() 取，中文是 zh 或 zh-Hans，两个都写）。
千卡表是目录式（名字……数值）：点线按名字的估计宽度算，中英文各一个元素（另一种语言里是零宽空格，什么也不显示）。
数字要和代码一致：Food.cs（Quality）、Oil.cs、Machines.cs、Items.cs。
"""
import json
import math
import os
import sys

CHROME = [
    {"type": "button", "id": "close", "action": "close", "x": 297, "y": 2.5, "w": 14, "h": 15},
    {"type": "button", "id": "zoom", "action": "zoom", "x": 273.5, "y": 3.5, "w": 14, "h": 15},
    {"type": "button", "id": "flip_left", "action": "flip_left", "x": 124, "y": 380, "w": 16, "h": 16},
    {"type": "button", "id": "flip_right", "action": "flip_right", "x": 176, "y": 380.5, "w": 14, "h": 15},
    {"type": "text", "id": "page_indicator", "action": "page_indicator", "style": "page_number", "align": "center",
     "valign": "middle", "wrap": False, "x": 140.5, "y": 374, "w": 33, "h": 25},
]

X, W = 35, 253           # 正文区域（和游戏的鉴定指南一样）
BOTTOM = 368
BODY = 7.5               # body 字号（设计坐标）
LINE = 10.2              # 估计行高
ZWSP = "​"

INK = "#2b2733"
SECTION_TEXT = "#f4f1e6"
TEAL, AMBER, WINE, OLIVE, SLATE = "#3f6f73", "#b5793a", "#8a3b4a", "#6f7a3a", "#4d5a73"
TIER_COLORS = ["#9a3a3a", "#55525e", "#4f7a3a", "#c0602a", "#b8860b"]

_ids = [0]


def nid(prefix):
    _ids[0] += 1
    return f"{prefix}{_ids[0]}"


def tr(en, zh):
    return {"text": en, "translations": {"zh": zh, "zh-Hans": zh}}


# ---- 宽度估计（只用来摆点线；中文按 1 个字号宽，英文按 Noto Serif 的大致字宽）
def char_w(c, size):
    o = ord(c)
    if o >= 0x2E80 or c in "“”‘’（）：，。、":
        return size
    if c in "iljtfr.,:;'!|()[] ":
        return size * 0.30
    if c in "mwMW":
        return size * 0.82
    if c.isupper():
        return size * 0.68
    if c.isdigit():
        return size * 0.56
    return size * 0.52


def text_w(s, size=BODY):
    return sum(char_w(c, size) for c in s)


def lines(s, width, size=BODY):
    n = 0
    for para in s.split("\n"):
        n += max(1, math.ceil(text_w(para, size) / (width * 0.97)))
    return n


def block_h(en, zh, width=W, size=BODY):
    return max(lines(en, width, size), lines(zh, width, size)) * LINE + 2


# ---- 元素
def title(en, zh, y=24):
    return [dict(type="text", id=nid("title"), style="serif_title", align="center", wrap=False, x=20, y=y, w=274, h=24, color=INK, **tr(en, zh))]


def image(sprite, x, y, w, h):
    return dict(type="image", id=nid("img"), sprite=sprite, fit="fit", x=x, y=y, w=w, h=h)


def section(y, en, zh, color):
    """色带 + 白字小节标题，占 16 高。"""
    return [
        dict(type="image", id=nid("bar"), sprite="notebook_manual_section", fit="stretch", color=color, x=X - 4, y=y, w=W + 8, h=14),
        dict(type="text", id=nid("sec"), style="label", align="left", valign="middle", wrap=False, x=X + 2, y=y, w=W - 4, h=14,
             color=SECTION_TEXT, **tr(en, zh)),
    ]


def para(y, en, zh, color=INK, x=X, w=W, style="body", align="left"):
    h = block_h(en, zh, w)
    return [dict(type="text", id=nid("t"), style=style, align=align, x=x, y=y, w=w, h=h, color=color, **tr(en, zh))], h


def leader_row(y, name_en, name_zh, value_en, value_zh=None, color=INK):
    """目录式一行：名字……数值。"""
    value_zh = value_zh if value_zh is not None else value_en
    x0, xr = X + 6, X + W - 4
    els = [
        dict(type="text", id=nid("n"), style="body", wrap=False, x=x0, y=y, w=150, h=11, color=color, **tr(name_en, name_zh)),
        dict(type="text", id=nid("v"), style="body", align="right", wrap=False, x=xr - 60, y=y, w=60, h=11, color=color, **tr(value_en, value_zh)),
    ]
    dot = text_w(".")
    for lang, name, value in (("en", name_en, value_en), ("zh", name_zh, value_zh)):
        start = x0 + text_w(name) + 3
        end = xr - text_w(value) - 3
        n = max(2, int((end - start) / dot))
        dots = "." * n
        el = dict(type="text", id=nid("d"), style="body", wrap=False, x=start, y=y, w=max(10, end - start + 4), h=11, color="#8b8778")
        if lang == "en":
            el.update(text=dots, translations={"zh": ZWSP, "zh-Hans": ZWSP})
            # 英文点线的起点按英文名字算；中文时这个元素是零宽空格
        else:
            el.update(text=ZWSP, translations={"zh": dots, "zh-Hans": dots})
        els.append(el)
    return els


class Page:
    def __init__(self, name):
        self.name = name
        self.els = []
        self.y = 0

    def add(self, els):
        self.els.extend(els)

    def sec(self, en, zh, color, gap=6):
        self.y += gap
        self.add(section(self.y, en, zh, color))
        self.y += 18

    def p(self, en, zh, color=INK, gap=2):
        self.y += gap
        els, h = para(self.y, en, zh, color)
        self.add(els)
        self.y += h

    def rows(self, items, color=INK):
        for it in items:
            self.add(leader_row(self.y, *it, color=color))
            self.y += 11

    def done(self):
        if self.y > BOTTOM:
            print(f"!! page '{self.name}' too long: y={self.y:.0f} > {BOTTOM}", file=sys.stderr)
        return {"name": self.name, "elements": self.els}


pages = []

# ---------------------------------------------------------------- 1 四道菜
p = Page("Dishes")
p.add(title("Chef's Recipes", "厨师菜谱"))
p.add([image("chef_fries", 52, 54, 22, 44), image("chef_fried_chicken", 96, 60, 38, 38),
       image("chef_burger", 154, 60, 38, 38), image("chef_combo", 212, 50, 50, 50)])
cap_y = 102
for x, en, zh in ((38, "Fries\n200", "薯条\n200"), (90, "Fried chicken\n200", "炸鸡\n200"), (148, "Burger\n300", "肉汉堡\n300"), (208, "Super combo\n900", "超级大套餐\n900")):
    p.add([dict(type="text", id=nid("cap"), style="caption", align="center", x=x, y=cap_y, w=60, h=22, color=INK, **tr(en, zh))])
p.y = 126
p.sec("How each dish is made", "做法", TEAL)
p.p("Fries: deep fryer, overnight. 1 nutrifruit.\nFried chicken: deep fryer, overnight. 1 piece of meat.\nMeat burger: food processor, \"Burger\". Meat + nutrifruit (bun) + fillerweed.\nSuper combo: food processor, \"Combo\". Burger + fries + fried chicken.",
    "薯条：油锅，过一夜。营养果 ×1。\n炸鸡：油锅，过一夜。肉 ×1。\n肉汉堡：食品加工器，点“做汉堡”。肉 + 营养果（面包胚）+ 填料草。\n超级大套餐：食品加工器，点“配套餐”。汉堡 + 薯条 + 炸鸡。")
p.sec("Quality", "品质", AMBER)
p.p("Every dish scores 0-100 points in five grades, up to Michelin at +200%. Points come from the meat's calories, oil purity, the burger's ingredient ratio and the machine's quality modules. See the last page.",
    "每份菜 0–100 分，分五档，最高米其林，价格 +200%。分数看肉的千卡、油的纯净度、汉堡的食材比例和机器上的品质模组。详见最后一页。")
p.p("Hover over a dish to see its grade and points.", "鼠标指着菜能看到档次和得分。", color=SLATE)
pages.append(p.done())

# ---------------------------------------------------------------- 2 油锅
p = Page("Deep fryer")
p.add(title("Deep Fryer", "油锅"))
p.add([image("chef_fryer", 127, 52, 60, 40)])
p.y = 98
p.sec("Setting up", "准备", TEAL)
p.p("Put a container of cooking oil in the \"Oil\" slot: the cooking oil jug, or any of the game's jugs and bottles you poured oil into. It needs at least 1 L, and no water in it. Put a battery in the power slot.",
    "“油桶”槽放一桶食用油：食用油桶，或者倒了油的游戏里任何桶、瓶子都行。至少 1 L，油里不能有水。电源槽放电池。")
p.sec("Frying", "炸", AMBER)
p.p("Put nutrifruit and meat in the basket and sleep. Each night everything in the basket is fried into the output: nutrifruit → fries, meat → fried chicken. Each portion soaks up 100 ml of oil; one night uses 20 charge.",
    "炸篮放营养果和肉，睡一觉。每晚把炸篮里的东西炸好放进出锅格：营养果 → 薯条，肉 → 炸鸡。每份吸掉 100 ml 油，每晚耗 20 电。")
p.sec("Modules", "模组", SLATE)
p.p("The fryer has the game's standard module grid. Quality modules raise the score: +100% quality gives the full 20 points.",
    "油锅有游戏标准的模组格。品质模组加分：品质加成 +100% 拿满 20 分。")
p.p("Hover over the fryer to see the oil in it and how many portions it can still fry.", "鼠标指着油锅能看到里面的油，还能炸几份。", color=SLATE)
pages.append(p.done())

# ---------------------------------------------------------------- 3 食品加工器
p = Page("Food processor")
p.add(title("Food Processor", "食品加工器"))
p.add([image("chef_processor", 127, 52, 60, 40)])
p.y = 98
p.sec("Burger", "做汉堡", TEAL)
p.p("Put meat, nutrifruit and fillerweed in \"Ingredients\" and click \"Burger\" in the title bar: everything in the grid becomes one burger. It needs at least meat and nutrifruit. Each click uses 5 charge.",
    "“食材”格放肉、营养果、填料草，点标题栏的“做汉堡”：格子里的全部食材做成一个汉堡。至少要有肉和营养果。每按一次耗 5 电。")
p.p("The ratio is counted in calories. Ideal: meat 55% : nutrifruit 30% : fillerweed 15%. The message after each burger shows your ratio.",
    "比例按千卡算，理想是 肉 55% : 营养果 30% : 填料草 15%。每做一个汉堡，提示里会写出这次的比例。", color=WINE)
p.p("Example: standard raw meat (1200) + 2 nutrifruit (480) + fillerweed (259) = 62 : 25 : 13.",
    "例：标准生肉（1200）+ 营养果 ×2（480）+ 填料草（259）= 62 : 25 : 13。")
p.sec("Super combo", "配套餐", AMBER)
p.p("Put a burger, fries and fried chicken in \"Plating\" and click \"Combo\". The combo's grade is the lowest of the three.",
    "“配餐”格放汉堡、薯条、炸鸡各一份，点“配套餐”。套餐的档次取三样里最低的。")
pages.append(p.done())

# ---------------------------------------------------------------- 4 食用油
p = Page("Cooking oil")
p.add(title("Cooking Oil", "食用油"))
p.add([image("chef_oil_barrel", 108, 50, 30, 45), image("chef_strainer", 172, 56, 36, 36)])
p.y = 98
p.sec("Oil gets dirty", "油会变脏", WINE)
p.p("Each night of frying leaves large crumbs and fine residue in the oil, and 4% of the cooking oil turns into aged oil. Oil purity = the share of cooking oil in the container.",
    "每炸一晚，油里会多出大颗粒炸渣和小颗粒油渣，还有 4% 的食用油变成老化油。油的纯净度 = 食用油在桶里占的比例。")
p.sec("Filtering", "过滤", TEAL)
p.p("1. Put the oil strainer in a water pitcher and pour the oil in: the large crumbs stay behind.\n2. Swap in a water filter and pour the oil through again: the fine residue stays behind.\nFiltered oil is \"recycled oil\".",
    "1. 滤油网装进滤水壶，把油倒进去：大颗粒炸渣被滤掉。\n2. 换成滤水器，再倒一遍：小颗粒油渣被滤掉。\n滤过的油叫“二次食用油”。")
p.p("Large crumbs also clog a water filter fast, so strain first.", "大颗粒也能用滤水器滤，但很伤滤嘴，先用滤油网。", color=WINE)
p.sec("Good to know", "注意", SLATE)
p.p("Aged oil can't be filtered, so recycled oil never gets back to 100%; pouring in fresh oil brings it back up. No water in the oil, or the fryer won't use it. Spent oil can be poured down the drain.",
    "老化油滤不掉，所以二次食用油回不到 100%；倒新油进去能拉回来。油里不能混水，否则油锅不用。不要的油倒进下水口。")
pages.append(p.done())

# ---------------------------------------------------------------- 5 千卡
p = Page("Calories")
p.add(title("Calories", "食材的千卡"))
p.y = 46
p.sec("Meat", "肉", WINE, gap=4)
p.rows([
    ("Fat meat", "肥肉", "1250"),
    ("Raw meat (standard)", "生肉（标准）", "1200"),
    ("Peat", "“泥炭”合成肉", "800"),
    ("Raw meat (small)", "生肉（小）", "550"),
    ("Meat scrap", "肉边角料", "350"),
    ("Morsel", "一口食物", "275"),
    ("Small morsel", "一小口食物", "140"),
    ("Fish, eel", "鱼肉、鳗鱼肉", "by size", "看大小"),
])
p.sec("Nutrifruit", "营养果", OLIVE)
p.rows([
    ("Huge", "巨大", "768"),
    ("Large", "大", "384"),
    ("Normal", "普通", "240"),
    ("Small", "小", "144"),
])
p.sec("Fillerweed", "填料草", TEAL)
p.rows([
    ("Normal", "普通", "259"),
    ("Small", "小", "155"),
])
p.p("Plant traits change the calories; hover over an ingredient to see them.", "种植特性会改变千卡，鼠标指着食材能看到。", color=SLATE, gap=6)
pages.append(p.done())

# ---------------------------------------------------------------- 6 品质（最后一页）
p = Page("Quality")
p.add(title("Quality", "品质"))
p.y = 46
p.sec("Five grades", "五个档次", AMBER, gap=4)
tiers = [
    ("Michelin", "米其林", "90+", "90 以上", "+200%"),
    ("Chef's", "主厨", "75-89", "75–89", "+100%"),
    ("Tasty", "美味", "55-74", "55–74", "+50%"),
    ("Plain", "普通", "30-54", "30–54", "base", "原价"),
    ("Inedible", "难以下咽", "under 30", "30 以下", "−50%"),
]
for i, t in enumerate(tiers):
    en, zh, s_en, s_zh = t[0], t[1], t[2], t[3]
    price_en = t[4]
    price_zh = t[5] if len(t) > 5 else t[4]
    color = TIER_COLORS[4 - i]
    p.add([
        dict(type="text", id=nid("tier"), style="label", wrap=False, x=X + 6, y=p.y, w=90, h=11, color=color, **tr(en, zh)),
        dict(type="text", id=nid("pts"), style="body", wrap=False, x=X + 100, y=p.y, w=80, h=11, color=INK, **tr(s_en + " pts", s_zh + " 分")),
        dict(type="text", id=nid("price"), style="label", align="right", wrap=False, x=X + W - 70, y=p.y, w=66, h=11, color=color, **tr(price_en, price_zh)),
    ])
    p.y += 12
p.sec("Points", "怎么拿分", TEAL)
p.p("Fried chicken = meat calories 35 + oil purity 45 + modules 20\nFries = nutrifruit calories 25 + oil purity 55 + modules 20\nBurger = meat calories 30 + ratio 50 + modules 20\nSuper combo = the lowest of the three",
    "炸鸡 = 肉的千卡 35 + 油的纯净度 45 + 模组 20\n薯条 = 营养果的千卡 25 + 油的纯净度 55 + 模组 20\n肉汉堡 = 肉的千卡 30 + 比例 50 + 模组 20\n超级大套餐 = 三样里最低的")
p.p("Meat: full at 1200 kcal (a burger counts all its meat). Nutrifruit: full at 384 kcal. Oil purity: full at 98%, none at 70%. Modules: full at +100% quality. Ratio: ideal meat 55 : nutrifruit 30 : fillerweed 15.",
    "肉：1200 千卡满分（汉堡算所有肉加起来）。营养果：384 千卡满分。油的纯净度：98% 满分，70% 以下 0 分。模组：品质加成 +100% 满分。比例：理想 肉 55 : 营养果 30 : 填料草 15。", color=SLATE)
p.p("Fat meat or standard raw meat in fresh oil with good quality modules makes Michelin fried chicken.",
    "肥肉或标准生肉、新油、好的品质模组，炸鸡就是米其林。", color=TIER_COLORS[4])
pages.append(p.done())

book = {
    "id": "chef_recipes",
    "title": "Chef's Recipes",
    "background": "notebook_manual",
    "width": 628,
    "height": 804,
    "chrome": CHROME,
    "pages": pages,
}

out = sys.argv[1] if len(sys.argv) > 1 else "Books/chef_recipes.json"
os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
with open(out, "w", encoding="utf-8") as f:
    json.dump(book, f, ensure_ascii=False, indent=1)
print("book:", out, len(pages), "pages")
