"""生成厨师菜谱（游戏的纸质书 JSON：mod 文件夹 Books/*.json，游戏自己读）。

    python3 tools/make_book.py Books/chef_recipes.json

版式照游戏自带的“鉴定指南”笔记本（PaperUI/Starters/guide.json）：底纸 notebook_manual，628×804（设计坐标 314×402，放大 2 倍），
右上角关闭 / 放大，底部翻页和页码。正文用 flow 自动排版。
每段文字的 text 是英文，translations 里放中文（游戏按 LocHelper.GetCurrentLocaleCode() 取，中文是 zh 或 zh-Hans，两个都写）。
图片用本 mod 的物品贴图（Textures/Items/*.png，按文件名引用）。
数字要和代码一致：Food.cs（Quality）、Oil.cs、Machines.cs。
"""
import json
import sys

CHROME = [
    {"type": "button", "id": "close", "action": "close", "x": 297, "y": 2.5, "w": 14, "h": 15},
    {"type": "button", "id": "zoom", "action": "zoom", "x": 273.5, "y": 3.5, "w": 14, "h": 15},
    {"type": "button", "id": "flip_left", "action": "flip_left", "x": 124, "y": 380, "w": 16, "h": 16},
    {"type": "button", "id": "flip_right", "action": "flip_right", "x": 176, "y": 380.5, "w": 14, "h": 15},
    {"type": "text", "id": "page_indicator", "action": "page_indicator", "style": "page_number", "align": "center",
     "valign": "middle", "wrap": False, "x": 140.5, "y": 374, "w": 33, "h": 25},
]

X, W = 35, 253          # 正文区域（和游戏的鉴定指南一样）
_ids = [0]


def nid(prefix):
    _ids[0] += 1
    return f"{prefix}{_ids[0]}"


def tr(en, zh):
    return {"text": en, "translations": {"zh": zh, "zh-Hans": zh}}


def title(en, zh, y=26):
    return dict(type="text", id=nid("title"), style="serif_title", align="center", wrap=False, x=20, y=y, w=274, h=24, **tr(en, zh))


def image(sprite, x, y, w, h):
    return dict(type="image", id=nid("img"), sprite=sprite, fit="fit", x=x, y=y, w=w, h=h)


def flow(y, h, blocks):
    """blocks: [(style, en, zh)]，按顺序排下去。"""
    fid = nid("flow")
    out = [dict(type="flow", id=fid, x=X, y=y, w=W, h=h, spacing=4)]
    for style, en, zh in blocks:
        out.append(dict(type="text", id=nid("t"), flow=fid, style=style, autoHeight=True, w=0, h=0, **tr(en, zh)))
    return out


def page(name, elements):
    return {"name": name, "elements": elements}


S, B = "section", "body"

