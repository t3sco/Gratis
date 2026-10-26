# Gratis
Gratis is a Dalamud plugin that replaces the Final Fantasy XIV map window with a flat, minimal interface around the game's maps. It is a fork of Mappy, with its own map presentation, place search and travel controls.

## Language

### Map and markers

**Marker**:
A symbol at a location on the Gratis map, representing a place, person, activity or destination.
_Avoid_: map icon, map dot

**Marker family**:
A marker's specific meaning, such as an inn, a mender, a quest or an aetheryte.
_Avoid_: icon family, marker kind

**Marker category**:
One of the six broad visual categories: point of interest, transport, objective, link, service or party. A category contains several marker families.
_Avoid_: palette category, declutter category

**Visibility group**:
A collection of markers with a shared lens and filter setting, such as housing, treasure or pins. These groups are more detailed than the six marker categories.
_Avoid_: filter category, marker group

**Priority marker**:
A marker whose gameplay meaning or current selection protects it from a lens's Hidden setting. Quests, FATEs, objectives, flags, aetherytes and party members are examples; an explicit filter mute still takes precedence.
_Avoid_: important marker, priority icon

**Protected marker**:
A marker exempt from declutter clustering, including priority markers and additional gameplay markers such as gathering points and map links.
_Avoid_: declutter exception, protected icon

**Ambient marker**:
A marker eligible for declutter clustering when it has no current gameplay protection, such as an unselected shop or landmark.
_Avoid_: background marker, unimportant marker

**Decluttering**:
The optional grouping of nearby ambient markers from the same marker category into a count badge. Protected markers remain individual markers at their original locations.
_Avoid_: density hiding, marker thinning

**Plate**:
The shaped backing around a Gratis marker's symbol, with colour and outline belonging to its visual meaning.
_Avoid_: icon background, marker backing

**Glyph**:
The outline or filled symbol within a Gratis marker or toolbar control.
_Avoid_: pictogram, icon artwork

**Label**:
Text beside a map marker or pin, identifying it without an open card.
_Avoid_: marker caption, inline name

**Quest marker**:
A marker for a quest, with a state such as available, in progress, complete or locked. Main scenario and feature quests have distinct meanings within this family.
_Avoid_: quest pin, quest icon

**Objective marker**:
A marker for a current gameplay target, including an accepted quest's next location. This is narrower than the objective marker category, which also contains flags and FATEs.
_Avoid_: objective pin, task marker

**FATE marker**:
A marker for a field event, including its battle, collection, defence or escort variant.
_Avoid_: FATE pin, event icon

**Flag**:
The game's coordinate destination marker, also represented in Gratis and its flag history.
_Avoid_: waypoint, coordinate pin

**User pin**:
A saved Gratis map location with a title, optional note and chosen colour, separate from the game's flag.
_Avoid_: bookmark, saved marker

**Player arrow**:
The directional marker for the player's own character on the Gratis map.
_Avoid_: player chevron, player cursor

**Player dot**:
A circular marker for another player, including party and alliance members.
_Avoid_: party arrow, player icon

**Edge pointer**:
A marker at the map's edge for a target outside the visible area, such as a flag, objective or selected pin.
_Avoid_: offscreen chip, edge indicator

**Spotlight**:
A temporary map view focused on a quest, flag, search result or user pin, optionally including the player. Its emphasis and framing belong to that selection rather than the remembered map view.
_Avoid_: quest highlight, target focus

### Interface

**Chrome**:
The controls and information surfaces around the map artwork, including the toolbar, location card and coordinate card.
_Avoid_: map furniture, interface decoration

**Lens**:
A named presentation of visibility groups using Loud, Normal, Quiet or Hidden states, with an optional map tint. Built-in and custom lenses share this vocabulary.
_Avoid_: visibility preset, map mode

**Filter**:
A temporary visibility choice for one visibility group, taking precedence over the active lens.
_Avoid_: visibility override, group mute setting

**Filter drawer**:
The panel of temporary marker visibility controls attached to the toolbar's Filters button.
_Avoid_: filter bar, layers panel

**Peek**:
A temporary view of marker groups at normal visibility while the chosen peek key is held.
_Avoid_: reveal all, filter bypass

**Hover card**:
A temporary information card for the markers beneath the pointer, one row each, with their names and any additional details.
_Avoid_: marker tooltip, hover tooltip

**Location card**:
The map's zone and area name display among the chrome surfaces.
_Avoid_: title card, location chip

**Coordinate card**:
The chrome surface containing player and cursor coordinates.
_Avoid_: coordinate bar, coordinate chip

**Place chip**:
A compact map control for a suggested nearby place, such as a market board or inn.
_Avoid_: nearest-place button, place shortcut

**Ghost mode**:
The optional faint, click-through state of the Gratis map while it is not being used.
_Avoid_: passive map mode, click-through mode

**Keep controls visible**:
The chrome setting for a toolbar and information surfaces that remain visible while the pointer is away.
_Avoid_: pin chrome, pin controls

**Follow mode**:
The map view centred on the player's position, with a return to the player after temporary browsing.
_Avoid_: player tracking, follow player

**Remembered map view**:
A map's saved manual zoom and pan, with optional lens memory.
_Avoid_: saved viewport, map preset

### Places and travel

**Place**:
A named destination or location available to Gratis search, including zones, aetherytes, quests, markers, flags and user pins.
_Avoid_: search location, search destination

**Place index**:
The catalogue of named game places available to Gratis place search.
_Avoid_: place database, location catalogue

**Place search**:
The toolbar's search field and results panel for named places, with recent places and expansion browsing.
_Avoid_: palette, location search

**Aetheryte**:
A main teleport crystal represented as a transport destination in Gratis, with attunement and any applicable gil cost.
_Avoid_: main crystal, teleport stone

**Aethernet shard**:
A local transport crystal belonging to a hub's aethernet, with its own attunement state.
_Avoid_: shard, mini aetheryte

**Aethernet**:
The local travel network joining an aetheryte hub and its shards.
_Avoid_: shard network, local teleport network

**Travel card**:
A destination card with a teleport or aethernet offer, cost or availability information, and an explicit travel action.
_Avoid_: teleport card, travel popover

**Guide line**:
The straight dotted connection between the player and a Spotlight target, representing direction rather than a walkable route.
_Avoid_: route, path

### Settings

**Gratis style**:
The flat visual style with Gratis marker presentation, chrome and interface options.
_Avoid_: modern style, new style

**Classic style**:
The legacy Mappy-derived map presentation retained alongside Gratis style.
_Avoid_: original mode, upstream mode

**Window preset**:
A named saved size and screen position for the map window, with an optional duty-specific assignment.
_Avoid_: window profile, layout preset

**Frame style**:
The chosen treatment of the map window's edge, including its border and shadow.
_Avoid_: frame preset, border theme

**Colour preset**:
A named colour treatment for the map and its markers.
_Avoid_: colour palette, map theme

**Tweak**:
A small, independent Gratis option in the Tweaks catalogue, with its own description and reset.
_Avoid_: enhancement, quality-of-life feature

### Project

**Gratis Preview**:
The offline preview and verification companion for Gratis, with scripted map scenes and selftests that run without the game.
_Avoid_: mock map, screenshot generator

**Feedback relay**:
The Gratis service between an explicitly submitted feedback message and the developer's private Discord forum.
_Avoid_: feedback webhook, telemetry service

**Diagnostic report**:
A local account of Gratis settings and technical state, with character names and chat excluded. It is an optional attachment to an explicitly submitted feedback message.
_Avoid_: crash report, telemetry dump
