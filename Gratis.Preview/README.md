# Gratis headless preview

Run from this repository with .NET 10 on Windows x64:

```powershell
dotnet run --project Gratis.Preview -c Release -- --scene map-default
dotnet run --project Gratis.Preview -c Release -- --scene map-circular
dotnet run --project Gratis.Preview -c Release -- --scene hover-tooltip
dotnet run --project Gratis.Preview -c Release -- --scene markers
dotnet run --project Gratis.Preview -c Release -- --scene toolbar
dotnet run --project Gratis.Preview -c Release -- --scene map-classic
dotnet run --project Gratis.Preview -c Release -- --scene map-feather
dotnet run --project Gratis.Preview -c Release -- --scene map-feather-circular
dotnet run --project Gratis.Preview -c Release -- --scene map-edge-legacy
dotnet run --project Gratis.Preview -c Release -- --scene map-edge-legacy-circular
dotnet run --project Gratis.Preview -c Release -- --scene glass-on --verify
dotnet run --project Gratis.Preview -c Release -- --scene glass-off
dotnet run --project Gratis.Preview -c Release -- --scene glass-toolbar
dotnet run --project Gratis.Preview -c Release -- --scene glass-card --verify
dotnet run --project Gratis.Preview -c Release -- --scene glass-tinted
dotnet run --project Gratis.Preview -c Release -- --scene glass-unavailable
dotnet run --project Gratis.Preview -c Release -- --scene glass-circle
```

Outputs default to `_preview/<scene>.png` (ignored by Git). Optional `--out path.png`,
`--size 1280x800`, and `--map art.png` select output, viewport, and original artwork.
Without `--map`, deterministic procedural terrain supplies busy art for contrast checks.
Limits: 320x240 minimum, 8192 per axis maximum, 16 megapixels maximum.
`--feather-width 0..48` exercises the edge-mask range. Legacy-edge scenes explicitly
disable feather; all other Gratis scenes use its 6px default. The renderer reports
the map mesh vertex count: 253 for the default rect, 1,153 for the default circle,
at most 4,645 per map image (eight smoothstep bands plus an opaque interior fan).
UVs are affine in screen position, preserving texture sampling through every triangle.
The legacy rect / circle outputs match 66e6227 byte-for-byte (SHA256 respectively
`C5FD20C23A782231101A40C278B1BFE5B2EC896C17BE4412F44D557132F892C1` and
`49D4A05ABAA8E9F971DDFE9B96DB2E4F882E8A01F0DBA1827DFB9D22623DB780`).

Glass scenes enable the tint, grain and host blur capability. `glass-card` hovers a
marker and exercises all four chrome surfaces; `glass-toolbar` hovers Settings.
`glass-tinted` disables blur; `glass-unavailable` simulates an unavailable host API.
Their PNGs must match. `glass-on` and `glass-off` share an identical fixture.
`glass-circle` adds a fully visible circle, supported by a rounded-square host mask.
Partially clipped circles use tint; chrome glass never blurs the whole map.
See [glass diagnostics and defaults](../docs/GLASS.md).

Harness blur is a **CPU box-blur approximation** of the already-drawn backdrop at
the surface's index position, using an immutable padded snapshot and a rounded mask.
No callbacks or sentinel textures are emitted. Dalamud owns the live blur kernel;
the radius UI is nominal (12px maps to host strength 3 at 1×), not pixel parity.
`--verify` runs 36 independent analytic mask/UV cases, including partial images and
16-bit vertex rollover. It also checks blur confinement, mean change <2%, reduced
high-frequency energy, material reset and settled secondary-text contrast >=4.5:1
over a white backdrop at all effective tint/grain extremes (including old settings).

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
including the single surface entry point for glass blur/tint. Host adapters supply
textures, fonts, live marker preparation and toolbar actions. `DrawBinding` handles
binding differences and text measurement without loading or replacing any fonts.

The headless host uses ImGui.NET 1.91.6.1/native Dear ImGui 1.91.6 and SkiaSharp.
It needs no Dalamud installation, game, GPU, visible window or screenshot API.
The software renderer consumes actual ImGui draw lists, clip rectangles, vertex
RGBA/UVs and index offsets; it uses bilinear sampling and straight alpha blending.
Each saved PNG is decoded again to report dimensions, sample pixels, triangles,
and non-background coverage. Custom rendering callbacks fail explicitly.

