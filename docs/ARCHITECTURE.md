# Architecture

Gratis targets Dalamud API 15 and .NET 10. The plugin owns game services and
native window lifecycle. Gratis.Preview runs the same map frame, geometry and
interaction with scripted data and a software renderer; see
[Gratis.Preview/README.md](../Gratis.Preview/README.md) for how to run it.

## Execution and ownership

`MapWindow` owns window lifetime and state; Host, Title, Style and Commands parts
adapt native behavior. `MapWindowFrame.Draw` owns following, child lifetime,
hover/focus, fading, chrome, overlays, zoom and dragging. `MapRenderer` adapts game
resources/markers to `MapRendererFrame`, which owns readiness, motion, transforms,
texture passes and paint order. Fog/recolour redraws reuse one frame's transform.

`MapDrawContext` exposes host resources/actions/state. Its Dalamud adapter separates
host reads from text, icon and game fonts. `DrawBinding` handles binding differences
and the pinned preview ABI. `MapFrameTrace` observes real shared widgets/surfaces,
markers and motion; it is null in the plugin and reuses buffers in Preview. It does
not audit unrelated Dalamud windows.

Drawing separates textures, frames, symbols, labels, interaction, chrome and cards.
Toolbar parts separate dispatch/state, Classic, segmented layout, menus and glyphs.
MapAssistance separates travel decisions/state from targets, cards, controls and
panels. Initialized fields remain in original declaration order: moving fields
between partial files can change construction order even when methods are unchanged.

## Threads and lifetime

- Read ObjectTable, agents, addons and camera/group state only on the main thread.
  Plugin construction can run off-thread; ZoneSwitchController defers its first
  player read to OnUpdate. Null players, missing addons and loading agents are
  real states. Never retain native pointers across callbacks.
- Hooks can request centring/pings without a valid ImGui context. Consume requests
  during drawing; do not read ImGui time or change focus from game hooks/events.
- Workers publish immutable sheets/CPU results. Never wait on them from drawing.
  PlaceIndex publishes batches of 128; live entries refresh at most every 750ms.
  Attunement/cost stay live, outside the account-independent sheet cache.
- Framework callbacks own GPU uploads/eviction. Reject stale worker results,
  observe failures and release late completions after cancellation. Content bounds
  cache 32 maps with at most two workers. Place artwork has two loaders, a 24-entry
  cache including pending/failed entries, and protects textures used in the last 2s.
- Hooks, events, rented textures and font handles have owners. Font scopes last
  through drawing. Raw font pointers/old atlas vertices cannot survive rebuilds;
  animated exits retain data, never GPU/font resources.

## Saved configuration

