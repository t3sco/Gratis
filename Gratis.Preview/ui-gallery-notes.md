# UI gallery coverage and extraction gaps

`ui-gallery` and `ui-gallery-classic` render real linked plugin code. Bundled Noto Sans
is used for gallery labels. Adjacent generated coverage JSON is the cell-level source,
state and crop ledger. No proposed UI-library API is implemented here.

Direct rows cover the button substrate, icon/close/pin/search buttons, text/quiet/action/
primary faces, switch/Classic checkbox, slider, combo, menu/selectable adapters, pill,
palette background, text/colour editors, lens key capture, surface painter, scroll region/
bar, four text roles, and icon plus label. Presenter crops supply the remaining kinds.

## Not isolatable yet

These are crops from the named **real presenter with an existing fixture**, not independent
component calls. They form the extraction backlog:

- Toolbar segments, selection fill, notification dots and divider: `MapToolbarDrawing`.
- Teleport face, cost, confirmation-disabled action, heading/detail pill, close and card:
  `MapAssistance.TeleportCard`. Confirmation timing and travel policy stay domain-owned.
- Update action, dismiss, label/badge and notice shell: `UpdateNotice`. Its existing lack
  of press/focus decoration is preserved.
- Filter icon chips, normal/quiet/hidden/solo/loud states and drawer: `FilterBarDrawing` and
  `MarkerVisibility`. These are multi-state controls.
- Palette scope/quest-filter segments, search/clear, result/quick/expansion/artwork rows,
  empty/helper content and selection: `PaletteOverlay` with `PaletteFixture`.
- Quest tabs, toggle/pin, sticky headers, rows, search, progress, map action, reward
  metrics/count badges/artwork and empty states: `QuestLogPanel` with `QuestLogFixture`
  and `SummaryFixture`.
- Item content tooltip and reward menu: `ItemTooltipDrawing`, reached by real hover and
  right-click over a summary reward.
- Settings navigation, Gratis/Classic value rows, headers/dividers, search and Tweaks
  rows: `SettingsLayout` and its catalogue. They own layout/reflection and config.
- Location search, hierarchy/menu rows and dropdown: `LocationMenuDrawing` with the
  existing location-menu verification source.
- Context-menu rows and popup entry/exit: `ContextMenu` with `ContextMenuFixture`.
- Feedback field, checkbox, privacy note, dimmed unavailable Send, buttons and popup:
  `FeedbackButtonDrawing`. The fixture never invokes Send or external links.
- Pin title/note fields, selected colour swatches, buttons and editor: `PinOverlay`
  with the existing pin store fixture.
- Bottom location/coordinate cards: `BottomCardsDrawing` / `MapCoordinateDrawing`.
- Hover-card stack and native Classic tooltip: `DrawHelpers.Interaction` / `HoverCardDrawing`.
- Move affordance and ghost hint: `MoveHandleInput` / `MapWindowFrame.Ghost`.

## State and host limits

Drawing-only primitives have no invented selected/disabled/focus states. Native disabled
scopes represent caller policy. Keyboard navigation is enabled for focused primitive
captures and disabled between them, clearing the native nav cursor before rest captures.
Rest cells assert that no focus decoration remains from a prior cell. Classic's generic
Lucide button cells are marked Gratis-only: its real native toolbar below supplies the
existing ASCII host substitutes for game/FontAwesome icons. Classic uses native checkbox/slider paths, matching
`SettingsEntry`. Quiet/action/primary faces retain their Gratis domain gate. Modern
palette, quest, filter, travel, pin, feedback and update presenters are Gratis-only.
Invisible palette targets retain the presenter's keyboard selection model. Update
retains its existing state omissions. Unavailable Send retains dimming, not new native
disabled semantics.

The context-menu exit currently fades its background while nested menu rows disappear
immediately: the retained exit window has a new native ID scope, so its nested row motion
starts hidden. The baseline deliberately records this existing drawing behavior. Fixing
that production motion behavior is outside this visual-safety-net task.

Legacy game-host quest/FATE/selection/flag windows and native legacy TabBar variants
require Dalamud/game adapters; they are not skins of the modern panel. Their reusable
native control kinds are represented through linked adapters, not fabricated presenters.
Game actions, live fonts/CJK fallback, game textures, docking/window resizing and GPU blur
still need an in-game check. Procedural icons/artwork and CPU blur have the existing
harness's documented fidelity limits.

## Test-only seams and fixture changes

- `ChromeControls` and `ChromeControls.Motion` have before/after Button/TextButton probes
  under `#if GRATIS_PREVIEW`. They are
  absent from the plugin build. Without callbacks they do nothing. Gallery focus calls
  native `SetKeyboardFocusHere`; after probes observe native state/bounds.
- `ScriptedFrameDriver` has harness-only overlay and saved-frame callbacks for control
  frames and crop discovery. A fixture opt-in preserves native window order, preventing
  the scripted host's forced map focus from hiding popup exit windows. Default behavior
  is unchanged.
- `PopupDiagnostics` records bounds, alpha, kind and frame only in `GRATIS_PREVIEW` for
  accurate popup crops and assertions that exit samples are visible and mid-transition.
- The existing location verification source is internal so the scene can reuse it.
- Preview `--style gratis|classic` uses existing map/settings style dispatch.
- The quest scene adds Completed and optional scripted gallery target states.
- Preview pin fixtures use fixed GUIDs. Production pin behavior is unchanged.
- `library-*` scenes arrange existing fixtures and replay input at fixed times, including
  retained noninteractive popup exits and the real teleport confirmation guard. They
  explicitly enable Classic's existing always-show-toolbar option so off-map rest samples
  contain the intended toolbar rather than its default hidden state.
