"""Builds Gratis/Assets/Fonts/Icons/icons.ttf.

Lucide supplies the outline glyphs used by the interface and quest markers. Phosphor (Fill) supplies the
solid glyphs used on marker plates, remapped to U+F100 and up so the two sets never share a codepoint.
The filled codepoints must match GratisIcons.Filled in the plugin.

    python tools/icon-font/build.py

Needs fontTools (pip install fonttools).
"""
from io import BytesIO
from pathlib import Path

from fontTools.merge import Merger, Options
from fontTools.ttLib import TTFont
from fontTools.ttLib.scaleUpem import scale_upem

HERE = Path(__file__).parent
OUTPUT = HERE.parent.parent / "Gratis" / "Assets" / "Fonts" / "Icons" / "icons.ttf"
FIRST_FILLED = 0xF100

phosphor = TTFont(HERE / "phosphor-fill.ttf")
scale_upem(phosphor, 1000)
source = phosphor.getBestCmap()
glyphs = {}
for index, codepoint in enumerate(sorted(source)):
    glyphs[FIRST_FILLED + index] = source[codepoint]
    print(f"U+{codepoint:04X} -> U+{FIRST_FILLED + index:04X} {source[codepoint]}")
for table in phosphor["cmap"].tables:
    table.cmap = dict(glyphs)

remapped = BytesIO()
phosphor.save(remapped)
remapped.seek(0)
# Neither set uses ligatures or kerning, and their layout tables do not merge.
layout = Options(drop_tables=["GSUB", "GPOS", "GDEF"])
merged = Merger(layout).merge([str(HERE / "lucide.ttf"), remapped])
merged["name"].setName("Gratis Icons", 1, 3, 1, 0x409)
merged["name"].setName("Gratis Icons", 4, 3, 1, 0x409)
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
merged.save(OUTPUT)
print(f"wrote {OUTPUT} ({OUTPUT.stat().st_size} bytes)")