Keep SystemConfig, IconConfig, FlagConfig, Flag and their saved names, types,
defaults and filenames stable: `System.config.json`, `Icons.config.json`,
`Flags.data.json`. SystemConfig inherits MapDrawingConfig and legacy metadata:
Version (int, default 2), CharacterName ("Unknown Name"), CharacterWorld ("Unknown
World"), ContentId (ulong), LodestoneId (nullable string). Metadata is passive;
there is no Lodestone/portrait network client.

Production serialization uses System.Text.Json, IncludeFields, WriteIndented,
case-sensitive names and numeric enums. Saving uses the runtime type. No custom
converters, polymorphic tags or assembly-qualified type names are written.
Missing/null/invalid files fall back to defaults; errors are logged and writes use
Dalamud's safe-write helper. Keep this contract covered by `config`. The script
serializer separately accepts case-insensitive names/string enums.

Field initializers, SettingsEntry/SettingsLayout and TweakDefinition/TweakCatalog
share defaults, discovery and reset. Reflection-bound paths are API. Keep inert
compatibility fields such as QuickFilterBar, FilterBarAnchor and Filters.Mode;
the toolbar owns Filters. Explicit anchor markers distinguish user choices from
ambiguous old defaults. Section resets preserve unrelated settings. Resetting
presets removes saved presets without moving the current window.

## Coordinates, selection and viewport

World positions use X/Z as Vector2 X/Y; distances are yalms. Texture coordinates
are unscaled pixels around a 2048px map's 1024px centre. Native markers use
sixteenth-pixels. Sheet offsets have the opposite sign to AgentMap.SelectedOffsetX/Y.
Display coordinates also depend on the map size factor. Preserve operation order:
algebraic centre/offset rewrites can lose low bits near 1024 and clear spring
velocity on a false boundary hit.

Zone switching polls territory/map every 100ms, waits for 200ms of stable readiness
and bounds active retries to five seconds after loading. Only the latest request
waits through loading. User action/intentional browsing can consume the request;
another map change is needed to request again. Selection cannot open a closed
window or recurse into the external map hook. Floors follow CurrentMapId, not
player altitude. Layers match PlaceName, require nonzero MapIndex, sort by MapIndex
and label PlaceNameSub. Missing/misgrouped rows and floors without a native map-ID
update need live investigation; background artwork layers do not resolve floors.

Every Gratis map/session starts AutoFit. Ready frames recompute using actual
viewport/texture/DPI/content, including late presets/scans. Dimensions <=1/loading
placeholders defer transforms. Explicit pan/zoom or a newly enabled lock selects
Manual. Only explicitly manual views restore zoom; old entries without the flag
do not. Texture/map/session mismatches invalidate restores; same-ID instance
re-entry starts a new session. Lens memory is separate. MapViews caps at 100;
Spotlight's temporary framing is never saved. Dirty views save at most every 2s,
on map change and on close. Follow/centre locks win over remembered pan.

Window presets have five stable slots, work-area fractions and a monitor hint.
Restore before Begin, defer held input and clamp to the nearest work area,
including negative offsets. The normal 400x250 minimum scales with DPI, bounded
by available work area. Deletion clears assignments without renumbering other slots.

## Places and travel

PlaceIndexBuilder joins Aetheryte, Level, map markers, Map, TerritoryType and
ExVersion. Prefer Level world positions; marker fallback is
`(texture - 1024) / (Map.SizeFactor / 100) + sheet offset`. Display current-language
names; retain English/Japanese aliases. NFKD and ordered-subsequence token matches
rank exact/prefix/word-boundary matches above context; small map/distance/recency
bonuses refine results with stable name/ID ties. This is not edit-distance correction.
Nearest queries merge pre-culling frame targets, deduplicate family/position and
stay on the viewed map. Without a player there, distance is unavailable.

Toolbar-only search supports a:/z:/q:/m: scopes, nearest/n: queries and keyboard
selection. Enter shows via Search Spotlight without movement; Alt+Enter/cost
buttons request teleport. PaletteTeleportButton=false makes search display-only.
MSQ selection uses accepted/progress/completed objectives; another-map objective
can offer a 12s transport chip without switching maps/travelling until clicked.

GameTeleportService rechecks unlock, membership, price, game conditions and native
Teleport action status before Telepo.Teleport. Confirmations bind destination and
current price; >= TravelConfirmGil (default 1000) needs confirmation. Shards use
their own unlock, not the hub's. Marker DataType 3 keys are Aetheryte row IDs;
DataType 4 keys are AethernetName IDs. Unknown/ambiguous mappings retain normal
styling; the mapping cache caps at 512. Compare target-map positions; cross-map
hubs are fallback only for layers with no main crystal, never comparable coordinates.

Lifestream is optional; the inspected contract was 2.6.0.8 on 2026-10-24. Recheck
its documented IPC when updating:

| Gate | Signature |
| --- | --- |
| Lifestream.IsBusy | () -> bool |
| Lifestream.GetActiveAetheryte | () -> uint; 0 means absent |
| Lifestream.AethernetTeleportById | (uint Aetheryte row ID) -> bool |
| Lifestream.Teleport | (uint destination, byte subIndex) -> bool; Gratis uses 0 |
| Lifestream.Abort | () -> void |

TravelActions enables on first successful detection; TravelActionsInitialized
preserves later choices. A gate failure disables integration until reload; no chat
fallback exists. Independent nearest-aetheryte chips can use native teleport without
Lifestream; aethernet requires the provider. Gratis never owns walking, flight,
interaction or pathfinding. Imagery loads loadingimage _hr1.tex, then .tex, then
expansion icons; missing art uses a synthetic gradient/glyph. No game art is bundled.

## Attribution

Gratis retains Mappy/MappyGlass-derived code and Glint adaptations. KamiLib is no
longer a dependency; required Compat helpers keep MidoriKami/KamiLib origin and
AGPL notices. Keep those headers, LICENSE, NOTICE.md and licenses/KamiLib-LICENSE.
Geist/Lucide keep their notices. Historical similarity estimates were not authorship
or licensing findings; formatting/splitting does not establish independent provenance.
