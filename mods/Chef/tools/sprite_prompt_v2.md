You are re-making three item sprites for a mod of the pixel-art game "Probably Stolen".
`reference_sheet.png` shows real game sprites (6x scale). `previous_version.png` shows the previous versions of this mod's sprites (6x scale):
from left: fries, fried chicken, burger, combo, deep fryer, food processor, oil jug, strainer, icon.
Keep the same art style as the reference: chunky pixels, muted palette, 1px dark outline (#2b2733), 2-3 tone shading, no anti-aliasing, no text.

Make these (final PNG, RGBA, transparent background, alpha strictly 0 or 255, at most ~24 colours, exact size):
1. chef_fryer.png      48x32 (3 cells wide, 2 cells tall) - the same stainless-steel deep fryer as in previous_version.png, but redesigned as a WIDE, LOW countertop unit: a wide body with a dial and indicator light on the front, an oil well on top with a wire basket and its handle. It must fill the whole 48x32 rectangle (a solid rectangular silhouette, like a box), touching or nearly touching all four edges.
2. chef_processor.png  48x32 (3x2 cells) - the same industrial food processor as previous_version.png, redesigned WIDE and LOW: a wide metal base with buttons and a clear bowl with a blade on one side, maybe a small chute. Solid rectangular silhouette filling the whole 48x32 canvas.
3. chef_strainer.png   32x32 (2x2 cells) - an oil filter mesh for a water pitcher: a round fine wire-mesh disc/sieve with a metal rim seen from slightly above, filling the whole 32x32 square (the disc touches all four edges). No long handle.
Every 16x16 cell must be mostly filled with opaque pixels (the game derives the item shape from the opaque pixels of each cell; the item must read as a full rectangle).

Process: generate each with your image generation tool on a flat #FF00FF background (item large and centered), keep raw images in `raw/`,
then with Python + Pillow crop to the item, fit to the exact target size (keep it crisp: nearest-neighbour), key out the background, make alpha binary,
quantize colours, enforce the 1px dark outline, and save with the exact file names in THIS folder.
Then write `preview.png` (the three final sprites side by side on #3a3645, 6x nearest). Do not touch files outside this folder.
