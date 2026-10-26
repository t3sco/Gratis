# 0002: Merge the icon fonts

Gratis uses Lucide outline symbols for the interface and quest markers, alongside Phosphor Fill symbols for marker plates. Build and embed one merged icon font with tools/icon-font/build.py, remapping Phosphor symbols into the private-use range starting at U+F100, rather than using per-icon textures or selecting between two icon fonts. Both symbol sets then share a font-loading path, at the cost of a custom build step and character assignments that must remain consistent between the generated font and the drawing code.
