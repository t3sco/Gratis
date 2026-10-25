# Gratis

Gratis is a Dalamud plugin providing a flat, minimal map UI for Final Fantasy XIV.
Open it with `/gratis`.

Based on [harbingerftw/Mappy](https://github.com/harbingerftw/Mappy) version 3.2.0.0,
with credit to MidoriKami, harbingerftw, and the KamiLib/MappyLib contributors.

Licensed under AGPL-3.0. See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).

## Testing builds (custom repository)

Gratis is in early testing and is not in the official Dalamud repository.

1. In game, open `/xlsettings` -> Experimental -> Custom Plugin Repositories.
2. Add this URL, tick Enabled, and save:
   `https://raw.githubusercontent.com/t3sco/Gratis/master/repo.json`
3. Open `/xlplugins`, search for Gratis and install it.
4. Disable the Mappy plugin while testing so the two do not fight over the map.

If something looks wrong, run `/gratis report` and send the saved report file
(settings and diagnostics; it contains no character names). Other diagnostics:
`/gratis text`, `/gratis markers`, `/gratis motion`, `/gratis camera`.
