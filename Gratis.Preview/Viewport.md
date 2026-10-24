# Map viewport behaviour

`ScaleWithFrame` and `ClampToMap` default to `true` in both visual styles.
The Map Functions tab contains their controls and a reset button. Missing fields
in older JSON files keep those defaults; saved `false` values survive reload.

Scaling uses `min(frame width, frame height)` relative to the initial frame,
with the user's zoom retained in double precision per frame pixel. Uniform
resizes preserve the map centre and visible region. Changing aspect ratio preserves
the short-axis fraction and reveals more along the longer axis. This absolute
fit reference avoids path-dependent drift and uses a circle's diameter. Temporary
coverage limits do not change the user's relative zoom. The 0.05–20 user zoom
limits are frame-relative while scaling is enabled, so enlarging at maximum zoom
still preserves the region. With scaling disabled they remain the original
absolute limits. Texture coverage can raise the minimum above that limit.

Clamping uses the actual texture dimensions. Rectangle bounds conservatively
include rounded corners; circle bounds use the diameter. Classic's existing
non-square circle is drawn as a rounded rectangle, so its full rectangle is
covered conservatively. There is no map texture rotation: player and camera
symbols rotate independently, so no rotated-map bounds are needed.

Cursor zoom anchors in texture coordinates before clamping. Follow mode retains
the resulting offset relative to the moving player, so the next frame does not
undo the anchor. Explicit centering clears that offset; panning exits follow as
before. At an edge the bounds constraint takes priority over the cursor anchor
and centering the player. Disabling both toggles restores pixel-scale resizing,
unbounded pan, and the original centre-based wheel zoom.

Run `dotnet run --project Gratis.Preview -c Release -- --selftest viewport` for
numeric coverage, resize, cursor-anchor, old-settings, reset, and legacy checks.
Scenes: `viewport-small`, `viewport-large`, `viewport-unscaled`,
`viewport-clamp-corner`, `viewport-clamp-off`, and `viewport-circle-corner`.
The first pair uses the same texture-space region at 432×288 and 864×576;
white crosses mark fixed texture coordinates. Corner scenes deliberately request
a centre outside the texture and zoom below coverage. `--viewport-off` disables
both behaviours explicitly, including in the viewport scenes.
