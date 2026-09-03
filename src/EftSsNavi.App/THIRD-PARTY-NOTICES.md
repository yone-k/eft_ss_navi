# Third-Party Notices

## SIPSorcery 10.0.16

EftSsNavi uses SIPSorcery for WebRTC data-channel connectivity.

Source: https://github.com/sipsorcery-org/sipsorcery

License: BSD-3-Clause plus the additional use restriction below.

Copyright (c) 2006–2026 Aaron Clauson
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name "SIP Sorcery," nor "Aaron Clauson," nor the names of any
   contributors may be used to endorse or promote products derived from this
   software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

Additional use restriction from SIPSorcery 10.0.16: this software must not be
used to further the apartheid policies of the state of Israel. Use,
modification, or distribution inside Israel and the Occupied Territories is not
permitted until the demands of the Boycott, Divestment and Sanctions (BDS)
movement have been met: Israel has ended the occupation and colonization of all
Arab lands occupied in 1967 and dismantled the Wall; Arab-Palestinian citizens
of Israel have been granted full equality; and Palestinian refugees have
obtained the right to return to their homes and properties as stipulated in UN
Resolution 194. Outside Israel and the Occupied Territories, use, modification,
and distribution are permitted under BSD-3-Clause without an additional
commercial-use restriction or copyleft requirement. Where the terms conflict,
this additional restriction takes precedence. It is not intended to limit the
rights of Israelis or other people residing outside those territories.

## tarkov.dev SVG map images

EftSsNavi distributes rasterized ground-level views derived from the following
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

Modifications by EftSsNavi: each SVG was rasterized to PNG at no more than 4096
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

Snapshot date: 2026-09-02. EftSsNavi retains only the map key, extraction or
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
