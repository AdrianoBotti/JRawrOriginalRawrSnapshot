# AGENTS.md — Rawr WotLK 3.3.5a fork (notes for the LLM)

Purpose: persistent context so future sessions start with the high-level understanding
gained during the Wowhead/evowow integration and debugging work. Read this first.

## What this project is

- Rawr 2.3.23 (WoW theorycrafting tool) for WotLK **3.3.5a**, WinForms, C# targeting .NET 2.0.
- Solution `Rawr.sln`; shell app `Rawr.csproj` + core library `Rawr.Base/Rawr.Base.csproj`
  + 22 model class libraries (`Rawr.Cat`, `Rawr.Mage`, ...) + `Rawr3/` (Silverlight/WPF, mostly untouched).
- `bin/Release/Data/` contains the runtime data (ItemCache.xml, BuffCache.xml, icons...) and
  the model DLLs. `bin/Release/Rawr.exe` loads `bin/Release/Data/Rawr.Base.dll`.
- README: this fork was modified to pull items from a WotLK database instead of post-Cata Wowhead.
- User environment: Kubuntu, **Wine 9.0 + wine-mono** (`~/.wine`, DISPLAY=:0, 4480x1440).
  The app is run as `wine bin/Release/Rawr.exe` from the repo root (cwd matters for `Rawrlog.txt`).

## Build & run on Linux (verified recipes)

Rawr.Base (use Roslyn; Mono `mcs` cannot compile this codebase — local functions, etc.):

```bash
FrameworkPathOverride=/usr/lib/mono/2.0-api dotnet msbuild Rawr.Base/Rawr.Base.csproj \
  /p:Configuration=Release /p:Platform=AnyCPU /p:PostBuildEvent= /v:m
# output: bin/Release/Data/Rawr.Base.dll
# NOTE: without /p:PostBuildEvent= the Windows-only sgen2.exe post-build step fails.
# NOTE: platform is "AnyCPU" (no space) or OutputPath is unset.
```

Rawr.exe cannot be built by `dotnet msbuild` (ResGen unsupported on .NET Core MSBuild) nor by
`xbuild` (case-sensitive Linux: csproj references `FormChooseCharacter.designer.cs`, file is
`.Designer.cs`). Working recipe: generate the `.resources` with Mono's resgen, then compile with
Roslyn csc. Resource logical names are `Rawr.<Path.With.Dots>.resources` (42 of them; use the
existing `bin/Release/Rawr.exe` as the authority: `ikdasm bin/Release/Rawr.exe | grep .mresource`).

```bash
# for each EmbeddedResource in Rawr.csproj (e.g. Forms\Options.resx -> Rawr.Forms.Options.resources):
mono /usr/lib/mono/4.5/resgen.exe /useSourcePath /compile "<real.resx>,/tmp/opencode/res/<logical>.resources"
# then csc (dotnet SDK): /noconfig /nostdlib+ /target:winexe /unsafe+ /optimize+ /debug:pdbonly
#   /define:TRACE /win32icon:RawrIcon.ico /out:<dir>/Rawr.exe
#   refs: /usr/lib/mono/2.0-api/{mscorlib,System,System.Configuration,System.Data,System.Drawing,System.Windows.Forms,System.Xml}.dll
#         bin/Release/Data/*.dll   (System.Deployment.dll does not exist in mono 2.0-api; drop it)
#   sources: all <Compile Include> from Rawr.csproj (case-resolve paths), plus /resource: each .resources
# Output MUST be named Rawr.exe (assembly name comes from the file name).
```
Session leftovers: `/tmp/opencode/rawr_build2.rsp`, `/tmp/opencode/rawr_check.rsp`,
`/tmp/opencode/res/*.resources` (regenerate if missing; /tmp may be wiped).

Run: `cd bin/Release && wine Rawr.exe` (started detached via `setsid` if automating).
Kill: `pkill -x Rawr.exe` — do NOT use `pkill -f "Rawr.exe"`, it matches the invoking shell.

## Item data architecture (important)

- `Wowhead.GetItem(site, query, filter)` in `Rawr.Base/Wowhead.cs` is the single item-fetch entry.
  - site `"en"` is mapped to `"wotlk"` (site token is ignored by the current URL templates).
  - Downloads via `WebRequestWrapper.DownloadItemWowhead` → `NetworkSettings.ItemWowheadURI`.
  - Parses XML whose root is `<wowhead>` (Wowhead) **or** `<aowow>` (AoWoW/evowow); payload
    (`<name>`, `<quality id>`, `<icon>`, `<htmlTooltip>`, `<json>`, `<jsonEquip>`) is the same format.
  - json/jsonEquip provide the actual stats; sockets come from the tooltip HTML, set names from `(0/N)`.
