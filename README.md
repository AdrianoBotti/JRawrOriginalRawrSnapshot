# JRawr Original Rawr Snapshot

This repository is a development-only snapshot of the WotLK 3.3.5a Rawr reference
implementation used to generate and verify JRawr's C# oracle. It is not the JRawr
application, it is not part of a JRawr release, and it is not intended for end users.

The JRawr application is at <https://github.com/AdrianoBotti/JRawr>.

## What this snapshot is

- Based on the public fork [Magmonix/RAWR_WotLK_3.3.5a](https://github.com/Magmonix/RAWR_WotLK_3.3.5a),
  commit `4f2bdc09e68a24b0efefab8830861ef6b44f8992`.
- Includes the local WotLK Classic integration commit
  `46f190bee32964cc8df5fe853d95b8a343f5aa23` (Wowhead/AoWoW parsing, Wine/Mono timeout and
  thread-pool fixes, rebuilt `Rawr.Base.dll`).
- Adds the WotLK item, buff, enchant, filter and source caches used by the JRawr oracle.

The working app remains the WinForms Rawr application. The snapshot exists so that a clean clone
can rebuild the JRawr oracle tools and reproduce the committed golden JSON. Normal JRawr development
and release builds do not use this repository.

## Use with JRawr

The JRawr oracle scripts expect this tree at `legacy/RAWR_WotLK_3.3.5a`, relative to the JRawr
checkout:

```bash
git clone https://github.com/AdrianoBotti/JRawrOriginalRawrSnapshot.git \
  JRawr/legacy/RAWR_WotLK_3.3.5a
```

Then follow the oracle instructions in JRawr's
[`oracle/README.md`](https://github.com/AdrianoBotti/JRawr/blob/master/oracle/README.md). The tool
scripts also accept `RAWR_LEGACY_DIR` when the checkout lives elsewhere. The preferred setup is
JRawr's `tools/setup-legacy.sh`, which clones this repository at the `jrawr-oracle-1` tag into the
expected path.

## Requirements

- Linux with Mono (`mono`, `mcs`) for the C# oracle tools.
- Wine is only needed to run the WinForms application itself, not to build the JRawr oracle tools.
- The prebuilt DLLs under `bin/Release/Data` are included so the oracle does not need to rebuild
  the original WinForms application. See the upstream README in `README.upstream.md`.

## Provenance and licensing

The original Rawr project is Copyright 2008 Chadd Nervig, "HugeHoss", "Toskk" and the Rawr
Developers, licensed under Apache License 2.0. The WotLK fork was maintained by Magmonix; the
WotLK Classic integration and Wine/Mono fixes in this snapshot were made by Adriano Botti.

JRawr does not redistribute this repository as part of its runtime release. World of Warcraft
game data, names, icons and trademarks remain the property of Blizzard Entertainment. This project
is not affiliated with or endorsed by Blizzard Entertainment. See [NOTICE](NOTICE).

## Status

This is an archived development snapshot, not a supported Rawr distribution. It is kept only to
make JRawr's numerical verification reproducible.