Fidelity limits: bundled Noto Sans substitutes for the live application's game fonts;
procedural game icons and ASCII Classic buttons substitute for game textures and
FontAwesome. Gratis shapes and toolbar icons are the same primitives in both hosts.
Classic intentionally retains its original centre-only marker containment and
circular toolbar clipping quirks. Feather-on Gratis conservatively culls complete symbols,
radius overlays and player cone bounds against the opaque inset contour. These disappear
as a unit near the edge rather than drawing opaque symbols over faded-away terrain.
Live game actions, docking, fog, host blur, and host window behavior require an in-game
check. The plugin has one existing obsolete `FateContext.EurekaFate` warning.
Geist chrome fonts and glass are included; CPU blur cannot establish GPU parity.
At 320×240, chrome still overflows; conservative edge culling hides large overlays
and cones as a unit instead of feathering their individual pixels.

Classic regression check: rendering the original HEAD fixture separately produced
the exact same PNG bytes as the new `map-classic` scene (SHA256
`68AEFCD3FFABCF87515B6DFA549D01C564D1716EBF10C5B50FC914DA656596C7`).
Both projects build in Release; all six scenes and smaller rectangular/circular
viewports were rendered and inspected. The in-game window cannot be run headlessly.

Noto Sans Regular is bundled under the SIL Open Font License; see
[assets/OFL.txt](assets/OFL.txt). Its source is
[notofonts/noto-fonts](https://github.com/notofonts/noto-fonts/tree/main/hinted/ttf/NotoSans).
ImGui.NET and SkiaSharp are pinned NuGet dependencies.

Gratis toolbar glyphs use the same bundled lucide-static 1.61.0 TTF and explicit
codepoint ranges as Dalamud (12�24px, default 16px). Glyph ink bounds determine
centring. Missing Lucide assets retain the primitive toolbar fallback. Category
swatches retain marker shapes so their meaning stays consistent with the map.
Classic's `a8e77a7` PNG hash remains `68AEFCD3FFABCF87515B6DFA549D01C564D1716EBF10C5B50FC914DA656596C7`.

Chrome visibility: `chrome-hidden`, `chrome-visible`, `chrome-fading` use fixed
progress 0, 1, and 0.5, with glass enabled. At 0, the title retains 35% alpha;
toolbar and coordinates reach zero. All use shared drawing code. Live visibility
uses ImGui time, a 1.2s idle delay, and a smoothstep fade over 180ms. Hover, drag,
zoom, an open context/layers popup, a visible hover card, or open settings reveals
immediately. Pin / disabling auto-hide keeps full alpha. Hidden toolbar areas
reveal before pointer input is processed and retain their button actions; they do
not act as pass-through map drags. Existing toolbar switches apply when auto-hide
and pin are off. Blur strength decreases with surface alpha because the host blur
API has no separate opacity argument. Classic ignores these settings entirely.

`--verify` also checks chrome timing/easing, minima, reveal/pin/reset/Classic,
Lucide glyph bounds at 12/16/24px, hidden blur suppression, and a first-hover click.


Settings UI uses the same linked `SystemConfig`, `SettingsLayout`, catalog and ImGui
rows in both hosts. Render `_preview/settings-window.png` with:

```powershell
dotnet run --project Gratis.Preview -c Release -- --scene settings-window --verify
dotnet run --project Gratis.Preview -c Release -- --selftest config
dotnet run --project Gratis.Preview -c Release -- --selftest settings
```

Other settings fixtures: `settings-classic`, `settings-search`, `settings-map`,
`settings-markers`, `settings-chrome`, `settings-tools`, `settings-advanced`.
The config selftest uses an embedded legacy fixture without accessing external configs. It checks retained fields, ignored removed fields,
new defaults and round-trip compatibility using the plugin's serializer options.
Settings checks cover search, 37 Classic-only controls and scoped resets. Game-owned
marker controls are labelled placeholders; game tool buttons are disabled in preview.
Their shared placement renders, while the exact game adapters need an in-game check.
See [the settings audit](../docs/SETTINGS_AUDIT.md) for every field and its reader.


## Scripted window frames and live parity

The shared window/renderer host runs real chrome, viewport, motion and mouse input
logic. Run every selftest (non-zero exit on any failure), or capture a timeline:

```powershell
dotnet run --project Gratis.Preview -- --selftest all
dotnet run --project Gratis.Preview -c Release -- --script Gratis.Preview/scripts/frame-glue.json --out _preview/frames
dotnet run --project Gratis.Preview -c Release -- --scene live --config "$env:APPDATA\XIVLauncher\pluginConfigs\Gratis\System.config.json" --map-id 134 --out _preview/live.png
dotnet run --project Gratis.Preview -c Release -- --diff _preview/ours.png _shots/reference.png --out _preview/diff.png
```

See [PARITY.md](../docs/PARITY.md) for the state schema, `/gratis shot` workflow,
read-only saved configs, mutation evidence and the exact/approximated boundary.
