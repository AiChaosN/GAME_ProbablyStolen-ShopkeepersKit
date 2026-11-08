"""
归类入箱的“大类”：每件货只属于一个类，用尽量少的类覆盖尽量多的货。
规则（先命中先算）同时用到物品所在的游戏目录（FoodItemDirectory 等，来自数值手册的 data/items.json）和物品类型。
游戏运行时遇到表里没有的物品（新版本、其他 mod），mod 只按类型套用同一套规则（见 src/Categories.cs 的 FromTypes）。

用法：python3 -I tools/categories.py <数值手册根目录> [src/Generated/ItemCategories.cs]
不给输出路径时只打印统计。
"""
import json, sys, collections, pathlib

CATS = [  # key, 中文名, English
    ("food", "食物", "Food"),
    ("drink", "饮料与水", "Drinks & water"),
    ("household", "日用品", "Household"),
    ("medical", "医疗", "Medical"),
    ("chemical", "化学", "Chemistry"),
    ("weapon", "武器与弹药", "Weapons & ammo"),
    ("material", "材料", "Materials"),
    ("tool", "工具与设备", "Tools & equipment"),
    ("module", "模组", "Modules"),
    ("farming", "种子与农业", "Seeds & farming"),
    ("animal", "养殖", "Husbandry"),
    ("paper", "文件证件", "Documents & cards"),
    ("luxury", "奢侈品与收藏", "Luxury & collectibles"),
    ("other", "其他", "Other"),
]

DRINK_T = {"BREVAGE", "WATER", "WATER_RATION", "ALCOHOL"}
FOOD_T = {"FOOD", "PROCESSED_FOOD", "TREAT", "FRESH_FOOD"}
WEAPON_T = {"WEAPON", "FIREARM", "MELEE_WEAPON", "AMMUNITION", "MUNITION", "GUN_MOD", "ARMOR"}
MED_T = {"MEDICAL", "MEDICAL_TOOL", "ORGAN"}
CHEM_T = {"CHEMICAL_SUPPLIES", "NARCOTIC", "POISON", "SUBSTANCE"}
MODULE_T = {"MODULE", "HYDROPONIC_MODULE", "PRINTER_MODULE", "EVAPORATOR_MODULE", "ALARM_MODULE", "NODE"}
FARM_T = {"SEED", "MODIFIED_SEED", "FARMING_SUPPLIES"}
PAPER_T = {"DOCUMENT", "ACCESS_CARD", "LOTTERY"}
LUX_T = {"ART", "LUXURY_ITEM"}
HOUSE_T = {"HOUSEHOLD_GOOD", "WATER_PURIFICATION_SUPPLY", "FILTER"}
MAT_T = {"MATERIAL", "BASIC_MATERIAL", "REFINED_MATERIAL", "JUNK"}
TOOL_T = {"TOOL", "ENERGY_CELL"}


def first(types, group):
    """types 里最先出现的、属于 group 的类型的位置；没有返回大数。"""
    for i, t in enumerate(types):
        if t in group:
            return i
    return 99


# 逐个指定（规则判断不准的）
OVERRIDE = {
    "water_jug": "drink",
    "meat_scrap": "food", "ribwich": "food",
    "med_bottle_orange": "chemical", "med_bottle_small_pink": "chemical",
    "1ml_dropper": "chemical", "5ml_dropper": "chemical",
    "25ml_cylinder": "chemical", "50ml_cylinder": "chemical", "100ml_cylinder": "chemical",
    "hand": "medical",
    "plasma_fuel": "tool", "black_lamp": "tool", "aug_scanner": "tool",
    "food_stamp": "paper", "rev_codeword": "paper",
    "gamblers_coin": "luxury",
    "packet_red_cigarette": "household",
    "kotton_fiber": "material",
}
# 不归箱的固定设施、存档用的隐藏包、背包等：不列入分类
EXCLUDE = {
    "save_bag", "back_inv_save_bag", "hidden_save_bag", "showcase_save_bag", "sold_save_bag",
    "trashcan", "drain", "faucet", "dossier", "debris_pile1", "debris_pile2", "debris_pile3",
    "box_dispenser", "fanny_pack", "satchel", "kids_backpack", "kids_backpack_large", "backpack_small",
    "backpack_medium", "backpack_medium_military", "backpack_large", "backpack_large_military",
}