pages = [
    page("Dishes", [
        title("Chef's Recipes", "厨师菜谱"),
        image("chef_fries", 52, 58, 24, 48),
        image("chef_fried_chicken", 96, 66, 40, 40),
        image("chef_burger", 156, 66, 40, 40),
        image("chef_combo", 214, 54, 52, 52),
        *flow(114, 256, [
            (S, "Four dishes", "四道菜"),
            (B, "Fries: 200. Deep fryer, 1 nutrifruit.\nFried chicken: 200. Deep fryer, 1 piece of meat.\nMeat burger: 300. Food processor: meat + nutrifruit + fillerweed.\nSuper combo: 900. Food processor: a burger + fries + fried chicken.",
                "薯条：200。油锅，营养果 ×1。\n炸鸡：200。油锅，肉 ×1。\n肉汉堡：300。食品加工器：肉 + 营养果 + 填料草。\n超级大套餐：900。食品加工器：汉堡 + 薯条 + 炸鸡。"),
            (S, "Quality", "品质"),
            (B, "Every dish scores 0-100 points:\nInedible (under 30): price −50%\nPlain (30-54): base price\nTasty (55-74): +50%\nChef's (75-89): +100%\nMichelin (90+): +200%",
                "每份菜 0–100 分：\n难以下咽（30 分以下）：价格 −50%\n普通（30–54）：原价\n美味（55–74）：+50%\n主厨（75–89）：+100%\n米其林（90 以上）：+200%"),
            (B, "Hover over a dish to see its score and where the points came from. Points come from four things: the meat's calories, oil purity, the ingredient ratio and the machine's quality modules.",
                "鼠标指着菜能看到得分和每一项拿了多少分。分数看四样：肉的千卡、油的纯净度、食材比例、机器上的品质模组。"),
        ]),
    ]),
    page("Deep fryer", [
        title("Deep Fryer", "油锅：薯条和炸鸡"),
        image("chef_fryer", 133, 52, 48, 48),
        *flow(106, 264, [
            (S, "How to use", "用法"),
            (B, "Drop a cooking oil jug on the fryer to pour oil in (up to 5 L; it needs at least 1 L to fry). Put a battery in the power slot and nutrifruit or meat in the basket, then sleep: the fried food is in the output. Each portion soaks up 100 ml of oil; one night uses 20 charge.",
                "把食用油桶拖到油锅上倒油（最多 5 L，至少 1 L 才炸）。电源槽放电池，炸篮放营养果或肉，睡一觉，出锅格里就是炸好的。每份吸掉 100 ml 油，每晚耗 20 电。"),
            (S, "Score", "得分"),
            (B, "Fried chicken = meat calories 35 (full at 1200 kcal) + oil purity 45 + modules 20\nFries = nutrifruit calories 25 (full at 384 kcal) + oil purity 55 + modules 20",
                "炸鸡 = 肉的千卡 35（1200 千卡满分）+ 油的纯净度 45 + 模组 20\n薯条 = 营养果的千卡 25（384 千卡满分）+ 油的纯净度 55 + 模组 20"),
            (B, "Oil purity: full points at 98% or more, none at 70% or less. Modules: put quality modules in the fryer's module grid; +100% quality gives the full 20 points.",
                "油的纯净度：98% 以上满分，70% 以下 0 分。模组：油锅的模组格放品质模组，品质加成 +100% 拿满 20 分。"),
        ]),
    ]),
    page("Food processor", [
        title("Food Processor", "食品加工器"),
        image("chef_processor", 133, 52, 48, 48),
        *flow(106, 264, [
            (S, "Burger", "做汉堡"),
            (B, "Put meat, nutrifruit and fillerweed in \"Ingredients\" and click \"Burger\" in the title bar: everything in the grid becomes one burger (it needs at least meat and nutrifruit). Each click uses 5 charge.",
                "“食材”格放肉、营养果、填料草，点标题栏的“做汉堡”：格子里的全部食材做成一个汉堡（至少要有肉和营养果）。每按一次耗 5 电。"),
            (B, "Score = meat calories 30 (all meat in the burger, full at 1200 kcal) + ratio 50 + modules 20.\nThe ratio is by calories; ideal is meat 55% : nutrifruit 30% : fillerweed 15%. Example: standard raw meat 1200 + 2 nutrifruit 480 + fillerweed 259 = 62 : 25 : 13, about 36 ratio points.",
                "得分 = 肉的千卡 30（汉堡里所有肉加起来，1200 千卡满分）+ 比例 50 + 模组 20。\n比例按千卡算，理想是 肉 55% : 营养果 30% : 填料草 15%。例：标准生肉 1200 + 营养果 ×2（480）+ 填料草 259 = 62 : 25 : 13，比例分约 36。"),
            (S, "Super combo", "配套餐"),
            (B, "Put a burger, fries and fried chicken in \"Plating\" and click \"Combo\". The combo's quality is that of the lowest of the three.",
                "“配餐”格放汉堡、薯条、炸鸡各一份，点“配套餐”。套餐的品质取三样里最低的。"),
        ]),
    ]),
    page("Cooking oil", [
        title("Cooking Oil", "食用油和过滤"),
        image("chef_oil_barrel", 113, 50, 32, 48),
        image("chef_strainer", 169, 58, 40, 40),
        *flow(106, 264, [
            (B, "Every night of frying dirties the oil: some is soaked up, large crumbs and fine residue are left behind, and 4% of the fresh oil turns into aged oil. Purity = the share of fresh oil.",
                "每炸一晚油都会变脏：一部分被吸掉，留下大颗粒炸渣和小颗粒油渣，还有 4% 的新油变成老化油。纯净度 = 新油占的比例。"),
            (S, "Filtering", "过滤"),
            (B, "Large crumbs: drop the oil strainer on the fryer (or a jug).\nFine residue: drop the fryer (or a jug) on a water pitcher with a water filter in it. Large crumbs can be filtered this way too, but they wear the filter out fast, so strain first.",
                "大颗粒炸渣：把滤油网拖到油锅（或油桶）上。\n小颗粒油渣：把油锅（或油桶）拖到装着滤水器的滤水壶上。大颗粒也能这样滤，但很伤滤嘴，先用滤油网。"),
            (B, "Aged oil can't be filtered: cleaned oil is \"recycled oil\" and never gets back to 100%. Pouring in fresh oil brings the purity back up. Under 70% it is spent oil; \"Dump oil\" in the fryer's title bar (click twice) empties it.",
                "老化油滤不掉：滤干净的油叫“二次食用油”，纯净度回不到 100%。往里倒新油能把纯净度拉回来。低于 70% 是废油，点油锅标题栏的“倒掉油”（点两次）清空。"),
            (B, "Hover over the fryer or a jug to see the oil's state.", "鼠标指着油锅、油桶能看到油的状态。"),
        ]),
    ]),
    page("Calories", [
        title("Calories", "食材的千卡"),
        *flow(60, 310, [
            (S, "Meat", "肉"),
            (B, "Fat meat 1250 · Raw meat (standard) 1200 · Peat 800 · Raw meat (small) 550 · Meat scrap 350 · Morsel 275 · Small morsel 140. Fish and eel meat depend on their size.",
                "肥肉 1250 · 生肉（标准）1200 · “泥炭”合成肉 800 · 生肉（小）550 · 肉边角料 350 · 一口食物 275 · 一小口食物 140。鱼肉、鳗鱼肉看大小。"),
            (S, "Nutrifruit", "营养果"),
            (B, "Small 144 · Normal 240 · Large 384 · Huge 768. Plant traits change it; hover over a fruit to see.",
                "小 144 · 普通 240 · 大 384 · 巨大 768。种植特性会改变，鼠标指着能看到。"),
            (S, "Fillerweed", "填料草"),
            (B, "Normal 259 · Small 155.", "普通 259 · 小 155。"),
            (S, "Tips", "小窍门"),
            (B, "Fried chicken from fat meat or standard raw meat in fresh oil, with good quality modules, is Michelin grade. For burgers, balance the ratio with large or small nutrifruit.",
                "肥肉或标准生肉、新油、好的品质模组，炸鸡就是米其林。做汉堡用大、小营养果来调比例。"),
        ]),
    ]),
]

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
import os
os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
with open(out, "w", encoding="utf-8") as f:
    json.dump(book, f, ensure_ascii=False, indent=1)
print("book:", out, len(pages), "pages")
