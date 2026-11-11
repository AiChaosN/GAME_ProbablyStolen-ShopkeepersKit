You are making item sprites for a mod of the pixel-art game "Probably Stolen".
The attached image `reference_sheet.png` (also in this folder) shows real sprites from the game, scaled up 6x:
instant ramen, fat meat, fillerweed (a leafy green), hot sauce bottle, cotton fabric, nutrifruit (PURPLE potato-like fruit),
"PEAT" canned meat, raw meat, small raw meat, a metal wine rack machine, a plastic water jug.
Match that style exactly: chunky pixels, muted/desaturated palette, a 1-pixel dark outline (around #2b2733), soft 2-3 tone shading,
top-left light, no anti-aliasing, no text, no background, no drop shadow beyond 1px.
The game grid is 16x16 pixels per cell; final sprites must be exactly the sizes below.

Make these sprites (final PNG, RGBA, transparent background, exact pixel size):
1. chef_fries.png        16x32  - french fries standing in a red paper carton. The fries are pale golden with a slight lavender tint (they are made from purple nutrifruit).
2. chef_fried_chicken.png 32x32 - two or three crispy golden-brown fried chicken drumsticks/pieces, crunchy coating.
3. chef_burger.png       32x32  - a burger: bun with a faint purple tint (nutrifruit bread) and sesame dots, a thick brown meat patty, green leafy fillerweed lettuce. NO cheese.
4. chef_combo.png        48x48  - a "super combo meal": a tray holding a burger, a fries carton and fried chicken, plus a paper cup drink.
5. chef_fryer.png        48x48  - an industrial stainless-steel deep fryer machine with a wire frying basket and amber oil visible, a small dial and indicator light. Similar framing/weight to the wine rack machine in the reference.
6. chef_processor.png    48x48  - a countertop food processor machine: metal base with buttons, clear bowl with a blade, chunky and industrial.
7. chef_oil_barrel.png   32x48  - a plastic jerrycan of cooking oil (like the water jug in the reference) but the plastic is translucent amber/golden showing oil inside, with a cap and handle.
8. chef_strainer.png     32x32  - a round wire-mesh oil strainer/skimmer with a handle (kitchen spider/skimmer).
9. chef_icon.png         48x48  - profession icon for "Chef": a white chef's toque hat over a frying pan or crossed spatula, bold and readable.

Process:
- Generate each image with your image generation tool (one image per item, large, plain flat background colour that is easy to key out, e.g. pure magenta #FF00FF, item centered and filling most of the frame).
- Then with Python + Pillow (python3 is available) convert each to the exact target size: key out the background to full transparency,
  downscale with a method that keeps it crisp (e.g. resize to target with Image.NEAREST after cropping to the item's bounding box and fitting it into the target with 0-1px margin, keeping aspect ratio), make alpha strictly 0 or 255, quantize to at most ~24 colours, and make sure there is a 1px dark outline.
  The item should fill its canvas well (touch or nearly touch the edges) because the game derives the item's grid shape from the opaque pixels of each 16x16 cell; every 16x16 cell of the canvas should contain some opaque pixels.
- Save the final sprites in THIS folder with the exact file names above. Also keep the raw generated images in a subfolder `raw/`.
- Finally write `preview.png`: all final sprites side by side on a dark background (#3a3645), scaled 6x with nearest neighbour.
Do not modify reference_sheet.png. Do not touch files outside this folder.
