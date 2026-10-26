<div align="center">

# Gratis

**A flat, glassy map for Final Fantasy XIV.**

Search for any place in Eorzea, keep your quests in sight, browse your whole quest log beside the map, and get to an aetheryte faster.

**Add to Dalamud:** copy this into `/xlsettings` > Experimental > Custom Plugin Repositories.

```
https://raw.githubusercontent.com/t3sco/Gratis/master/repo.json
```

[Install](#install) · [Features](#features) · [Commands](#commands) · [Feedback](#feedback) · [Credits](#credits)

![Status: early testing](https://img.shields.io/badge/status-early%20testing-C9A24B)
![Dalamud API 15](https://img.shields.io/badge/Dalamud-API%2015-4C8BF5)
![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0-lightgrey)
![Downloads](https://img.shields.io/github/downloads/t3sco/Gratis/total?color=4C8BF5)

<img src="docs/images/search-browser.png" alt="Gratis place search, browsing by expansion" width="300"> &nbsp; <img src="docs/images/search-places.png" alt="Gratis place search results with aetheryte, flag and marker chips" width="300">

</div>

## Why Gratis

Gratis keeps the maps from the game and replaces the interface around them. It has a flat glass
look in warm gold and crystal blue, a search built on Eorzea's own place names, and a long list of
small tweaks that each save a click. It also has an optional Quest Log that can stand in for the game's Journal.

<img src="docs/images/before-after-wipe.png" alt="The same Thanalan map in the game's own window and in Gratis, with a divider sweeping between them" width="420">

*The game's own map window against the same map in Gratis.*

## Features

### Search

![The Gratis search panel with the toolbar and the expansion browser](docs/images/banner-search.png)

- Click the search icon and type a zone, aetheryte, quest, marker or NPC. Each result shows its distance,
  whether you are attuned, and what kind of place it is.
- With an empty box you can browse by expansion, each with its own cover art.
- Recent places, a "Nearest aetheryte" shortcut, and chips for the nearest market board and inn.
- The keyboard works throughout: arrows to move, Enter to show, Tab to filter, Esc to close. Hovering with the mouse never scrolls the list.

### Interface

- The glass toolbar and info cards fade when the pointer is away. An optional click-through "ghost" mode
  lets clicks pass to the game.
- Motion can be interrupted at any point. Choose Full, Reduced or Off, and set the speed.
- Dragging has inertia, follow mode eases back to you, and each zone remembers its view.
- Small rooms and raid arenas fit the window, including right after you teleport. Wide, short windows scale the map to cover the whole window.
- Popups and menus share the same rounded glass look, and the coordinates sit in their own cards at the bottom.

### Markers

![Quest, flag and service markers on a recoloured map, with a quest hover card](docs/images/banner-markers.png)

- Quests, FATEs, objectives, flags, aetherytes and your party count as priority markers. Decluttering and
  lenses never hide them; only a filter you set yourself can.
- Other players are small dots and you are an arrow. The arrow follows your camera, with adjustable smoothing.
- Six lenses (Explore, Quests, Gather, Hunt, Travel, Minimal) and a filter drawer thin out a busy zone.
- Pets are not drawn.

### Quest Log

![The Gratis Quest Log docked beside the map](docs/images/quest-log.png)

- A panel beside the map lists your Accepted, Completed and Available quests, grouped by type, with a search
  button next to close. Your current main scenario quest has a slow gold glow, and each scenario row shows
  its expansion crest.
- Click a quest for a summary panel: where to go, requirements, objectives and rewards. Right-click a quest to
  show it on the map. Pin keeps the log open.
- Hover a reward for the game's item tooltip, or right-click it to link it in chat or search for it.
- Available lists quests you could pick up. Names you have not unlocked yet are hidden by default; there is a
  spoiler setting if you would rather see them.
- Optional: replace the game's Journal. With the setting on, opening the Journal opens the map with the log
  instead. Unbind or leave the key as it is; the native window is kept offscreen while this is on.
- Not done yet: abandoning and tracking quests from the log.

![A quest summary beside the Quest Log](docs/images/quest-summary.png)

### Travel

- Aetheryte chips show whether you are attuned and what the teleport costs. They do not take over map clicks.
- Travel offers are optional and degrade quietly. Gratis can show and start a trip, but it never moves your character.

### Settings

![The Appearance page in Gratis settings](docs/images/banner-settings.png)

- A Tweaks page lists each small quality-of-life option as one toggle with a one-line description and a reset.
- Five named window presets, with an optional separate preset for duties.
- Frame styles, colour presets, a circular map, and a Classic style that looks as it did before.
- One bundled font, a fixed font budget, and a frame-cost readout in diagnostics.

## Install

Gratis is in early testing and is not yet in the official Dalamud repository.

1. In game, open `/xlsettings`, then Experimental, then Custom Plugin Repositories.
2. Add this URL, tick Enabled and save:
   `https://raw.githubusercontent.com/t3sco/Gratis/master/repo.json`
3. Open `/xlplugins`, search for Gratis and install it.
4. If you run another map replacement plugin, disable it while testing so the two do not fight over the map.

Open the map with `/gratis`. The Quest Log button is at the top right of the map.

## Commands

| Command | What it does |
| --- | --- |
| `/gratis` | Open or close the map |
| `/gratis report` | Save a diagnostics report and screenshot (no character names) |
| `/gratis text`, `markers`, `motion`, `camera`, `chrome`, `glass`, `popups`, `pvp` | Focused diagnostics |
| `/gratis journal debug`, `/gratis journal log` | Trace the Journal replacement and print it |

## Feedback

This is an early build and has rough edges. If something looks wrong, run `/gratis report`, then open an issue
or message me with the saved file and a description of what you saw.

You can also use the speech-bubble button in the bottom-left corner of the map. It opens a small form that sends
your message to me. It goes to a small relay hosted on Vercel, which posts it to a private Discord forum that only I
can read. This is the only time Gratis contacts a server of its own: nothing is sent until you press Send,
and only your message and the Gratis version go. Tick "Include a diagnostic report" to add the same report that
`/gratis report` makes (settings and technical details, no character name or chat). The relay that receives it is in
[`relay/`](relay). Turn the button off under Settings > Feedback button.

## Credits

- [Mappy](https://github.com/harbingerftw/Mappy), by MidoriKami (original author) and harbingerftw (maintainer of the
  fork this is based on, version 3.2.0.0). The map window, marker handling and plugin structure come from their work.
- KamiLib and MappyLib, by MidoriKami and contributors, which Gratis still uses.
- [Geist](https://github.com/vercel/geist-font) by Vercel (SIL Open Font License), Noto Sans CJK by Adobe and Google (SIL Open Font License, preview harness only), [Lucide](https://lucide.dev) icons
  by Lucide Contributors (ISC) and [Phosphor](https://phosphoricons.com) icons by Phosphor Icons (MIT).
- Dalamud (goatcorp), FFXIVClientStructs and Lumina, which Gratis is built against.

[NOTICE.md](NOTICE.md) has the full list, with licences and what is derived from what.

FINAL FANTASY XIV is a registered trademark of Square Enix. Gratis is a fan project and is not affiliated with
or endorsed by Square Enix. Screenshots contain game artwork copyright Square Enix.

Licensed under AGPL-3.0. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).