- `Item.LoadFromId(...)` → `Wowhead.GetItem` (when `useWowhead`) or `Armory.GetItem` (Armory is dead
  and internally falls back to Wowhead). On success it calls `ItemCache.AddItem`.
- Upgrade list crawling (`ImportItemsFromWowhead`, `LoadUpgradesForSlot`) parses AoWoW HTML
  (`<div class="listview-void">`) so `ItemWowheadUpgradeURI` intentionally stays on evowow.
- Item sources: `json.source`/`sourcemore`; vendor items additionally scrape `DownloadItemHtmlWowhead`
  (also evowow).

## Network URLs / config

`Rawr.Base/app.config` + `Properties/NetworkSettings.settings` + `NetworkSettings.Designer.cs`
(and `bin/Release/Data/Rawr.Base.dll.config` after builds):

- `ItemWowheadURI` = `https://www.wowhead.com/wotlk/?item={1}&xml`
- Fallback (hardcoded in WebRequests.cs) = `https://wotlk.evowow.com/?item={1}&xml` (`WOWHEAD_FALLBACK_ITEM_URI`)
- `ItemWowheadUpgradeURI` = `https://wotlk.evowow.com/?items&filter={1}`
- `QuestWowheadURI` = `https://www.wowhead.com/wotlk/?quest={0}`
- `UserAgent` = modern Chrome string (was Firefox 2.0)
- Library app.config is ignored by `ConfigurationManager` when Rawr.exe is the entry assembly, so the
  **compiled defaults in NetworkSettings.Designer.cs are what actually apply**. `NetworkSettings.Default.Save()`
  is never called, so no persisted user overrides.

## What was changed this session (all uncommitted)

- `Rawr.Base/Properties/NetworkSettings.{settings,Designer.cs}` + `Rawr.Base/app.config`
  → Wowhead WotLK Classic URLs, evowow upgrade/fallback, modern user agent.
- `Rawr.Base/Wowhead.cs`
  → root-agnostic parsing (`DocumentElement.SelectSingleNode("item")`), trimmed names,
    socket parsing works for `<span class="socket-X">` (AoWoW) and `<a class="socket-X">` (Wowhead),
    text socket bonus parser `GetSocketBonusFromText`, weighted report URL → `wowhead.com/wotlk/items?filter=...`,
    root-agnostic token-name lookup.