def classify(directory, types, name_zh, item_id=None):
    if item_id in OVERRIDE:
        return OVERRIDE[item_id]
    t = list(types)
    s = set(t)
    n = name_zh or ""
    # 喝的：饮料、水、酒（代糖果汁 = 加工食品+饮料+食物 → 饮料）
    if s & DRINK_T or "瓶装水" in n or "泉水" in n:
        return "drink"
    if directory in ("Gun", "GunMod", "Melee", "Explosive", "Armor") or (s & WEAPON_T and first(t, WEAPON_T) <= first(t, TOOL_T)):
        return "weapon"
    if directory == "Meds" or first(t, MED_T) == 0 or "医疗用品" in n:
        return "medical"
    if s & FOOD_T and directory in ("Food", "Husbandry", "Hydroponic", "Wine", "Meds") or first(t, FOOD_T) == 0:
        return "food"
    if s & CHEM_T or "溶液" in n:
        return "chemical"
    if directory == "Module" or s & MODULE_T:
        return "module"
    if directory == "Hydroponic" and first(t, MAT_T) == 0:
        return "material"
    if directory == "Hydroponic" or s & FARM_T:
        return "farming"
    if directory == "Husbandry":
        return "animal"
    if directory in ("Book", "Document", "Key") or s & PAPER_T:
        return "paper"
    if directory == "Cassette" or s & LUX_T and first(t, LUX_T) < first(t, HOUSE_T):
        return "luxury"
    if directory in ("Amenity", "Wine") or s & HOUSE_T:
        return "household"
    if directory == "Material" or s & MAT_T:
        return "material"
    if directory in ("Tool", "Power") or s & TOOL_T:
        return "tool"
    return "other"


def main():
    root = pathlib.Path(sys.argv[1])
    types_all = None
    items = json.load(open(root / "data/items.json", encoding="utf-8"))
    rows = []
    for x in items:
        if x["category"] == "Unused" or x["id"] in EXCLUDE:
            continue
        tags = x.get("tags") or []
        types = [t for t in tags if t.isupper() and not t.endswith("_TAG") and t not in ("IMPORTANT_TAG",)]
        # 设施类（机器、储藏区、背包、容器）不归箱，不分类
        if "MACHINE" in types or "STORAGE" in types or x["category"] in ("Container", "Furniture", "Ship") and not (set(types) & (FOOD_T | DRINK_T | CHEM_T | HOUSE_T | LUX_T | FARM_T)):
            continue
        rows.append((x["id"], classify(x["category"], types, x.get("name_zh"), x["id"]), x.get("name_zh") or x["id"], x["category"], types, x.get("obtainable")))
    by = collections.defaultdict(list)
    for r in rows:
        by[r[1]].append(r)
    names = {k: z for k, z, _ in CATS}
    for k, z, _ in CATS:
        lst = by.get(k, [])
        ob = [r for r in lst if r[5]]
        print(f"{z}（{k}）: {len(ob)} 件可获得 / 共 {len(lst)}  例：" + "、".join(r[2] for r in ob[:10]))
    if len(sys.argv) > 2:
        out = pathlib.Path(sys.argv[2])
        lines = [
            "// <auto-generated>",
            "// 由 tools/categories.py 根据数值手册 data/items.json 生成，不要手改；规则改了重新运行。",
            f"// 共 {len(rows)} 种物品。",
            "// </auto-generated>",
            "using System.Collections.Generic;",
            "",
            "namespace SortToBoxes",
            "{",
            "    internal static partial class Categories",
            "    {",
            "        private static readonly Dictionary<string, string> ById = new Dictionary<string, string>(System.StringComparer.Ordinal)",
            "        {",
        ]
        for r in sorted(rows):
            lines.append(f'            {{ "{r[0]}", "{r[1]}" }},')
        lines += ["        };", "    }", "}", ""]
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text("\n".join(lines), encoding="utf-8")
        print("written", out)


if __name__ == "__main__":
    main()
