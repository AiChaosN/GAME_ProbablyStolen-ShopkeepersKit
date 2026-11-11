You are redrawing five item sprites for a mod of the pixel-art game "Probably Stolen" so they match the game's own art.
Reference images in this folder (all scaled 6x):
- `reference_modules.png`: real game sprites — machine modules (green/red PCB cards with a coloured side cap), a module extractor (robotic arm on a base), a small augment chip (16x16), electronic parts, a chemistry module, small PCB modules.
- `reference_food_machines.png`: more real game sprites (food, a metal wine-rack machine, a plastic jug) for the general style.
- `current_versions.png`: the mod's CURRENT flat placeholder sprites, from left: blank module maker, affix extractor, affix inlayer, base chip, affix chip, (icon — ignore). Keep their meaning, replace their look.
Style: exactly like the game sprites — chunky pixels, muted palette, 1px dark outline (#2b2733), 2-3 tone shading, small details (screws, vents, LEDs, cables), no text, no anti-aliasing.

Make (final PNG, RGBA, transparent background, alpha strictly 0 or 255, at most ~24 colours, EXACT size):
1. as_blank_module_maker.png  48x48 - a small industrial fabricator that presses blank modules: metal housing with a press/stamp on top, a slot at the front with a half-finished bare green PCB card coming out, a little control panel with LEDs. Solid silhouette that covers the whole 48x48 square.
2. as_affix_extractor.png     48x48 - a machine that pulls the "affixes" out of a module as chips: a metal base with a clamp holding a module card (like the game's modules), a small robotic probe/arm above it, and two tiny chips (one green, one purple) dropping into a tray. Covers the whole 48x48 square.
3. as_affix_inlayer.png       48x48 - a machine that inlays chips into a blank module: a metal workbench base with a module card on a bed, a precision arm/soldering head placing a chip onto it, a magnifier lamp. Different silhouette/colour accent from the extractor. Covers the whole 48x48 square.
4. as_chip_base.png           16x16 - a "base chip": a tiny square microchip on a GREEN PCB, gold pins, in the style of the game's 16x16 augment chip. Fill most of the 16x16 cell.
5. as_chip_effect.png         16x16 - an "affix chip": same kind of tiny chip but on a PURPLE PCB with a small glowing yellow core. Fill most of the 16x16 cell.
For the three 48x48 machines: EVERY 16x16 cell of the 3x3 grid must be well filled with opaque pixels — the item must read as a full 3x3 square (it is placed on a grid; empty cells would change its shape).

Process: generate each with your image generation tool on a flat #FF00FF background (item large and centred), keep raw images in `raw/`,
then with Python + Pillow crop to the item, fit to the exact target size (crisp, nearest-neighbour), key out the background, make alpha binary,
quantize colours, enforce the 1px dark outline, and save with the exact file names in THIS folder.
Then write `preview.png` (the five final sprites side by side on #3a3645, 6x nearest). Do not touch files outside this folder.
