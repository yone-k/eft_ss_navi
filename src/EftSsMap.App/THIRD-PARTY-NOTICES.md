# Third-Party Notices

## tarkov.dev SVG map images

EftSsMap distributes rasterized ground-level views derived from the following
files in the-hideout/tarkov-dev-svg-maps:

- `Customs.svg` as `customs-tarkov-dev.png`
- `Factory.svg` as `factory-tarkov-dev.png`
- `GroundZero.svg` as `ground-zero-tarkov-dev.png`
- `Interchange.svg` as `interchange-ground-tarkov-dev.png`
- `Lighthouse.svg` as `lighthouse-tarkov-dev.png`
- `Reserve.svg` as `reserve-tarkov-dev.png`
- `Shoreline.svg` as `shoreline-tarkov-dev.png`
- `StreetsOfTarkov.svg` as `streets-of-tarkov-tarkov-dev.png`
- `Woods.svg` as `woods-tarkov-dev.png`

Source repository and contributors:
https://github.com/the-hideout/tarkov-dev-svg-maps

Source revision: `5a8b6115d1c0cf56f2ebaac1a96fa5ae3074d178`

License: Creative Commons Attribution-NonCommercial-ShareAlike 4.0
International (CC BY-NC-SA 4.0)
https://creativecommons.org/licenses/by-nc-sa/4.0/

Modifications by EftSsMap: each SVG was rasterized to PNG at no more than 4096
pixels on its longest projected side, sized to its tarkov.dev coordinate bounds.
For SVGs containing multiple floors, non-ground floor groups were hidden. These
derived PNG files are offered under CC BY-NC-SA 4.0.

The source repository additionally prohibits using these assets in software
designed to facilitate cheating or gaining an unfair advantage in Escape from
Tarkov, including in-game radar/ESP overlays, cheat-client maps, automation
scripts, and pixel bots. See its `LICENSE.md` for the complete restriction and
revocation clause.

## tarkov.dev coordinate metadata

The bundled map catalog derives its coordinate transformations, rotations, SVG
bounds, and map-to-SVG associations from `src/data/maps.json` in
the-hideout/tarkov-dev.

Source:
https://github.com/the-hideout/tarkov-dev/blob/560a844649b92aba1cdd463271e21e772e4e8df9/src/data/maps.json

PMC/shared/SCAV extraction positions, transit positions, and PMC spawn
positions in `markers.json` are a filtered snapshot of the public tarkov.dev
map data and English translation endpoints:
https://json.tarkov.dev/regular/maps
https://json.tarkov.dev/regular/maps_en

Snapshot date: 2026-09-02. EftSsMap retains only the map key, extraction or
transit name, faction, and X/Z position needed for offline marker display. The
PMC spawn filter follows tarkov.dev's own map implementation: category
`player` and side `pmc` or `all`. Nearby PMC spawn candidates are grouped into
15-meter connected clusters for display.

API information:
https://tarkov.dev/api/

The `tarkov-dev` source file cited above is distributed under the following
MIT license:

MIT License

Copyright (c) 2019 Oskar Risberg

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Source-only maps not included in release artifacts

The repository retains `labyrinth-re3mr.png` and `terminal-re3mr.jpg` for
possible future support, but the application project explicitly excludes them
from build and release output. Both originate from the Escape from Tarkov Wiki
and are credited to RE3MR:

- https://escapefromtarkov.fandom.com/wiki/File:The_Labyrinth_Map_by_re3mr.png
- https://escapefromtarkov.fandom.com/wiki/File:Terminal2DMapByRE3MR.jpg

Wiki copyright policy:
https://escapefromtarkov.fandom.com/wiki/Escape_from_Tarkov_Wiki:Copyrights

Escape from Tarkov is a trademark of Battlestate Games. This project is not
affiliated with or endorsed by Battlestate Games.
