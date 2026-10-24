# Gratis headless preview

Run from this repository with .NET 10 on Windows x64:

```powershell
dotnet run --project Gratis.Preview -c Release -- --scene map-default
dotnet run --project Gratis.Preview -c Release -- --scene map-circular
dotnet run --project Gratis.Preview -c Release -- --scene hover-tooltip
dotnet run --project Gratis.Preview -c Release -- --scene markers
dotnet run --project Gratis.Preview -c Release -- --scene toolbar
dotnet run --project Gratis.Preview -c Release -- --scene map-classic
```

Outputs default to `_preview/<scene>.png` (ignored by Git). Optional `--out path.png`,
`--size 1280x800`, and `--map art.png` select output, viewport, and original artwork.
Without `--map`, deterministic procedural terrain supplies busy art for contrast checks.
Limits: 320x240 minimum, 8192 per axis maximum, 16 megapixels maximum.

Gratis fixtures use the new default tokens, 14px markers at fixed screen size, a
hovered marker/card, all six marker categories, the player chevron/cone, and the
coordinate chip. The toolbar fixture hovers Settings. Circular and small viewports
exercise overlay placement and marker containment. Twelve synchronous frames
settle the 90ms hover transitions; only the final frame is rasterized.
`map-classic` retains the previous preview's 24px rounding, bright border/glow,
heavy vignette, zoom scaling, game-icon substitutes, and ASCII toolbar.

The preview compiles linked source from `Gratis/Rendering`, `DrawHelpers.cs`,
`MapShape.cs`, `MarkerType.cs`, and `IconSetting.cs`. All map geometry and chrome
come from these shared files. `MapWindowDrawing.cs` owns frame and surface drawing,
including the surface entry point for a later glass layer. Host adapters supply
textures, fonts, live marker preparation and toolbar actions. `DrawBinding` handles
binding differences and text measurement without loading or replacing any fonts.

The headless host uses ImGui.NET 1.91.6.1/native Dear ImGui 1.91.6 and SkiaSharp.
It needs no Dalamud installation, game, GPU, visible window or screenshot API.
The software renderer consumes actual ImGui draw lists, clip rectangles, vertex
RGBA/UVs and index offsets; it uses bilinear sampling and straight alpha blending.
Each saved PNG is decoded again to report dimensions, sample pixels, triangles,
and non-background coverage. Custom rendering callbacks fail explicitly.

Fidelity limits: bundled Noto Sans substitutes for the live application's fonts;
procedural game icons and ASCII Classic buttons substitute for game textures and
FontAwesome. Gratis shapes and toolbar icons are the same primitives in both hosts.
Classic intentionally retains its original centre-only marker containment and
circular toolbar clipping quirks. Gratis checks the complete flat symbol and halo;
large radius overlays and player geometry still use the existing rectangular clip.
Live game actions, docking, fog, blur, and host window behavior require an in-game
check. The plugin has one existing obsolete `FateContext.EurekaFate` warning.
No font or glass implementation is included in this visual pass.

Classic regression check: rendering the original HEAD fixture separately produced
the exact same PNG bytes as the new `map-classic` scene (SHA256
`68AEFCD3FFABCF87515B6DFA549D01C564D1716EBF10C5B50FC914DA656596C7`).
Both projects build in Release; all six scenes and smaller rectangular/circular
viewports were rendered and inspected. The in-game window cannot be run headlessly.

Noto Sans Regular is bundled under the SIL Open Font License; see
[assets/OFL.txt](assets/OFL.txt). Its source is
[notofonts/noto-fonts](https://github.com/notofonts/noto-fonts/tree/main/hinted/ttf/NotoSans).
ImGui.NET and SkiaSharp are pinned NuGet dependencies.
