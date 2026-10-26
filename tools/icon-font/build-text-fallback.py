"""Subset Dalamud's NotoSansCJK-Medium.ttc for the preview chrome atlas.

Usage: python tools/icon-font/build-text-fallback.py path/to/NotoSansCJK-Medium.ttc
Requires fontTools. The plugin uses the host's copy of this face.
"""
from pathlib import Path
import re
import sys

from fontTools import subset
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[2]
source = TTFont(sys.argv[1], fontNumber=0)
copyright = source["name"].getDebugName(0)
text = (root / "Gratis/Rendering/FontAtlasBudget.cs").read_text(encoding="utf-8")
ranges = re.search(r"FallbackRanges = \[(.*?)\];", text, re.S)[1]
values = [int(value, 16) for value in re.findall(r"0x([0-9a-f]+)", ranges)]
codepoints = {cp for lo, hi in zip(values[::2], values[1::2]) for cp in range(lo, hi + 1)}
options = subset.Options()
options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14]
subsetter = subset.Subsetter(options=options)
subsetter.populate(unicodes=codepoints)
subsetter.subset(source)
for record in source["name"].names:
    if record.nameID in (1, 3, 4, 6):
        record.string = "GratisPreviewGlyphs".encode(record.getEncoding())
output = root / "Gratis.Preview/assets"
source.save(output / "NotoSansCJK-Glyphs.otf")
license = (output / "OFL.txt").read_text(encoding="utf-8").split("This Font Software", 1)[1]
(output / "LICENSE-NotoSansCJK.txt").write_text(copyright + "\n\nThis Font Software" + license, encoding="utf-8")
print(f"Preview fallback: {len(source.getBestCmap())} glyphs")
