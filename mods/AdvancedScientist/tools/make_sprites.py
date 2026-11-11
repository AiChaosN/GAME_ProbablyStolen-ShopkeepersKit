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
CHIP_G, CHIP_P = (60, 150, 90, 255), (150, 90, 200, 255)


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


def machine_extractor():
    """词条提取机：3×3。机身 + 顶部模组插口 + 正面两块小芯片和一个向外的箭头。"""
    im = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([1, 4, 46, 46], fill=STEEL, outline=OUTLINE)
    d.rectangle([2, 5, 45, 8], fill=STEEL_L)
    d.rectangle([14, 1, 33, 6], fill=STEEL_D, outline=OUTLINE)          # 模组插口
    d.rectangle([17, 2, 30, 4], fill=WHITE)
    d.rectangle([5, 13, 42, 41], fill=STEEL_D, outline=OUTLINE)         # 窗口
    d.rectangle([9, 18, 18, 27], fill=CHIP_G, outline=OUTLINE)          # 基础芯片
    d.rectangle([9, 30, 18, 39], fill=CHIP_P, outline=OUTLINE)          # 特殊词条芯片
    d.polygon([(23, 24), (33, 24), (33, 20), (40, 28), (33, 36), (33, 32), (23, 32)], fill=AMBER, outline=OUTLINE)   # 箭头
    d.rectangle([40, 8, 43, 11], fill=GREEN, outline=OUTLINE)
    im.save(OUT / "as_affix_extractor.png")


def machine_inlayer():
    """词条镶嵌机：3×3。机身 + 正面一块模组，芯片往里压。"""
    im = Image.new("RGBA", (48, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([1, 4, 46, 46], fill=STEEL, outline=OUTLINE)
    d.rectangle([2, 5, 45, 8], fill=STEEL_L)
    d.rectangle([5, 13, 42, 41], fill=STEEL_D, outline=OUTLINE)
    d.rectangle([20, 22, 37, 38], fill=WHITE, outline=GREY)             # 模组
    for x in (23, 28, 33):
        d.rectangle([x, 25, x + 1, 35], fill=GREY)
    d.rectangle([9, 15, 16, 22], fill=CHIP_G, outline=OUTLINE)          # 芯片
    d.rectangle([9, 26, 16, 33], fill=CHIP_P, outline=OUTLINE)
    d.line([17, 19, 20, 25], fill=AMBER, width=2)
    d.line([17, 30, 20, 30], fill=AMBER, width=2)
    d.rectangle([40, 8, 43, 11], fill=GREEN, outline=OUTLINE)
    im.save(OUT / "as_affix_inlayer.png")


def chip(name, body, mark):
    """芯片：1×1 格（16×16）。"""
    im = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for i in (4, 7, 10):                                                 # 引脚
        d.line([i, 0, i, 2], fill=GREY)
        d.line([i, 13, i, 15], fill=GREY)
    d.rectangle([1, 2, 14, 13], fill=body, outline=OUTLINE)
    d.rectangle([5, 5, 10, 10], fill=mark)
    im.save(OUT / (name + ".png"))


if __name__ == "__main__":
    machine_blank_maker()
    scenario_icon()
    machine_extractor()
    machine_inlayer()
    chip("as_chip_base", CHIP_G, (200, 240, 210, 255))
    chip("as_chip_effect", CHIP_P, (250, 220, 120, 255))
    for p in sorted(OUT.glob("*.png")):
        print(p.name, Image.open(p).size)
