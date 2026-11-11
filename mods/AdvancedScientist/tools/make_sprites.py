"""生成高级科学家 mod 用的像素图（Textures/Items/*.png）。游戏按 16 像素一格从图片的不透明像素推出物品形状。
用法：python3 -I tools/make_sprites.py   （在 mod 目录下运行，需要 Pillow）"""
import pathlib
from PIL import Image, ImageDraw

OUT = pathlib.Path(__file__).resolve().parent.parent / "Textures" / "Items"
OUT.mkdir(parents=True, exist_ok=True)

STEEL_D, STEEL, STEEL_L = (46, 52, 64, 255), (78, 88, 104, 255), (122, 134, 150, 255)
OUTLINE = (20, 22, 28, 255)
WHITE, GREY = (232, 236, 240, 255), (170, 178, 188, 255)
GREEN, AMBER, CYAN = (96, 220, 120, 255), (240, 180, 60, 255), (90, 200, 230, 255)


def machine_blank_maker():
    """空白模组制造机：3×3 格（48×48）。机身 + 进料口 + 正面窗口里一块白色模组 + 指示灯。"""
    im = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([1, 4, 46, 46], fill=STEEL, outline=OUTLINE)            # 机身
    d.rectangle([2, 5, 45, 8], fill=STEEL_L)                            # 顶部高光
    d.rectangle([6, 1, 22, 5], fill=STEEL_D, outline=OUTLINE)           # 进料口
    d.rectangle([9, 2, 19, 3], fill=AMBER)
    d.rectangle([7, 14, 40, 40], fill=STEEL_D, outline=OUTLINE)         # 窗口
    d.rectangle([15, 19, 32, 35], fill=WHITE, outline=GREY)             # 白色模组
    for x in (18, 23, 28):
        d.rectangle([x, 22, x + 1, 32], fill=GREY)                      # 模组触点
    d.rectangle([38, 8, 41, 11], fill=GREEN, outline=OUTLINE)           # 指示灯
    d.line([4, 43, 43, 43], fill=STEEL_D)
    im.save(OUT / "as_blank_module_maker.png")


def scenario_icon():
    """职业图标：烧瓶 + 一块模组芯片，3×3 格。"""
    im = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([18, 4, 28, 16], fill=(200, 230, 240, 255), outline=OUTLINE)   # 瓶颈
    d.polygon([(18, 16), (28, 16), (40, 40), (6, 40)], fill=(200, 230, 240, 255), outline=OUTLINE)
    d.polygon([(12, 30), (34, 30), (40, 40), (6, 40)], fill=CYAN)              # 液体
    d.rectangle([26, 26, 44, 44], fill=WHITE, outline=OUTLINE)                 # 模组
    for x in (30, 34, 38):
        d.rectangle([x, 29, x + 1, 41], fill=GREY)
    d.rectangle([40, 27, 42, 29], fill=GREEN)
    im.save(OUT / "as_icon.png")


if __name__ == "__main__":
    machine_blank_maker()
    scenario_icon()
    for p in sorted(OUT.glob("*.png")):
        print(p.name, Image.open(p).size)