- `Rawr.Base/WebRequests.cs`
  → `LogWowhead()` (console + Trace + Rawrlog.txt, useful for debugging);
    browser navigation headers (`Accept`, `Accept-Language`, `Upgrade-Insecure-Requests`,
    `Sec-Fetch-Dest/Mode/Site: none`) — **required or Wowhead's AWS WAF returns 403**;
    `ServicePointManager.SecurityProtocol = 3072` (TLS 1.2);
    **hard 20s timeout** implemented by running the download on a dedicated thread and calling
    `HttpWebRequest.Abort()` (Mono's `HttpWebRequest.Timeout` does NOT fire for stalled HTTPS/TLS);
    `Semaphore _requestSlots` bounds concurrent requests to MaxHttpRequests (5) with a 40s wait;
    consecutive-failure circuit breaker (5 fails → skip rest of batch, 60s cooldown, `RecordRequestResult`);
    automatic `_wowheadBlocked` switch to evowow when a Wowhead request returns 403 (`ClearFatalError`).
- `Rawr.Base/ItemUpdater.cs` (rewritten multithreaded mode)
  → previously queued every item onto the .NET ThreadPool; Mono's pool grew to **1206 threads** and
    then stopped completing work → cache update hung forever. Now uses ≤5 dedicated background worker
    threads + global rate limiter, `Monitor`-based queue, `newItems[index] = i` (indexer, so a duplicate
    can't kill a worker), completion always incremented.
- `FormMain.cs`, `FormItemSelection.cs`, `FormUpgradeComparison.cs`
  → `toolStripItemComparison.ShowItemToolTips = false;` in the constructors. Mono's
    `ToolStrip.UpdateToolTip` converts the cursor icon via GDI+ on mouse move and throws
    `GDI+ InvalidParameter` under Wine; disabling item tooltips makes it return before that code.
- `ItemContextualMenu.cs` → "Open in Wowhead" opens `https://www.wowhead.com/wotlk/item=<id>`.
- `FormEnterId.cs` → accepts `wowhead.com/wotlk/item=...`, `?item=...`, and evowow links.
- `FormItemEditor.cs` → name import no longer appends `.0.0.0` (Wowhead resolves plain names; the old
  suffix now returns "Item not found!").
- Rebuilt binaries in the working tree: `bin/Release/Rawr.exe`, `bin/Release/Rawr.pdb`,
  `bin/Release/Data/Rawr.Base.{dll,pdb,dll.config}`.

## Hard-won facts / gotchas

Wowhead:
- Legacy XML endpoint still exists: `https://www.wowhead.com/wotlk/?item=<id>&xml` → 301 →
  `/wotlk/item=<id>?xml` → XML. Plain `curl` with a browser UA alone still gets **403**; the
  `Sec-Fetch-*` navigation headers are what make it pass.
- Wowhead rate-limits/blocks the IP after thousands of requests: the item pages then return 403 while
  `nether.wowhead.com` (tooltip JSON API) and `wotlk.evowow.com` keep working. The WAF block is
  IP-level and temporary; the app now falls back automatically.
- `nether.wowhead.com/wotlk/tooltip/item/<id>` returns JSON (name/quality/icon/tooltip/spells) but
  **no jsonEquip** → not usable as a full item data source.

evowow:
- `https://wotlk.evowow.com/?item=<id>&xml` is an AoWoW database with the same WotLK 3.3.5a data in
  `<aowow>` XML. It is the pre-existing data source of this fork. Occasionally Cloudflare-challenges.
- Same stats as Wowhead WotLK Classic; phase/drop metadata may differ.

Mono/Wine:
- `HttpWebRequest.Timeout`/`ReadWriteTimeout` are unreliable for stalled HTTPS (TLS); enforce timeouts
  yourself. Verified with a local TCP listener that never responds: old code hung forever, new code
  returns in ~20.0s.
- Mono ThreadPool grows slowly (~1 thread / 0.5s) to a max of ~1206; every thread blocked = frozen app.
  The 0.5s GET cadence in logs is thread injection, not request pacing.
- `ToolStrip.UpdateToolTip` GDI+ InvalidParameter (see above).
- `mcs` (xbuild) cannot compile local functions used in `Wowhead.cs`; use Roslyn csc.
- Ptrace is restricted (`yama ptrace_scope=1`), so `gdb -p` cannot attach to the Wine process. Debug via
  `Rawrlog.txt`, console output, `/proc/<pid>/task/*/wchan`, `ss -tnp`, and screenshots
  (`ffmpeg -f x11grab ...`, Python Xlib `warp_pointer`, `xwininfo -root -tree`). `wmctrl`, `xdotool`,
  ImageMagick are NOT installed; `xwd`, `ffmpeg`, python-Xlib and PIL are.

Persistence:
- ItemCache is saved to `bin/Release/Data/ItemCache.xml` only on `ItemCache.Save()`:
  normal app close (`FormMain_FormClosing` → `SaveSettingsAndCaches`) or after batch operations.
  Closing the Item Editor or File→Save (character XML) does NOT save it. Killed/crashed app loses
  items added since the last save.
- Item IDs ≥ 90000 are skipped by cache-update operations.

## Debugging checklist when "it hangs / fails"

1. `tail Rawrlog.txt` — every fetch logs `GET/OK/FAILED/SKIPPED/Loaded/Item not found` plus the URL.
2. `pgrep -x Rawr.exe` → `ls /proc/<pid>/task | wc -l` (thread explosion?).
3. `for t in /proc/<pid>/task/*; do cat $t/wchan; done | sort | uniq -c` (anon_pipe_read = idle pool).
4. `ss -tnp | grep <pid>` (are there real sockets? ESTAB/CLOSE-WAIT to which IPs?).
5. Test endpoints directly with `curl` (use the full browser header set) to separate app bugs from
   server/WAF behaviour.
6. Small Wine harnesses in `/tmp/opencode/*` are the fastest way to test Rawr.Base.dll in isolation
   (`csc /target:exe /reference:Rawr.Base.dll`); use reflection to reach private statics if needed.

## Behaviour you should expect in the app now

- `Update Item Cache (Wowhead)` runs bounded/parallel; on Wowhead 403 it logs
  "Wowhead is blocking this IP (403) - switching to wotlk.evowow.com for this batch" then continues on
  evowow. All items are still fetched/persisted.
- Tooltips over the comparison toolbar buttons no longer crash under Wine.
- "Open in Wowhead" / pasted links point at `wowhead.com/wotlk`.
