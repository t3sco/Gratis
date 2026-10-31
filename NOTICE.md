# Attribution

Gratis is based on harbingerftw/Mappy version 3.2.0.0, licensed under AGPL-3.0:
https://github.com/harbingerftw/Mappy

Credit to MidoriKami and harbingerftw for Mappy, and to the KamiLib/MappyLib
authors and contributors for their library work.

The original AGPL-3.0 license is retained in LICENSE. The KamiLib code still
needed by Gratis was copied into Gratis/Compat, with small one-call helpers
inlined at their call sites. Its original license is retained unchanged in
licenses/KamiLib-LICENSE. Existing authorship and license notices remain applicable.

Geist (Regular, Medium, SemiBold) and Geist Mono Regular, by Vercel, are
bundled from the official v1.8.2 release under the SIL Open Font License 1.1:
https://github.com/vercel/geist-font/releases/tag/v1.8.2
The complete license and copyright notice are in Gratis/Assets/Fonts/OFL.txt.

Lucide icon font from the official lucide-static 1.61.0 npm package:
https://www.npmjs.com/package/lucide-static/v/1.61.0
Lucide Icons and Contributors, ISC license (with MIT notices for Feather-derived
icons). The complete upstream license is in Gratis/Assets/Fonts/Icons/LICENSE-Lucide.txt.
Only the TTF and license are used; named codepoints come from that package's
font/codepoints.json. The package was downloaded as data; no package scripts ran.

Phosphor Icons (Fill weight), https://github.com/phosphor-icons/web, MIT license. The solid
marker glyphs come from it; the license is in Gratis/Assets/Fonts/Icons/LICENSE-Phosphor.txt.
The Lucide TTF and a subset of the Phosphor TTF are merged into the
single Gratis/Assets/Fonts/Icons/icons.ttf that the plugin embeds.

## Authorship of the code this project builds on

Gratis started as a copy of Mappy 3.2.0.0 and its bundled KamiLib source. The git history
of this repository begins at that point, so it does not show the earlier authors. Their work
is the foundation of the map window, marker handling, configuration and much of the
plugin structure:

- MidoriKami: original author of Mappy and KamiLib (copyright 2022, AGPL-3.0; see LICENSE
  and licenses/KamiLib-LICENSE).
- harbingerftw: maintainer of the Mappy fork this project is based on (version 3.2.0.0).
- The KamiLib/MappyLib contributors credited in those repositories.

Gratis changes and additions are by t3sco. Files that keep Mappy/KamiLib code remain under the
AGPL-3.0 with their original copyright notices. Source for every released build is in this
repository, as the AGPL requires.

## Third-party code shipped in the plugin

KamiLib's unused Lodestone/portrait code and its NetStone, HtmlAgilityPack,
Karashiiro.HtmlAgilityPack.CssSelectors.NetCoreFork, Newtonsoft.Json and
Microsoft.Extensions.ObjectPool dependencies have been removed. These libraries
are not bundled in the release zip.

Gratis is built against the Dalamud plugin framework (goatcorp), FFXIVClientStructs
(aers and contributors) and Lumina (NotAdam and contributors), which are provided by the
game's plugin host and are not redistributed here. The offline preview tool used for testing
(not shipped) uses ImGui.NET by Eric Mellino and SkiaSharp by Microsoft, both MIT.

## Game content and inspiration

FINAL FANTASY XIV, its map artwork, icons and other game data are copyright Square Enix.
Gratis reads this data from the player's own installation and does not redistribute it. Any
game artwork shown in screenshots here is copyright Square Enix. Gratis is a fan project and is
not affiliated with or endorsed by Square Enix.

Design inspiration, with no code taken: the WoW addon OpenMap (seamless region view idea).
