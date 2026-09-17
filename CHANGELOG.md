## v0.7.90.0 — Install With Extras (UE4SS)

- Direct live feedback: "we should add that as part of the install, option to install just the
  server without mods and then the option of installing it with the extras."
- The wizard's Install step now offers two buttons: "Install Palworld Dedicated Server" (unchanged,
  server only) and a new "Install with Extras (+ UE4SS)", which installs the server and then
  automatically installs the newest stable UE4SS release via the exact same Preview/Apply flow the
  UE4SS page's own manual picker already uses.
- Palworld Save Tools and PIM/Oodle Decoder are not part of "extras" -- confirmed neither has any
  install capability anywhere in the app yet (both explicitly "BACKEND REQUIRED").
- Touches `MystTiq.Desktop` only; no backend changes.

Full detail: [`release-notes/v0.7.90.0.md`](release-notes/v0.7.90.0.md),
[`docs/architecture/v0.7.90.0-install-with-extras.md`](docs/architecture/v0.7.90.0-install-with-extras.md).

## v0.7.89.0 — Second-Server Lifecycle Fixes and Wizard Polish

- Direct live feedback while driving a genuine second local server through setup and Start surfaced
  a cluster of real bugs, most from code that treated "the literal default profile" as a stand-in
  for "a local server" -- breaking once a genuine second local server exists.
- **"Server did not start, no error message"** -- `EnsureManagementConnectionForLifecycleAsync`
  silently returned false for any non-default local profile. Now checks the actual loopback address.
- **Tab labeled "Remote" for a genuinely local server** -- same root cause, same fix
  (`TabSession.ConnectionKindText`).
- **Dashboard showed a different server's world data** -- confirmed live via `GET /world/explorer`
  across profiles: a brand-new server's "not ready yet" state left the *previous* tab's Active World
  ID/player-save count/size on screen. `ApplyDashboardSupport`'s stale branch now clears them.
- **"How is this overall progress at 100% when there are missing items?"** -- the bar only ever
  tracked the last operation's own completion. Relabeled to "OPERATION PROGRESS."
- **Install step didn't auto-refresh or show progress detail** -- now auto-refreshes the Server
  Environment checklist after install and shows SteamCMD's own captured output.
- **MODs step required a manual scan** -- now auto-scans Steam Workshop on arrival, matching MOD
  Library's own established auto-scan pattern.
- **MODs step had no local-ZIP install option** -- added a distinctly-colored "Install MOD ZIP…"
  button reusing MOD Library's own install path.
- Touches `MystTiq.Desktop` only; no backend changes. Live-verified the backend itself was never
  broken -- a direct `POST /server/start` against the new profile succeeded and produced a real
  running `PalServer.exe`.

Full detail: [`release-notes/v0.7.89.0.md`](release-notes/v0.7.89.0.md),
[`docs/architecture/v0.7.89.0-second-server-lifecycle-and-wizard-polish.md`](docs/architecture/v0.7.89.0-second-server-lifecycle-and-wizard-polish.md).

## v0.7.88.0 — New Server Wizard and Dashboard Bug Fixes

- Direct live feedback with screenshots against a running v0.7.87.0 build surfaced five real bugs.
- **Wizard jumped straight to Confirm & Finish** on every "+ New Server" attempt when a default
  server was already running -- a short-circuit meant only for the Connect flow was wrongly also
  firing for New Server Setup, since step 1 always connects to the shared default API before any new
  profile exists. Scoped to Connect only.
- **Install Directory step had no field** to see or change the target path. Added a real editable
  path `TextBox` + folder-browse button.
- **Game Port now highlighted** on Server Identity & Ports, with a permanent note explaining it only
  needs to change when running multiple servers on the same machine at once.
- **Clone source list was missing "Local MystTiq"** -- it reused the "+" menu's open-tab-exclusion
  filter, which has no bearing on clone-source validity. Now lists every saved profile.
- **Dashboard could get stuck on "Starting / Not Ready"** with Start greyed out after the server had
  genuinely stopped. Root cause, confirmed live: the backend correctly reports Stopped but also (by
  design) includes the previous run's PID for reference; the client was treating PID presence alone
  as "alive." New `ServerStatusDto.IsProcessLive` checks the actual reported phase instead.
- Touches `MystTiq.Desktop` only; no backend changes.

Full detail: [`release-notes/v0.7.88.0.md`](release-notes/v0.7.88.0.md),
[`docs/architecture/v0.7.88.0-new-server-wizard-and-dashboard-bug-fixes.md`](docs/architecture/v0.7.88.0-new-server-wizard-and-dashboard-bug-fixes.md).

## v0.7.87.0 — Configuration: Advanced Dirty Highlighting, Save/Discard Prompt, Default Tab

- Direct live feedback: "When i change items on this page they should highlight so that we know
  what has been changed in both simple and advanced settings. If I do not save the changes and click
  away it should ask if I want to save changes or discard. when i load up configuration it should
  always start with the servers settings."
- **Advanced Settings rows now highlight when edited** (new `Border.rowDirty` style + `TextBox.dirty`
  on the Active Value column), matching Simple Settings' existing amber unsaved-change highlight from
  v0.7.82.0. Distinct from and coexists with the existing "differs from Palworld's default" highlight.
- **Navigating away from Configuration with unsaved changes now prompts** Save Changes / Discard /
  Cancel via a new `ConfirmSaveDiscardDialog`, instead of silently discarding them. The guard lives
  once in `Navigate()`, the single funnel every nav call site already goes through.
- **Configuration always opens on Server Settings** (Simple view) rather than remembering Advanced
  from an earlier visit in the same session.
- Touches `MystTiq.Desktop` only; no backend changes.

Full detail: [`release-notes/v0.7.87.0.md`](release-notes/v0.7.87.0.md),
[`docs/architecture/v0.7.87.0-configuration-dirty-highlighting-and-save-prompt.md`](docs/architecture/v0.7.87.0-configuration-dirty-highlighting-and-save-prompt.md).

## v0.7.86.0 — New Server Wizard: MODs Step

- The last item scoped out of v0.7.81.0's original wizard rework: "embed the existing MOD Library
  Workshop-scan machinery as a wizard step... same commands, new step placement, no new backend."
- **New MODs step (7 of 8)**, install-wizard only, New World/Import paths only (Clone already
  carries over the source server's MODs). Reuses `ScanWorkshopModsCommand`/`WorkshopItems`/
  `ImportSelectedWorkshopModCommand` verbatim — both already correctly used
  `BuildProfileFromEditor`, so no wizard-usability bug fix was needed here.
- Entirely optional; Next advances to Confirm whether or not anything was imported.
- Confirm & Finish renumbered from step 7 to step 8 across the whole wizard (Clone's own
  jump-to-confirm and the "server already running" short-circuit both updated); Connect's own step
  numbers are untouched.
- Touches `MystTiq.Desktop` only; no configuration changes.

Full detail: [`release-notes/v0.7.86.0.md`](release-notes/v0.7.86.0.md),
[`docs/architecture/v0.7.86.0-new-server-wizard-mods-step.md`](docs/architecture/v0.7.86.0-new-server-wizard-mods-step.md).

## v0.7.85.0 — Import a World, Phase 2

- Closes out the honest interim placeholder scoped from v0.7.81.0: `ApplyCoreAsync` hard-requires an
  already-active world, a real mismatch with a brand-new install that has no world to replace.
- **Verified live, not assumed**: starting a genuinely fresh install for the very first time makes
  PalServer.exe generate its own real world save (`SaveGames/0/<32-hex-id>/Level.sav`) within a few
  minutes — confirmed by direct filesystem inspection. That means the already-existing, already-proven
  World Transactions "world-import" flow works completely unmodified once a fresh install has been
  started once. No new backend method, route, or DTO needed.
- **New: "Prepare for Import"** in the wizard's Install step (Import path only) — starts the server
  once, polls until a world exists, stops it, then embeds the same Analyze/Apply controls the World
  Transactions page already uses. World Settings choice (step 4) is skipped for Import, since a
  preset/customize choice doesn't apply to a placeholder world about to be entirely replaced.
- **Fixed a real bug found while wiring this in**: `AnalyzeWorldArchiveAsync`/`ApplyWorldTransactionAsync`
  hard-required `SelectedProfile` directly and silently no-op'd during the wizard (`SelectedProfile`
  is always null there) — the same bug class as `RefreshEnvironmentAsync`'s v0.7.81.0 fix. Both now
  use `BuildProfileFromEditor`, the established pattern.
- **Live end-to-end tested against a real server**: started a genuinely fresh install, confirmed real
  world generation, built a real test archive from that world's own save data, and ran it through the
  actual Analyze → Apply routes — completed successfully with a real safety backup created.
- Touches `MystTiq.Desktop` only; no configuration changes.

Full detail: [`release-notes/v0.7.85.0.md`](release-notes/v0.7.85.0.md),
[`docs/architecture/v0.7.85.0-import-world-phase-2.md`](docs/architecture/v0.7.85.0-import-world-phase-2.md).

## v0.7.84.0 — Fleet Clone World Workflow

- Direct live feedback (built earlier this session, explicitly tabled at the time: "table this for a
  future update"): "the clone world option in fleet should be a workflow with drop down options to
  clarify what it is doing," followed by "it should indicate better what server is being cloned
  (server name - description)."
- **Clone World on the Fleet page is now a labeled workflow**: SOURCE (this tab's connection, name
  plus `BaseAddress`), NEW PROFILE (ID/name), and PORT OFFSET (dropdown with an explanation),
  instead of one flat unlabeled card.
- **New: "Restart MystTiq Now" button** after a successful clone, reusing `RestartOwnedSidecarAsync`
  (built in v0.7.83.0 for the wizard's own second-server flow) to close the same "needs a restart to
  come online" gap here — offered as an explicit button rather than fired automatically, since this
  page's connection is already actively in use.
- Touches `MystTiq.Desktop` only; no configuration changes.

Full detail: [`release-notes/v0.7.84.0.md`](release-notes/v0.7.84.0.md),
[`docs/architecture/v0.7.84.0-fleet-clone-world-workflow.md`](docs/architecture/v0.7.84.0-fleet-clone-world-workflow.md).

## v0.7.83.0 — New Server Wizard: Reordered Steps & Genuine Second-Server Support

- Direct live feedback against the v0.7.81.0 wizard: remove the standalone "+" → "Clone a Server"
  shortcut; reorder to World Source → Server Identity & Ports → World Settings → Install Directory →
  Download & Install, with the directory "the one we typically use unless it already exists, then
  the same root but a different name based on the server's name."
- **Reordered wizard steps**: Identity & Ports and the World Settings preset choice are now
  collected right after World Source, but only actually applied automatically right after Install
  produces a real `PalWorldSettings.ini` — `CreateDefault` genuinely requires that file to already
  exist. "Customize" defers to the existing Configuration page after Finish instead of faking
  pre-install slider values.
- **New: genuine second-server support** — "Local MystTiq" permanently owns the default ServerId
  (confirmed live: reusing it collided with the existing duplicate-connection guard), so every
  completion now registers a real, separate fleet profile — at the typical root if free, or a
  derived sibling directory if occupied — then automatically restarts the local MystTiq sidecar
  (new `RestartOwnedSidecarAsync`) and reconnects. Clone gets the same restart-and-reconnect
  treatment, closing a previously-undisclosed identical gap in its own flow.
- **Removed**: the "+" menu's "Clone a Server" shortcut (it only ever navigated to Fleet's Clone
  World card). Clone is now reached solely through the wizard's own World Source step.
- **Set Up New Server auto-connects** instead of requiring a manual click, and skips straight to
  World Source once ready, instead of sitting on a screen that read like "connect to an existing
  service."
- **Fixed real bugs found live**: `BuildProfileFromEditor`'s duplicate-connection guard blocked the
  wizard's own Connect entirely; Step 1's header silently stayed blank (a 3-way `IsVisible`-toggled
  row that never updated, replaced with one `TextBlock` bound to a computed string); Confirm's
  summary showed the generic "New Server" placeholder instead of the real configured name
  (`SetupServerName` now syncs into `ProfileName`); and a second, deeper round of the Alert Center
  disk-space-prediction crash fix (explicit `double.IsFinite` guards plus a last-resort catch).
- Touches `MystTiq.Desktop` (and `MystTiq.HeadlessHost` for the Alert Center round-2 fix); no
  configuration schema changes.

Full detail: [`release-notes/v0.7.83.0.md`](release-notes/v0.7.83.0.md),
[`docs/architecture/v0.7.83.0-new-server-wizard-second-server-support.md`](docs/architecture/v0.7.83.0-new-server-wizard-second-server-support.md).

## v0.7.82.0 — Update Center Actionability & Server Setup Table Resize

- Direct live feedback: "the update center where it says update available should allow us to click
  on it to update. we have a couple that say unknown, we need to do better and have a way to
  check." Follow-up: "the server environment should expand or shrink to fill the screen."
- **New: pip's "Update" button** — `HeadlessComponentUpdateService.UpdatePipAsync` runs
  `python -m pip install --upgrade pip` for real via a new `POST /update-center/components/pip/update`
  route, mutation-gated like every other real update action in this app. `RunProcessAsync` gained
  an optional longer timeout for this specific real install, unchanged 8s default everywhere else.
- **New: "Open" links for Unknown rows** — Python Runtime, Visual C++ Runtime, Microsoft C++ Build
  Tools, and PIM/Oodle Decoder each get a link to the real official page to check manually, since
  none has a reliable unattended "latest version" feed this app could safely automate (already
  disclosed). New `ComponentVersionDto.SourceUrl`, keyed by `Component` since two of these rows
  share an identical `Source` label but need different URLs.
- **Fixed**: Server Setup's environment table had a fixed `MaxHeight="405"` regardless of actual
  window size. New `ServerSetupTableMaxHeight`, recomputed on every window resize (same established
  pattern as `UpdateTabStripWidth`/`UpdateRibbonWidth`).
- **Fixed a real crash**: Alert Center's disk-space-exhaustion prediction could overflow
  `DateTimeOffset.AddDays` when growth-per-day was a near-zero positive fraction, flooding Activity
  & Audit with a repeating warning roughly once a minute. Capped at `MaxProjectableDays` (100 years).
- **Configuration page reordered**: direct live feedback ("Server Identity should be moved to the
  top... the second screenshot items should be moved just above world settings... the server
  identity text needs some spacing"). Server Identity now sits first with more breathing room; the
  Simple/Advanced toggle, QoL preset, and search bar moved to just above World Settings.
- **New: unsaved-change highlighting** ("anything that has changed from the save[d] preset should
  highlight in Amber or Green") — Server Identity, Network toggles/settings, and World Settings
  sliders all highlight amber while dirty, via new `Border.statuscard.dirty`/`TextBox.dirty` styles.
- **Fixed a real Dashboard bug**: "why does it say ... PalServer process is active. The server is
  stopped and not ready, but the only option i have is to stop?" `ServerState`/`DashboardHealthText`
  now show "Starting / Not Ready" instead of a contradictory "Stopped" when a process is detected but
  not yet confirmed ready.
- **Fixed**: the Server Identity "🎲 Generate" and "Save As Preset" buttons stayed permanently
  disabled after the first Configuration load every session — their `CanExecuteChanged` was only
  ever re-raised from a property setter that fires mid-load, before `IsBusy` returns to false.
- Touches `MystTiq.Desktop` and `MystTiq.HeadlessHost`; no configuration changes.

Full detail: [`release-notes/v0.7.82.0.md`](release-notes/v0.7.82.0.md),
[`docs/architecture/v0.7.82.0-update-center-actionability.md`](docs/architecture/v0.7.82.0-update-center-actionability.md).

## v0.7.81.0 — "Set Up New Server" Wizard Rework

- Direct live feedback: "When i setup a new server it should always be a local install. It should
  not be trying to connect to a previously installed version as we already have an option to
  connect to a local server or remote server from the plus... It should have the option to clone
  an existing server, setup a new world, import a world, and then move onto the specific
  settings... If another service is detected it should flag that when selecting the ports."
- **Rebuilt**: "Set Up New Server" was reusing the exact same wizard as "Connect to Local/Remote
  Server," landing on an identical Local/Remote choice screen and "find a service" step regardless
  of intent (a gap already disclosed in the code's own comments). Now always local, skips that
  choice entirely, and walks through a real World Source step (Clone an Existing Server / Set Up a
  New World / Import a World) before Settings/Confirm.
- **New: Clone an Existing Server, inline in the wizard** — pick any saved profile as the source,
  choose a port offset, no new backend needed (`CloneWorldAsync` already took an explicit source
  profile; `CredentialStore.TryLoad` already resolves a token for any saved profile by Id).
- **New: read-only environment checklist during Install**, auto-loaded, surfacing SteamCMD/
  dependencies/UE4SS status. Found and fixed a real bug along the way: `RefreshEnvironmentAsync`
  had a hard `SelectedProfile is null` guard that made it unreachable during the entire wizard.
- **New: starter preset picker** (Vanilla/Balanced QoL/Relaxed QoL/custom) in the Settings step,
  chaining the existing Load → Apply → Save sequence the Configuration page already had.
- Port selection with live conflict detection was already built (`CheckSetupPortAsync`) — kept in
  the renumbered step order, no new work needed there.
- Import a World stays an honest interim placeholder this version — the existing world-import
  backend hard-requires an already-active world to replace, a real mismatch with a brand-new
  install that has none yet; a proper fix is scoped as a separate future version.
- **New**: `scripts/Test-v0.7.81.0-RouteSmoke.ps1`, live end-to-end coverage of the whole
  flow (connect, port-conflict detection, install-status, create-default-settings, starter-preset
  apply, clone) against a genuinely fresh, empty server root.
- `MystTiq.Desktop` only, no backend or configuration changes.

Full detail: [`release-notes/v0.7.81.0.md`](release-notes/v0.7.81.0.md),
[`docs/architecture/v0.7.81.0-new-server-wizard-rework.md`](docs/architecture/v0.7.81.0-new-server-wizard-rework.md).

## v0.7.80.0 — Server Setup Status Pill Centering & Glass Gradient

- Direct live feedback on the Server Setup page: "the Ready should be in the middle of the green.
  can we also add a gradient glassy effect."
- **Fixed**: the per-row STATUS pill (Ready/Disabled/Missing) relied on default Border/TextBlock
  layout behavior instead of explicit alignment, so its text wasn't reliably centered within the
  badge. Now explicit `HorizontalAlignment="Stretch"` on the pill plus
  `HorizontalAlignment`/`VerticalAlignment`/`TextAlignment="Center"` on its label.
- **Redesigned**: flat `GreenBrush`/`AmberBrush`/`RedBrush` fills replaced with this app's existing
  glass-gradient family (`SuccessGlassGradient`, `DangerGlassGradient`, and a new
  `WarningGlassGradient` added for amber, matching the same alpha-stop structure), plus a thin
  matching accent border for definition — the same glassy look already used on ribbon buttons
  elsewhere in the app.
- `MystTiq.Desktop` only, no backend or configuration changes.

Full detail: [`release-notes/v0.7.80.0.md`](release-notes/v0.7.80.0.md),
[`docs/architecture/v0.7.80.0-server-setup-status-pill-glass.md`](docs/architecture/v0.7.80.0-server-setup-status-pill-glass.md).

## v0.7.79.0 — Server Doctor Compact, Color-Coded Checks

- Direct live feedback on the Server Doctor page: "these items listed should be compacted and make
  use of the spacing a bit more. Perhaps Pass can be highlighted in green or red."
- **Redesigned**: each diagnostic finding is now a denser single row (colored state badge,
  component/category, evidence, actions) instead of a tall stacked card. A redundant line
  (Recommendation duplicating Evidence word-for-word, common on passing checks) is now hidden via a
  new `ShowRecommendation` computed property.
- **Color-coded**: PASS/WARNING/FAIL now show as a small colored badge (green/amber/red) instead of
  plain bold text, via new `IsPass`/`IsWarning`/`IsFail`/`IsOtherState` computed properties on
  `DiagnosticFindingDto`.
- **Follow-up**: added a subtle text-shadow style (`TextBlock.badgeText`) to keep the badge label
  legible against the bright green PASS background, per direct request, without touching the
  shared `GreenBrush` resource used elsewhere.
- `MystTiq.Desktop` only, no backend or configuration changes.

Full detail: [`release-notes/v0.7.79.0.md`](release-notes/v0.7.79.0.md),
[`docs/architecture/v0.7.79.0-server-doctor-compact-checks.md`](docs/architecture/v0.7.79.0-server-doctor-compact-checks.md).

## v0.7.78.0 — MOD Pages UX Pass

- Started from direct live feedback on the MOD Library page ("there are a number of buttons for the
  installed mods and the whole page just looks odd. Also the local steam Mods should load
  automatically") and grew through several rounds of live, iterative feedback into a full pass
  across MOD Library, MOD Dashboard, and the UE4SS page.
- **MOD Library**: local Steam Workshop scan now runs automatically on first visit each tab.
  Install Validated ZIP and Available Local Steam Workshop Mods moved side by side into their own
  row at the top; Installed MODs (2/3 width) and MOD DETAILS (1/3 width) sit below. The old 9-button
  row is gone: all-MODs actions (Enable All, Disable All, Repair, Safe-Start Diagnostic) moved to a
  new ribbon "MOD Maintenance" group, and per-mod actions (Enable/Disable/Rollback/Repair/Delete
  Selected) are now a right-click context menu on the list, matching the established Bases/Guilds/
  Players pattern.
- **Install Validated ZIP simplified**: removed the manual "Package name (optional)" box (the
  package name now always comes from the ZIP's own filename) and the two permanently-disabled
  "BACKEND REQUIRED" placeholder buttons. Added real drag-and-drop: drop a `.zip` directly onto the
  card to install it, using Avalonia 11.3's current `DataTransfer` API.
- **Per-mod version and update info**: `HeadlessModItem` gained `UpdateAvailable`/`UpdateHint`/
  `InstalledVersion`, computed once per mod during the regular inventory scan by reusing the
  existing `CheckModUpdateAsync` comparison and reading each installed mod's own `Info.json`
  directly from its install folder. Shown as a version line + "UPDATE AVAILABLE" badge per row on
  the Library list and in the MOD DETAILS panel.
- **MOD Dashboard redesigned** to feel distinct from MOD Library (direct follow-up: "help with the
  MOD dashboard... so it doesn't feel like it is essentially the same as the MOD library"): a new
  color-coded hero health banner, recolored status cards (green/cyan/amber/red by meaning instead of
  one flat look), and a deliberately lighter read-only list — renamed "MOD Overview" — with no
  install path, version, or update badge, since that detail lives in Library instead.
- **Fixed**: `Border.statuscard` never had a `Padding` setter at all (unlike `Border.card`), so text
  sat flush against every statuscard's border app-wide — found on the UE4SS page, fixed at the
  shared style so every page benefits.
- **Fixed**: UE4SS Runtime Health used to revert to "Unverified" any time its one-time evidence
  source disappeared (server stopped, log rotated), even on an install that had already run
  cleanly. A new small persisted marker now lets a confirmed-good run keep being reported
  ("Confirmed — ran without issue") instead of losing that signal.
- Touches `MystTiq.Desktop` and `MystTiq.HeadlessHost`; no configuration changes.

Full detail: [`release-notes/v0.7.78.0.md`](release-notes/v0.7.78.0.md),
[`docs/architecture/v0.7.78.0-mod-pages-ux-pass.md`](docs/architecture/v0.7.78.0-mod-pages-ux-pass.md).

## v0.7.77.0 — MOD/Workshop Detection Fixes, UE4SS Version via Hash, Per-MOD Repair

- Direct live investigation into three real observations: Steam Workshop items showing "NOT
  INSTALLED" when believed installed, UE4SS's own page showing "version metadata unavailable"
  despite an ask for a real version signal, and a discrepancy between 6 currently-detected MODs
  and a remembered "8" from v0.2.16.4. Investigated the real file system directly before writing
  any code: found real Workshop content on a second Steam library, found QualityOfLife's `.pak`
  files manually quarantined (`.quarantined-slow-start-cause`, dated Aug 9, never restored) from a
  past troubleshooting session, found PalSchema only in inert staging folders never actually
  copied into the active mods root, and confirmed via direct SHA-256 comparison that the real
  active `UE4SS.dll` is byte-identical to the one bundled in the "UE4SS Experimental (Palworld)"
  Workshop item.
- **Fixed**: `DescribeWorkshopItem` always checked a Workshop item's install status against the
  MOD inventory — correct for real mods, but wrong for a Workshop item whose manifest declares
  itself as the UE4SS runtime (the mod inventory never contains the runtime by definition). Now
  checks whether UE4SS is actually installed instead for that case.
- **Fixed**: UE4SS version now resolved via local-Workshop-copy hash match when nothing else
  identifies it — reports a real version string (`"2281fa31 (matched via local Workshop item
  3625223587, SHA-256 identical)"`) instead of "version metadata unavailable." Found a second real
  bug while wiring this up: two different `UE4SS.dll` files existed side by side (a stale
  legacy-location one from Feb 2024, the real active modern one from Sept 2026) and the naive
  candidate order was hashing the wrong one. New `ResolveActiveUe4ssDllPath()` mirrors
  `ResolveUe4ss()`'s own modern-vs-legacy preference instead of guessing.
- **Real data restored, direct user request**: PalSchema and QualityOfLife actually imported into
  the real active mods folder via the already-existing `ImportWorkshopItemAsync` — `mods.txt` now
  lists 8 mods, matching what was remembered from v0.2.16.4.
- **New: per-MOD Repair / Re-install** (`HeadlessModManagementService.RepairModAsync`) — direct
  request: "if a MOD is incomplete there should be an option for repair/re-install." Distinct from
  Update (only offers when Steam's local copy is *newer*) and the existing global Repair button
  (only fixes legacy `enabled.txt` overrides, never a MOD's own files). Finds a matching local
  Workshop source and does a clean delete-then-reimport, reusing the existing delete snapshot for
  a safety net — found live that a naive straight re-import fails outright since the install
  method correctly refuses to overwrite an existing MOD folder by design. Fails honestly, without
  touching anything, when no local Workshop source is known. New route:
  `POST /mods/{type}/{package}/repair`; new "Repair / Re-install Selected" button on the MOD
  Library page.
- Everything live-verified directly against the real production install — no isolated clone
  needed, since none of this touches player save data.

Full detail: [`release-notes/v0.7.77.0.md`](release-notes/v0.7.77.0.md),
[`docs/architecture/v0.7.77.0-mod-workshop-fixes-and-repair.md`](docs/architecture/v0.7.77.0-mod-workshop-fixes-and-repair.md).

## v0.7.76.0 — Base/Guild Right-Click Workflow

- Direct follow-up completing the deferred half of the previous version's request. **Bases page**
  right-click menu: "Copy Base Info" (full details to clipboard), "Transfer to Guild…" (a
  populated dropdown of real guilds instead of typing a 32-hex ID), "Wipe Base Completely…".
  **Guilds page** right-click menu: "Transfer Leadership To…", "Add Player…", "Remove Broken
  Member…", "Claim Orphaned Guild…" — the same four operation types the existing card's own
  dropdown already supports, each with a real player picker instead of a hand-typed ID.
- Every flow follows Preview → real server-reported findings → mandatory safety-backup notice →
  confirm → Apply, matching the exact workflow shape requested.
- **No new server-side mutation code**: reuses `HeadlessBaseOwnershipService`/
  `HeadlessGuildOwnershipService` entirely unchanged — both were already real, working, Preview →
  Safety Backup → Apply operations, previously reachable only through in-page cards requiring
  hand-typed IDs. New Desktop-side orchestration only: `SelectGuildDialog` (mirrors v0.7.75.0's
  `SelectPlayerDialog`), one reusable `ConfirmOperationDialog` instead of a bespoke dialog per
  operation, and parallel `MainWindowViewModel` Preview/Apply method pairs that write into the
  exact same status-text properties the in-page cards already display, so both entry points stay
  in sync.

Full detail: [`release-notes/v0.7.76.0.md`](release-notes/v0.7.76.0.md),
[`docs/architecture/v0.7.76.0-base-guild-right-click-workflow.md`](docs/architecture/v0.7.76.0-base-guild-right-click-workflow.md).

## v0.7.75.0 — Delete Player Completely / Copy Player

- **New: Delete Player Completely** (`HeadlessPlayerDeletionService`) — permanently deletes a
  player's save file, then as a separate follow-up operation reuses the already-proven Remove
  Broken Member operation to clean up their now-dangling guild reference, and clears their
  registry history. Same Preview → Safety Backup → Apply discipline as every other destructive
  operation in this app.
- **New: Copy Player** (`HeadlessPlayerCopyService`) — clones a player's inventory, unlocked
  recipes, records, skins, and quest progress onto another player's own save, while the
  destination keeps its own identity, position, session history, and pal-party/storage
  references. Real investigation before writing any code: decoded a real production player's
  `.sav` file read-only via the existing generic PlM/Oodle converter (first time this codebase has
  decoded a per-player save, not just `Level.sav`) and found the exact field split to copy versus
  preserve. Character level/stats (a separate `Level.sav` decode target) and pal-party contents
  are deliberately deferred, disclosed rather than silently dropped.
- **Real bug found and fixed during live testing on an isolated clone of the real production
  save**: the deletion's guild-cleanup follow-up initially failed with a lock conflict —
  `OperationCoordinator.Complete()` doesn't release the operation's resource lock, only
  `Dispose()` does, and the follow-up call ran before that disposal happened. Fixed by disposing
  explicitly right after `Complete()`. Re-verified after the fix: both the deletion and the
  follow-up guild cleanup were independently confirmed correct by decoding the raw save a second,
  separate way outside the app entirely — not just trusting the app's own success response.
- **Root-caused a separately reported "right-click looks broken" issue**: Kick/Ban were always
  correctly wired to a real backend call — they were just correctly disabled (no online player /
  no server running) with zero visible disabled styling, since no `MenuItem` style existed
  anywhere in this app. Added a real `MenuItem:disabled` style matching the existing
  `Button:disabled` convention.
- Desktop: Players page's right-click menu gains "Copy Player Data From…" (a populated
  player-picker dropdown via the new, genuinely reusable `SelectPlayerDialog`, not a hand-typed
  ID) and "Delete Player Completely…", both showing real server-reported preview findings before
  confirming.
- **Deferred, not forgotten**: Base/Guild right-click convenience (jump to the already-working
  transfer/wipe cards; populated target dropdowns) was requested in the same conversation and is
  scoped for its own follow-up rather than rushed in here — the underlying capability already
  fully works today via the existing cards, so this is lower urgency than Player Delete/Copy,
  which had zero existing UI.

Full detail: [`release-notes/v0.7.75.0.md`](release-notes/v0.7.75.0.md),
[`docs/architecture/v0.7.75.0-delete-and-copy-player.md`](docs/architecture/v0.7.75.0-delete-and-copy-player.md).

## v0.7.74.0 — Close-Dialog Exit Shortcuts & Tab-Restore Exception Isolation

- Direct live report after clicking through the v0.7.73.0 build: the "Server is running"
  close-confirm dialog only offered Cancel/Minimize to Tray, with a text hint pointing at the tray
  icon's own Safe Exit/Force Exit for anyone who actually wanted to stop the server — an extra,
  avoidable round-trip. `App.axaml.cs`'s `SafeExit_OnClick`/`ForceExit_OnClick` were split into
  public `SafeExitAsync()`/`ForceExitAsync()` (the tray menu's own handlers now just call these);
  `ConfirmMinimizeToTrayDialog` gained two more buttons wired to two new
  `ConfirmMinimizeToTrayResult` values, handled by `MainWindow_Closing` calling the same public
  methods. Both paths now run identical shutdown logic, nothing duplicated.
- **Real bug found and fixed, also reported live**: only 1 of 3 saved tabs reopened after a
  relaunch. Confirmed the saved data itself was intact — `%APPDATA%\MystTiq\open-tabs.json` had all
  3 profile IDs, `connections.json` had all 3 matching profiles. Root cause: the startup sequence
  ran `InitializeLocalDashboardAsync()` and `RestoreTabSessionAsync()` as one unguarded async
  continuation — an exception anywhere in the first silently aborted the whole thing before the
  second ever ran, dropping every remembered tab beyond the first with no error shown anywhere.
  Each phase now runs in its own try/catch, surfacing any failure to `StatusBarText` instead of
  swallowing it. Directly answers the user's own diagnostic question: tab session storage itself
  isn't tied to which copy of the exe is running (it's per-user-profile, not per-install-location),
  but the underlying suspicion wasn't unreasonable — the discovery step this cascade started from
  really can behave differently depending on where it's run from, and the missing exception
  isolation is what let that turn into lost tabs.
- **Checked, not re-fixed**: a separately reported per-tab color bug (switching tabs left chrome
  tinted like the previous tab). Traced directly to already being fixed by other work already
  present in the source tree — `RefreshTabAccentVisuals()` already re-syncs every open tab's own
  color after any global theme switch, and each tab's own `AccentBrush` already takes precedence
  over the shared selected-state style. Not this version's work; flagged for the record rather than
  claimed. The screenshots that reported it were from the older, frozen v0.7.73.0 checkpoint build.

Full detail: [`release-notes/v0.7.74.0.md`](release-notes/v0.7.74.0.md),
[`docs/architecture/v0.7.74.0-close-dialog-shortcuts-and-tab-restore-fix.md`](docs/architecture/v0.7.74.0-close-dialog-shortcuts-and-tab-restore-fix.md).

## v0.7.73.0 — Tray Icon Honestly Reflects Running State

- Direct live bug report: the user found two real PalServer processes running with no MystTiq tray
  icon visible, and expected "no tray icon" to reliably mean "nothing is running." Investigated
  auto-start thoroughly before touching anything: no Windows Startup-folder entry, `Run` registry
  key, scheduled task, Windows service, or configured automation rule anywhere; tab-session restore
  (`RestoreTabSessionAsync` → `ConnectExistingProfileTab`) only reconnects/polls an already-running
  server's status, never calls Start. Root cause instead: the tray menu's "Exit GUI Only — keep
  services running" item always fully quit the Avalonia app — taking the tray icon down with it —
  even while deliberately leaving PalServer running in the background. The one exit path
  specifically built to leave something running had no visible trace once used.
- Fix, per direct confirmation on how to reconcile it: `ExitGui_OnClick` now checks
  `Tabs.Any(t => t.ServerIsRunning)` first (the same check `MainWindow_Closing` already uses for
  the window's own close button) — if anything is running, it collapses to the same
  minimize-to-tray path instead of exiting, so the tray icon stays up as an honest "something's
  running" signal. Only a genuinely idle app still fully exits with no tray at all.
- Tray tooltip was a static string regardless of state; added a 5-second `DispatcherTimer`
  (`UpdateTrayStatus()`) that names what's actually running — "idle, nothing running" / "running:
  Default Server" / "2 servers running: Default Server, second-local".
- Confirmed, not changed: headless log capture (`HeadlessConsoleLogWriter`,
  `WindowsServerLifecycleService`'s redirection, v0.7.72.0's native console-write hook) already runs
  entirely inside the separate `mysttiq-server.exe` process — `Exit GUI Only` never stopped that
  sidecar even before this fix, so logging already continued headless regardless of GUI state; this
  version makes that already-true guarantee visible via the tray rather than needing to add it.

Full detail: [`release-notes/v0.7.73.0.md`](release-notes/v0.7.73.0.md),
[`docs/architecture/v0.7.73.0-tray-reflects-running-state.md`](docs/architecture/v0.7.73.0-tray-reflects-running-state.md).

## v0.7.72.0 — Console-Write Hook (Native Console Capture, completed)

- Completes the native console capture project scoped (foundation-only) in v0.7.57.0. The
  originally-planned hook target, Unreal's own `FOutputDevice::LogfImpl`, was never viable — not
  exported, unstable offset across builds. Re-checked the same `dumpbin /imports` data v0.7.57.0's
  DSOUND target was picked from and found a better target: `PalServer-Win64-Shipping-Cmd.exe`
  imports `WriteConsoleA`/`WriteConsoleW` directly from `KERNEL32.dll` — the same low-level path
  Unreal's own console window writes through. Hooked both in the game's own Import Address Table
  (`native/MystTiqConsoleProxy/dllmain.cpp`'s new `PatchKernel32Import`) — surgical, only this one
  importer is touched, unlike a shared kernel32 export patch which would hit every DLL in the
  process.
- **Real bug found and fixed during live testing**: the capture log initially opened with no file
  sharing at all (plain `_wfopen_s`), so MystTiq's own concurrent tail-read failed with a sharing
  violation for the entire life of the game process. Fixed via `_wfsopen(..., _SH_DENYWR)` —
  confirmed live, reading the file successfully while PalServer was still running.
- **Live-verified safe end-to-end, never touching the real production server**: proven twice on an
  isolated clone (offset ports, throwaway admin password) — first via a raw process launch (proved
  the native mechanism alone: process stayed alive, reached full readiness, captured real
  `LogMemory` engine diagnostics never visible anywhere before), then again through the real
  shipped code path (a fully isolated `mysttiq-server api-run` instance, its own isolated fleet
  root and API port, install via the new API route, start via the real lifecycle service, confirmed
  `/logs/tail` surfaces the new `"PalServer console capture (native hook)"` source with the same
  real content).
- New opt-in backend capability: `HeadlessConsoleCaptureProxyService` (Windows-only; Linux honestly
  reports no DLL-proxy equivalent exists) + three routes (`GET /server/console-capture`,
  `POST .../install`, `POST .../uninstall`). Nothing in the lifecycle/launch path calls Install
  automatically — this places native code that runs inside the game process on every future
  launch, a materially different risk category from every other console source this app reads (all
  pre-existing files something else already writes), so it stays explicit-action-only. Refuses to
  overwrite a foreign `dsound.dll` or touch a locked file (server must be stopped to install/remove).
- `Build-AvaloniaDesktop.ps1`/`Build-WindowsHeadless.ps1` now stage the native proxy DLL (built
  separately via `scripts/Build-ConsoleProxy.ps1`, MSVC toolchain required) into the published
  headless host's `native/` folder — best-effort, never fails the .NET publish if the native
  artifact hasn't been built yet.
- **Honest finding, confirmed empirically rather than just predicted**: let the isolated test
  server run several minutes past full readiness — the capture log never grew past its initial
  handful of startup lines. Palworld's Windows dedicated server build genuinely writes nothing
  further to console once startup finishes, consistent with the earlier `NO_LOGGING` finding. This
  version adds real, previously-invisible startup diagnostics (memory stats, console-variable
  echoes); it does not solve "the console goes quiet during actual gameplay" — that appears to have
  no further fix available short of Palworld's own build re-enabling logging.
- **Not installed on this machine's real production server** — every risky step was proven safe on
  an isolated clone first; actually placing native code next to the real, live PalServer executable
  needs the user's own explicit go-ahead, asked for separately.

Full detail: [`release-notes/v0.7.72.0.md`](release-notes/v0.7.72.0.md),
[`docs/architecture/v0.7.72.0-console-write-hook.md`](docs/architecture/v0.7.72.0-console-write-hook.md).

## v0.7.71.0 — Console Source: PalDefender Log

- Direct follow-up to a user-provided screenshot of a visible Windows Terminal console window
  showing PalDefender Anti-Cheat startup diagnostics that never appeared in MystTiq's own Console
  page. Checked live against the real production install rather than assuming a cause: PalDefender
  (a UE4SS-loaded anti-cheat mod) writes its own timestamped per-session log file at
  `Pal\Binaries\Win64\PalDefender\Logs\{timestamp}.log`, confirmed to carry exactly the
  screenshot's content — startup banner, REST API port, load confirmation, the version-mismatch
  warning, and even PalServer's own "Running Palworld dedicated server on :PORT" banner line.
  `HeadlessMonitoringService.ResolveConsoleSources` never looked for this file.
- Fix: added `"PalDefender log"` as a new merged console source, using the same
  newest-file-in-directory pattern (`FindNewestTextLog`) already used for AdminCommands' own
  per-session server logs.
- **Verified live, not just statically**: rebuilt via `Build.ps1 DesktopWindows`, relaunched the
  real local API host (now v0.7.71.0), and confirmed via `/api/v1/servers/{id}/logs/tail` that the
  new source appears and carries real PalDefender content for both the real production (`default`)
  and clone (`second-local`) server profiles on this machine.
- **Disclosed, unchanged scope**: this does not add ongoing/live console capture — PalDefender's
  log, like every other source here, only ever carries that session's startup banner and then goes
  silent for the rest of the run. Genuine live capture of ongoing PalServer/mod output still needs
  the native DLL-proxy hook scoped (and deliberately not implemented, due to low disclosed payoff
  given the binary's `NO_LOGGING` build) in v0.7.57.0.

Full detail: [`release-notes/v0.7.71.0.md`](release-notes/v0.7.71.0.md),
[`docs/architecture/v0.7.71.0-paldefender-console-source.md`](docs/architecture/v0.7.71.0-paldefender-console-source.md).

## v0.7.70.0 — Real Email Notification Dispatch

- Implemented Email as a real notification channel — previously a typed stub since v0.6.1.0 that
  just logged "not implemented" and dropped the notification, unlike `Webhook`/`Discord` (both
  real, v0.6.1.0/v0.6.17.0). `NotificationChannelConfig` gained optional Email fields (`SmtpHost`,
  `SmtpPort` — default 587, `SmtpUseSsl` — default true, `SmtpUsername`/`SmtpPassword`,
  `EmailFrom`/`EmailTo`), all defaulted so every existing construction elsewhere keeps compiling.
  `HeadlessNotificationRoutingService.DispatchEmailAsync` sends via `System.Net.Mail.SmtpClient`
  (the BCL's own client — reused rather than hand-rolled, unlike `PalworldRconService` where no BCL
  equivalent exists for Source RCON), with the same retry-once shape the other channels already use.
- `SmtpPassword` stored in plaintext in `channels.json`, matching this project's existing
  local-secret convention (same threat model as RCON's `AdminPassword`, bearer tokens). Only
  STARTTLS-style submission (port 587) is supported, not implicit TLS (port 465) — a `SmtpClient`
  limitation, disclosed.
- New coverage: 1 scenario added to `scripts\Testing\MystTiq.LogicHarness` (21/21 pass) using a
  minimal, real SMTP protocol stub server proving the exact envelope, authentication, subject and
  body reach the wire correctly. The stub's own first draft had a real protocol-shape bug
  (`SmtpClient` sends the `AUTH LOGIN` username inline on the same line, not as a separate line per
  the textbook three-step exchange) — caught and fixed during testing, not shipped as a false pass.

Full detail: [`release-notes/v0.7.70.0.md`](release-notes/v0.7.70.0.md),
[`docs/architecture/v0.7.70.0-email-notification-dispatch.md`](docs/architecture/v0.7.70.0-email-notification-dispatch.md).

## v0.7.69.0 — Stale Crash-State Fix (Windows)

- Fixed another previously-flagged-but-never-chased-down item from the same roadmap audit: a server
  shown as "Crashed" on Windows only ever cleared via an explicit Start — pressing Stop did nothing,
  leaving the stale Crashed state stuck indefinitely. Root cause:
  `WindowsServerLifecycleService.StopAsync`'s "nothing to stop" branch never wrote to the persisted
  state store at all, unlike `LinuxServerLifecycleService`'s equivalent branch, which already writes
  a fresh `Stopped`/`StopRequested: true` state there — a genuine Windows-only inconsistency.
- Fix: Windows's branch now mirrors Linux's exactly, writing a fresh acknowledged `Stopped` state
  before returning, instead of leaving whatever `GetStatusAsync` currently reports (which stays
  `Crashed` forever once set, since its own re-check only fires from `Running`/`Starting`).
- New coverage: 1 scenario added to `scripts\Testing\MystTiq.LogicHarness` (20/20 pass) constructing
  a real `WindowsServerLifecycleService` (fake process-inspector reporting nothing running, a
  pre-seeded Crashed state) and proving the real `StopAsync` call clears it correctly.

Full detail: [`release-notes/v0.7.69.0.md`](release-notes/v0.7.69.0.md),
[`docs/architecture/v0.7.69.0-stale-crash-state-fix.md`](docs/architecture/v0.7.69.0-stale-crash-state-fix.md).

## v0.7.68.0 — Graceful Shutdown: RCON-First Fix

- Diagnosed and fixed a real, previously-flagged-but-never-chased-down bug from the roadmap audit:
  graceful shutdown had been observed falling back to forced termination on effectively every real
  stop (v0.6.10.0's own session notes: "4/4 occurrences," never explained). Root cause:
  `Process.CloseMainWindow()` in `WindowsServerLifecycleService.StopAsync` silently does nothing once
  MystTiq's own `ApplyPostLaunchWindowPolicyAsync` (runs on a 500ms loop for 30+ seconds after every
  launch, explicitly `ShowWindow(SW_HIDE)`s PalServer's window to keep the app headless-first) has
  hidden PalServer's window — `Process.MainWindowHandle`, which `CloseMainWindow` depends on, only
  resolves a handle for a currently-*visible* window. MystTiq's own headless-mode feature was
  silently defeating its own graceful-shutdown feature.
- Fix: both `WindowsServerLifecycleService.StopAsync` and `LinuxServerLifecycleService.StopAsync` now
  try Palworld's native RCON `Shutdown 1 <message>` command first, when RCON is enabled and a
  password is configured — the game's own real graceful-exit path (broadcast, save, clean exit), with
  no dependency on window visibility or process signals. Purely additive: the existing
  `CloseMainWindow`/force-kill (Windows) and `SIGTERM`/`SIGKILL` (Linux) chain still runs completely
  unchanged if RCON isn't configured or doesn't result in a clean exit, so this can only improve the
  outcome, never regress it. Linux's `SIGTERM` wasn't confirmed broken the way `CloseMainWindow` was
  on Windows — this is a genuine reliability improvement there, not a bug fix.
- New coverage: 2 scenarios added to `scripts\Testing\MystTiq.LogicHarness` (19/19 pass) using a
  minimal, real Source RCON protocol stub server that proves `PalworldRconService.ExecuteAsync` sends
  the exact `"Shutdown 1 MystTiq requested a graceful shutdown."` command over the real wire protocol
  and correctly parses both success and rejected-password responses.
- **Update, same session**: end-to-end confirmed live against a real PalServer, at direct request to
  work on the previously structurally-blocked items. Cloned the real production install (~11 GB,
  never touching the live install/process) with offset ports and a throwaway admin password, then
  ran a direct A/B comparison on the real, real-modded server: RCON enabled → `Stop` in ~4 seconds,
  clean; RCON disabled (otherwise identical) → the same call took ~31 seconds and reported forced
  termination, the exact pre-fix behavior reproduced on demand. Clone fully torn down afterward;
  production confirmed unchanged throughout.

Full detail: [`release-notes/v0.7.68.0.md`](release-notes/v0.7.68.0.md),
[`docs/architecture/v0.7.68.0-graceful-shutdown-rcon-fix.md`](docs/architecture/v0.7.68.0-graceful-shutdown-rcon-fix.md).

## Slow-startup investigation (not reproduced) — session following v0.7.70.0

- The last structurally-blocked audit item: Palworld observed taking 10+ minutes of active CPU
  without completing startup, on both production and its clone (v0.6.10.0's own session notes),
  never diagnosed. Investigated this session using the same clone-and-isolate technique as the RCON
  A/B test above, against the real production save and its real 6-MOD set. Six consecutive start
  cycles all completed in 6–7 seconds — the issue did not reproduce. A genuine negative result, not
  a fix: the underlying cause (if still present under conditions not exercised here) remains
  undiagnosed. See the architecture doc for what was tried and why it wasn't closed.

Full detail: [`docs/architecture/v0.7.70.0-slow-startup-investigation.md`](docs/architecture/v0.7.70.0-slow-startup-investigation.md).

## v0.7.67.0 — Tray Reminder Toast: Layout Timing Fix

- Completes v0.7.66.0's tray toast positioning fix, found by actually testing it on the real
  reported hardware (the user asked to "try it out on the real hardware" — this machine turned out
  to be that exact multi-monitor setup). v0.7.66.0 fixed *which monitor* the toast resolves to; this
  release fixes a second, independent bug in *where on that monitor* it lands:
  `PositionBottomRight()` read `Bounds` at the moment `Opened` fired, before `SizeToContent` had
  actually measured the toast's real small content size. `Bounds` was already non-zero at that
  point — so the existing `Bounds.Width > 0 ? Bounds.Width : 320` fallback never caught it — just
  wrong (a placeholder ~1521×770, roughly the size of an unconstrained default window), corrupting
  the bottom-right math on both axes.
- Fix: `TrayReminderToast`'s constructor now also subscribes to `LayoutUpdated` (in addition to the
  existing `Opened` subscription), re-running `PositionBottomRight()` every time it fires — cheap and
  idempotent once the real size stabilizes.
- **Verified with an exact pixel match on the real hardware**: a standalone test harness (scratch,
  not part of the shipped solution) drove the real `TrayReminderToast` class with a stand-in window
  placed on this machine's actual secondary monitor. Before the fix: landed at `(766, -834)` against
  an expected `(1972, -127)` — provably explained by the placeholder-Bounds arithmetic above. After
  the fix: landed at exactly `(1972, -127)`, converging there through several `LayoutUpdated`
  corrections as `Bounds` settled. Full before/after logs preserved in the architecture doc.

Full detail: [`release-notes/v0.7.67.0.md`](release-notes/v0.7.67.0.md),
[`docs/architecture/v0.7.67.0-tray-toast-layout-timing-fix.md`](docs/architecture/v0.7.67.0-tray-toast-layout-timing-fix.md).

## v0.7.66.0 — Tray Reminder Toast Positioning Fix

- Fixes a real reported bug: on a multi-monitor layout (secondary monitor stacked above the primary
  at a negative Y origin), the "MystTiq is still running" tray toast (`TrayReminderToast`, v0.7.11.0)
  rendered far from its intended bottom-right corner. Root cause: `PositionBottomRight()` resolved
  its target monitor via `Screens.ScreenFromWindow(this)` at the moment `Opened` fires — but the
  toast (`WindowStartupLocation="Manual"`, no `Position` ever explicitly set) is still sitting at
  whatever default the OS/window manager assigned a moment earlier, not yet the corner it's about to
  move to; that default isn't guaranteed to land on the intended monitor, especially with a
  negative-Y secondary. `TrayReminderToast` now accepts an optional owner `Window`, resolves the
  screen from it first when supplied, and `App.ShowTrayStillRunningReminder` now passes `mainWindow`
  — always correctly positioned when the toast appears, since it only shows right after
  `mainWindow.Hide()`.
- Not verified visually — no way to render the app or reproduce the reported multi-monitor layout in
  this environment. Disclosed explicitly.

Full detail: [`release-notes/v0.7.66.0.md`](release-notes/v0.7.66.0.md),
[`docs/architecture/v0.7.66.0-tray-toast-positioning-fix.md`](docs/architecture/v0.7.66.0-tray-toast-positioning-fix.md).

## v0.7.65.0 — Linux Console Capture Fix

- Fixes the Linux console-capture gap flagged in v0.7.64.0, at direct follow-up request. Closer
  tracing found a more precise bug than "no capture at all": `LinuxServerLifecycleService`'s detached
  PalServer launch already redirected stdout/stderr via `setsid -f ... >> file 2>&1` — just into
  `ManagerRuntimeRoot/palserver-console.log`, a location nothing on the read side
  (`HeadlessMonitoringService.ResolveActiveLogPath`, the Live Console page, Doctor) ever looked at;
  all of those only ever check `LogsRoot/MystTiq-PalServer-Console.log`, the exact file Windows
  captures into.
- `LaunchDetached` now targets `LogsRoot/MystTiq-PalServer-Console.log` (with the same
  create-with-fallback directory logic `WindowsServerLifecycleService` already uses), so Linux's
  capture reaches the same consumers Windows' does. Also gets v0.7.64.0's `ConsoleLogRotation`,
  called once immediately before each server start rather than continuously — the only point in this
  platform's detached-process architecture where rotation is safe, since bash's `>>` redirect holds
  one file handle open for the entire session and renaming the file out from under it mid-session
  would just make the shell keep writing to the renamed `.1` generation forever.
- `Test-v0.7.65.0-LinuxAcceptance.sh`/`Test-v0.7.65.0-ProductionReadiness.sh` carried forward from the
  v0.7.62.0 copies (required to exist for `Build-LinuxHeadless.ps1` to build at all); one new targeted
  `--extended` check added, deriving the real `ServerRoot` from the live checklist response rather
  than a hardcoded path.
- **Verified live against the real reference Linux VM** — the correct address is `192.168.1.143`
  (an earlier `.144` in this session's own notes was a stale typo, corrected by the user). Built a
  fully isolated test (scratch directory, throwaway port, stand-in server script, non-conflicting
  game port) rather than running the acceptance script's `--extended` flag directly against the live
  production config; confirmed the real `mysttiq-palworld.service` was untouched (same PID)
  throughout. Live-confirmed: the console log lands at the correct new path with the expected start
  marker and real captured stdout/stderr, and rotation correctly moves a 26 MB file to a `.1`
  generation on the next server start.

Full detail: [`release-notes/v0.7.65.0.md`](release-notes/v0.7.65.0.md),
[`docs/architecture/v0.7.65.0-linux-console-capture-fix.md`](docs/architecture/v0.7.65.0-linux-console-capture-fix.md).

## v0.7.64.0 — Roadmap-Wide Gap Audit and Fixes

- Cataloged ~75 open items across every disclosed gap in this project's own history (release notes'
  "Known gaps" sections, docs/architecture/*.md, docs/roadmap/*.md, TODO/stub markers) at the user's
  request to keep going past v0.7.63.0's theme-system fix. Most are honestly-scoped future work with
  no defect behind them; the genuine, concrete, already-shipped-code bugs found are fixed here.
- `HeadlessAutomationService.CreateRule`/`UpdateRule` previously accepted any `int` for
  `Trigger.IdleThresholdMinutes`/`JitterSeconds`/`Interval` and any entry in
  `Action.WarningCountdownSecondsBeforeAction`, persisting and returning invalid values verbatim even
  though the scheduler silently reinterprets them at evaluation time (`Math.Max(1, ...)`, a
  non-positive `Interval` falling back to 1 hour). New `ValidateTriggerAndAction` (public static, unit
  -testable without the service's 13-dependency graph) rejects these at creation/update; the
  `POST`/`PUT /automation/rules[/{id}]` routes now return `400` with a clear message instead of
  silently accepting them.
- `MystTiq-PalServer-Console.log` had no size cap or rotation on either of its two independent
  writers (`HeadlessConsoleLogWriter`, `WindowsServerLifecycleService`'s raw stdout/stderr capture) —
  unbounded growth on a long-running production service. New `MystTiq.Core.Services.
  ConsoleLogRotation.RotateIfNeeded` caps it at 25 MB, keeping one prior `.1` generation, called from
  both writers immediately before each append.
- Three stale "BACKEND REQUIRED" disclosures fixed to match shipped reality: World Transactions'
  Repair Center still claimed Base ownership/recovery repair was unbuilt (shipped
  v0.5.3.0/v0.5.4.0, live on the Bases page); the Server Setup checklist's UE4SS row still claimed no
  install path existed (v0.7.49.0 shipped one, and `MainWindowViewModel.RunEnvironmentAction` already
  had a dedicated navigation case for it sitting unreachable behind the stale flag); the same
  checklist's Backup Storage row claimed no automated fix existed (Doctor's Fix Automatically has done
  this since v0.6.4.0 — only the note text was corrected here, since this page's own action has no
  dedicated handler for it).
- Newly-surfaced but deliberately not fixed this pass: `LinuxServerLifecycleService` has no PalServer
  stdout/stderr console-capture at all, unlike its Windows counterpart — found while tracing the log
  writers for the rotation fix. Flagged for a future session.
- New coverage: 11 scenarios added to `scripts\Testing\MystTiq.LogicHarness` (17/17 pass) and a new
  `scripts\Test-v0.7.64.0-RouteSmoke.ps1` (5/5 pass) verifying the validation/checklist fixes live
  against the real running sidecar, not just statically.

Full detail: [`release-notes/v0.7.64.0.md`](release-notes/v0.7.64.0.md),
[`docs/architecture/v0.7.64.0-roadmap-wide-gap-audit-and-fixes.md`](docs/architecture/v0.7.64.0-roadmap-wide-gap-audit-and-fixes.md).

## v0.7.63.0 — Central Theme System Audit: Status-Color Bypass Fix

- User-reported live: the "change colour" theme system was supposed to control every resource
  centrally, but several status colors stayed locked to one look regardless of accent theme or
  Light/Dark — the tab-bar connection dot, the Dashboard health label, the Fleet server list's status
  dot, and Update Center's component-status table.
- Root cause: those four values were each computed as a hardcoded hex literal in C# (a ViewModel
  property, a Model property, or a cached `IValueConverter` brush) and bound with a plain `{Binding}`
  rather than routed through `{DynamicResource}` — a different bug class from the ~530
  hardcoded-gradient-literal sweep v0.7.48.0/v0.7.56.0 actually covered, so it survived both "central
  theme system completion" passes undetected. Fixed by following the correct pattern
  `TabSession.AccentBrush` (v0.7.52.0) already established: resolve the brush live via
  `Application.Current.TryGetResource` instead of caching or hardcoding it. New shared
  `SemanticStatusColorConverter` centralizes this; `TabSession.StatusDotColor` →
  `StatusDotColorKey`, `MainWindowViewModel.HealthStateColor` → `HealthStateColorKey`,
  `ServerProfileSummaryDto.StatusDotColor` → `StatusDotColorKey` (now semantic keys, not hex
  strings), `ComponentStatusColorConverter` now delegates to the shared converter.
- Also fixed: `Border.statuscard` (the bare, non-accent class used by the sidebar's mini
  connection-status card and the duplicate-install-location warning banner) had no themed style at
  all in `Styles/DesignSystem.axaml` — `App.axaml`'s pre-DesignSystem-era hardcoded style
  (`#0E2133`/`#1E5279`) was the only one that ever applied, un-themed this whole time.
- Cleanup: removed `App.axaml`'s entire legacy parallel color-resource system
  (`MystTiqBackground`/`MystTiqButtonMetal`/etc.) and its now-fully-shadowed base
  `Window`/`Button`/`Border.card`/`TextBlock.muted` styles, plus two fully-dead nav-item selectors
  with zero live usages (`Button.navitem`, `Expander.navgroup` — the real nav uses
  `ToggleButton.nav`/`Border.navSurface`). `Button:focus-visible`'s hardcoded `#75C8FF` moved to
  `DesignSystem.axaml` as a theme-aware resource rather than being deleted.
- Deliberately not changed: `ConsoleLineColorConverter` (log-line highlighting in the mini
  console/RCON output/Console page) stays hardcoded, consistent with v0.7.48.0's own disclosed
  decision to keep terminal-style views dark/green in every theme.

Full detail: [`release-notes/v0.7.63.0.md`](release-notes/v0.7.63.0.md),
[`docs/architecture/v0.7.63.0-central-theme-system-audit-and-fix.md`](docs/architecture/v0.7.63.0-central-theme-system-audit-and-fix.md).

## v0.7.62.0 — Dashboard/Ribbon Layout Overlap Fix

- Reported live: after certain reconnect sequences, the ribbon and category tabs went visually
  blank on every page (confirmed on both Dashboard and Inspector), while the page title/subtitle
  and connection badge kept displaying correctly.

### The finding

- With `dotnet-sos` and Sysinternals `cdb` already installed earlier this session for the freeze
  investigation, attached non-invasively to the live, currently-buggy app process and dumped the
  actual `MainWindowViewModel` fields at the exact moment the bug was visible on screen.
- Every relevant field was genuinely correct: `VisibleRibbonGroups` held its normal 3 groups (not
  empty), `_ribbonWidth` was a real, healthy measured value, `SelectedPage` correctly matched the
  displayed page. This ruled out every ViewModel/data-layer explanation categorically — the bug was
  never in application logic at all, only in how it was being rendered.
- The user's own description ("the category tabs look like they're in the background") pointed
  directly at a Z-order/overlap issue rather than missing content. `MainWindow.axaml`'s outer `Grid`
  has no `ColumnDefinitions` at all — every row-1/2 child (the decorative background, the category
  tabs, the ribbon, and the page-header title/subtitle panel) shares one single column, with only
  each element's own `HorizontalAlignment` keeping it visually confined to "its" side. The
  page-header panel is `HorizontalAlignment="Right"` with a `MinWidth="460"` but **no `MaxWidth`** —
  nothing bounds how wide it can grow if an Arrange pass (plausibly triggered by a reconnect's
  visibility toggling) ever stops respecting that alignment constraint. Its own opaque
  `CardGradient` background would then paint directly over the category tabs and ribbon beneath it
  in the same shared column — exactly matching the reported symptom, and why title/subtitle text
  (the panel's own content) kept rendering fine while everything underneath it disappeared.

### The fix

- Added `MaxWidth="620"` to the page-header panel, bounding it regardless of the exact Avalonia
  layout condition that was letting it expand — a small, safe, targeted fix instead of a larger,
  riskier restructuring into real `Grid.ColumnDefinitions`, which would also change the app's
  current "floating overlay" visual design (ribbon/tabs spanning full width, header floating on top
  right) rather than just fixing the bug.
- Confirmed fixed live: reproduced the original bug, applied the fix, rebuilt, and confirmed the
  ribbon/category tabs/nav pane render correctly on both Dashboard and Inspector after the same
  reconnect sequence that previously broke them.

## v0.7.61.0 — PalServer Launch Freeze Root Cause Fix

- The root cause of the PalServer launch freeze — investigated on and off since v0.7.57.0, spanning
  the UE4SS/PalDefender injection chain, a separate SteamCMD `STATUS_STACK_OVERFLOW` crash, Windows
  Defender, Hyper-V/Realtek NIC networking, and Steam file-integrity checks — found live, on this
  session's own genuinely-frozen production server.

### The finding

- A frozen PalServer instance was caught live and inspected with Process Monitor (Sysinternals):
  after its initial ~7 `Thread Create` events, it produced **zero further activity of any kind** —
  no file, registry, or network I/O — for the entire time it sat frozen. That ruled out every I/O
  stall theory (including the network/Hyper-V hypothesis tested moments earlier) and pointed at a
  pure in-process synchronization wait instead.
- Process Explorer's live thread-stack inspection on that same frozen process nailed it down: the
  blocked thread's call stack showed `USER32.dll!MessageBoxW` → `MessageBoxTimeoutW` →
  `MessageBoxIndirectA` → `SoftModalMessageBox` → `win32u.dll!NtUserWaitMessage`. The process is
  blocked showing a native Win32 message box, waiting for someone to click it. PalServer runs with
  no interactive desktop session under MystTiq (headless launch, no visible window), so that dialog
  is permanently invisible and unclickable — the thread, and the whole server, waits forever. Zero
  CPU growth, no crash, no port bind, nothing in any event log: every symptom this investigation
  ever observed, explained by one missing launch flag.
- Unreal Engine's `-unattended` command-line flag exists specifically to suppress this class of
  modal dialog (engine `ensure()`/fatal-error dialogs, and dialogs some third-party SDKs including
  Steamworks can show), converting them to log output instead. It was never present in any of
  MystTiq's launch argument sets — not the Windows default, not the Linux default, not the user's
  real persisted production/clone profiles.

### Live confirmation

- Killed the frozen instances, relaunched the identical binary/save/arguments with `-unattended`
  added, and watched both the production server and its clone blow straight past the point they had
  always frozen at: UE4SS fully hooked all engine functions, all three MODs (BetterBaseBuilding,
  MystPalIntelligence, AdminCommands) loaded successfully, PalDefender anti-cheat started cleanly —
  none of which any previous attempt this session ever reached. Thread state confirmed genuinely
  different from the freeze: 1 thread actively `Running` (real, growing CPU time) with the rest
  normally `Wait`ing, instead of every thread sitting in `Wait` with zero CPU growth.
- A prior live test this session — temporarily re-pointing the Hyper-V "MystTiq External" virtual
  switch off the physical Realtek NIC and onto a spare USB NIC, to rule out a network-stack theory —
  did NOT fix the freeze (confirmed via an identical relaunch immediately after), correctly ruling
  that theory out before the real cause was found via Process Monitor/Process Explorer.

### The fix

- `-unattended` added to both platform default launch-argument templates
  (`HeadlessConfiguration.CreateWindowsDefault`/`CreateLinuxDefault`) so every newly-created profile
  gets it going forward.
- `HeadlessConfigurationService.LoadOrDefault` now backfills `-unattended` into every already-loaded
  profile's `LaunchArguments` if it's missing (case-insensitive check, purely additive, no schema
  version change) — existing installs, including this machine's own real production and clone
  profiles, are fixed automatically on the next config load rather than requiring a manual edit.
- The real, live `mysttiq.json` on this machine was also updated directly during the investigation
  so the fix is already in place locally; the still-running old headless host process (v0.7.50.0)
  will pick it up on its next restart.

## v0.7.60.0 — Port Conflict Prevention & UE4SS Version Tracking

- Two direct requests, arriving alongside continued investigation of the still-unresolved PalServer
  launch freeze from v0.7.57.0/v0.7.59.0.

### Port Conflict Prevention

- **A real, previously-unguarded failure mode**: nothing stopped two attempts at starting a server
  on the same UDP port from racing — the only existing guard (`FindManagedServerProcesses().Count >
  0`) only catches a duplicate start of the SAME profile's own already-tracked process, saying
  nothing about another profile, an unmanaged process, or a leftover from an unclean stop already
  holding the port. Launching anyway doesn't fail cleanly: PalServer starts, spins up its full
  engine thread pool, and then sits forever unable to bind — a state genuinely indistinguishable
  from a hang without deep diagnosis (discovered directly while investigating this session's own
  live freeze).
- `WindowsServerLifecycleService`/`LinuxServerLifecycleService.StartAsync` now check whether the
  configured game port is already bound by anything, using the existing, already-per-profile-aware
  `GetGuardedListeningPorts()` (confirmed via a live trace through `Program.cs`'s lifecycle-factory
  wiring that this already correctly reflects each profile's own configured port, not a shared
  hardcoded default). A conflict now returns immediately with a new `HeadlessExitCode.PortConflict`
  (mapped to HTTP 409) and a clear, actionable message — before the process is ever created, not
  after waiting out the full startup timeout for a port that was never going to bind.
- Surfaces through the Desktop's existing `LifecycleStatusText`/`Detail` mechanism, already shown
  prominently on the Dashboard's SERVER card — no new UI needed for the message itself to reach the
  user clearly.

### UE4SS Version Tracking

- **A real, user-reported bug**: Update Center could report UE4SS as current when it was not,
  because there was never anything trustworthy to compare against — this fork ships no version
  marker file, and the installed DLL's own `FileVersionInfo` comes back unstamped ("0.0.0.0",
  already filtered out by v0.7.30.0's own fix). `CheckUe4ssAsync` always fell back to a generic
  "Check manually" status regardless of what was actually installed.
- `ApplyUe4ssInstallAsync` (the real UE4SS install flow from v0.7.49.0) now records exactly which
  release catalog entry (`Source`/`TagName`) it actually applied, in a small manifest under
  `ManagerRuntimeRoot` — the one place MystTiq genuinely knows the true installed version, since it
  just downloaded and installed it itself. Invalidated on Rollback (the snapshot being restored
  could be from any earlier state, not necessarily a tracked release).
- `CheckUe4ssAsync` now does a real, exact tag comparison when that manifest exists — genuine
  `Up to date`/`Update available` status, not a guess. Falls back to the same honest "Check
  manually" behavior whenever no manifest exists (an install made before this tracking existed, or
  files copied in manually, bypassing MystTiq's own install flow entirely — exactly how this
  project's own real server's UE4SS was actually installed until this session).

## v0.7.59.0 — Safe-Start MOD Diagnostic

- Direct request, following real web research into the PalServer launch freeze investigated in
  v0.7.57.0: "remove mods one by one until the crash stops" turned out to be the standard,
  explicitly-documented community troubleshooting technique for Palworld server crashes. This
  automates exactly that, rather than inventing a new approach.
- **Detection covers both failure modes actually seen in this project's own investigation** — a
  naive crash-exit-only check would have missed the specific freeze this session spent hours
  chasing (process alive, near-zero CPU forever, UDP port never binds, no crash). A candidate MOD
  is disqualified if the server either crashes (`ServerLifecycleSnapshot.CrashDetected`) OR times
  out without ever becoming ready (`HeadlessExitCode.StartupTimeout`).
- **A baseline sanity check runs first**: every candidate MOD is disabled, then one start is
  attempted with none of them. If even that fails, the diagnostic stops immediately and reports
  "not a MOD problem" instead of cycling through every MOD and reporting each one as bad — this
  exact scenario was independently confirmed earlier this session (the live freeze persisted with
  the entire UE4SS/PalDefender/d3d9 injection chain removed).
- **Cumulative testing, not isolated testing**: MODs are re-enabled one at a time on top of the
  already-confirmed-good set, not each tested totally alone — real MOD ecosystems have dependencies
  (shared/core UE4SS libraries other mods rely on), so testing every MOD in isolation would produce
  false failures.
- **Fully automatic recovery**: any MOD that fails is disabled again and the run continues; the
  surviving set gets one final validation start and is left running.
- New `MystTiq.HeadlessHost.HeadlessModSafeStartService` — a genuinely new pattern for this
  codebase: an on-demand, multi-minute background job with polled status, held under the same
  `IOperationCoordinator` lock Start/Stop/Restart already use (`["lifecycle", "world-mutation"]`),
  but for the job's *entire* duration rather than one synchronous call, so nothing else can race a
  live start/stop cycle mid-diagnostic. Three new routes: `POST /mods/safe-start` (begin),
  `GET /mods/safe-start/status` (poll), `POST /mods/safe-start/cancel` (abort and restore original
  MOD state).
- Desktop: a new live-progress card in MOD Library, polled by its own `DispatcherTimer` independent
  of `IsBusy` (the diagnostic itself runs for minutes, so holding `IsBusy` the whole time would
  disable unrelated UI needlessly) — shows current phase, per-MOD pass/fail results as they land,
  and the final outcome.

## v0.7.58.0 — Website-Sourced MOD Descriptions, Light Polish

- Direct request to polish v0.7.55.0's DESCRIPTION panel. Found three genuine rough edges on
  review rather than inventing busywork:
- **Long descriptions were silently clipped, not scrollable.** The description `TextBlock` had a
  fixed `MaxHeight="220"` with no way to see anything past it — real Steam Workshop descriptions
  routinely exceed that. Wrapped in a `ScrollViewer` with `VerticalScrollBarVisibility="Auto"`.
- **The Source URL was inert text.** No way to actually visit the mod's real Steam Workshop or
  GitHub page without manually copying the text. Added an "Open" button next to it, reusing the
  same `Process.Start`/`UseShellExecute = true` pattern `WorkspaceOpen_Click` already established —
  only ever passes an already-validated `http`/`https` URL (the backend's own
  `SetModDescriptionSourceAsync` already rejects anything else before it can be stored).
- **The "No known Workshop match" message showed prematurely.** It was bound to
  `!HasSelectedModDescription`, which is also true before the user has ever clicked Fetch for a
  MOD — presumptuously claiming "no match" before any fetch attempt had happened. New computed
  `MainWindowViewModel.ShowNoModDescriptionMatchMessage` requires both a completed fetch
  (`HasModDescriptionResult`) and no match found (`!HasSelectedModDescription`), so the guidance
  only appears after an actual failed lookup.

## v0.7.57.0 — Native Console Capture, Proxy DLL Foundation

- The real, working half of the PalServer DLL-proxy logger scoped back in v0.7.51.0 — the item
  surfaced by a live bug report ("no PalServer console output captured") and researched extensively
  before this session picked it up: `-ABSLOG` produces no file, launching the engine process
  directly still captures zero lines even with redirected pipes, and UE4SS's own Lua API has no
  hook into Unreal's engine log. DLL proxy injection is the one proven technique (the same category
  UE4SS/PalDefender/third-party PalServerLogger all already use).
- **A new native C++ artifact**, `native/MystTiqConsoleProxy/` — outside the .NET solution
  entirely, built via a new standalone `scripts/Build-ConsoleProxy.ps1` using the MSVC toolchain
  already confirmed present on this machine (Visual Studio Community 18, MSVC v143). Proxies
  `DSOUND.dll` — re-confirmed this session via `dumpbin /imports` against the real
  `PalServer-Win64-Shipping-Cmd.exe` that it imports exactly 6 functions from DSOUND.dll, all by
  ordinal, chosen specifically because neither UE4SS nor PalDefender's own `d3d9.dll`-based proxy
  already occupies it. The `.def` file pins each export to the EXACT ordinal the real system
  `dsound.dll` uses (verified by direct `dumpbin /exports` comparison) — critical since PalServer
  imports by ordinal, not by name.
- **Actually verified, not just compiled**: loaded the built DLL in isolation via `LoadLibrary`,
  confirmed it resolves and forwards to the real system `dsound.dll`, confirmed its lifecycle log
  writes correctly, confirmed it unloads cleanly with no crash. This is real infrastructure a real
  game process can load safely.
- **The actual Unreal log-capture hook is deliberately not implemented.** Finding the target
  function's address requires a live memory signature scan against a running process, and this
  session's own production PalServer can't currently launch cleanly at all (a separate, unresolved
  environment issue investigated the same session — ruled out UE4SS/PalDefender/the d3d9 proxy chain
  as the cause via direct testing, then found SteamCMD itself crashes with `STATUS_STACK_OVERFLOW`
  on this machine right now, independent of Palworld entirely). Shipping an unverified memory patch
  that could crash a real game server isn't something to do without the ability to test it.
  Re-confirmed this session, independently of the earlier investigation, that the binary's Unreal
  log category strings (`LogTemp`, `LogEngine`, etc.) are still absent — `NO_LOGGING` still holds,
  so the realistic payoff of finishing the hook is genuinely small.
- **Not wired into the app.** No auto-install flow, no reference from `MystTiq.HeadlessHost`/
  `MystTiq.Desktop` — ships as a standalone, disclosed-experimental artifact until live verification
  against a real process becomes possible.

## v0.7.56.0 — Central Theme System Completion, Remaining Decorative Gradients

- The rest of the theming gap deferred from v0.7.48.0's foundation pass: ~240 hardcoded colors
  remained in `DesignSystem.axaml`, overwhelmingly the elaborate multi-stop translucent "glass"
  gradients — `GlassOptionHoverGradient`, the `PrimaryGlass*`/`SuccessGlass*`/`DangerGlass*`
  families, the four `Nav*Glass*Gradient` variants, `DashboardGlassGradient`, the `Context*Gradient`
  family (5), the `*GraphFill` family (5), the `Backup*Gradient` family (4), plus the `dataRow` list
  style's own hardcoded literals. Deferred at the time because translucent glass behaves differently
  over a dark vs. light background — a simple lighten/darken swap can't safely reproduce it, unlike
  the opaque card/border/glow resources v0.7.48.0 already covered.
- **Slotted in as v0.7.56.0, not v0.7.57.0**: the roadmap's own v0.7.56.0 (Second Local Server +
  Remote Linux Server, Both Cloned) turned out to be pure live-infrastructure work with no shippable
  code — a real second local server profile was cloned and registered, but starting it (and the
  original production server) hit an unexplained, reproducible PalServer launch freeze on this
  machine, tabled by direct request pending a device restart. Following this session's own "no
  version-slot gaps" discipline (see v0.7.51.0's self-correction), this version fills that slot
  instead of leaving it empty; the Second Local Server / Remote Linux Server item remains open,
  unscheduled, to resume once the local environment issue is resolved.
- **Approach, matching v0.7.48.0's own discipline**: formula-driven, not hand-authored — ~240
  individual values × 4 themes × 2 variants has no way to be manually tuned with any confidence
  without visual verification, which this environment cannot provide. New `ThemeApplier` helpers:
  `BuildGraphFill` (simple alpha-fade-to-transparent, safe over any background, the lowest-risk
  family), `BuildContextGradient` (2-stop diagonal accent-tinted corner cards), `BuildGlassSheen`
  (the bright-lip-to-tinted-face "directional option sheen" family), `BuildNavGlass` (the nav
  sidebar's structural, non-page-accent glass states plus the Dashboard atmosphere overlay). Each
  mirrors the existing hand-authored Dark-mode structure as closely as a formula reasonably can,
  with a parallel principled Light-mode treatment (fading toward white/near-transparent instead of
  toward black) — not a guaranteed byte-for-byte match to today's exact literals, disclosed the same
  way v0.7.48.0 disclosed its own Card{Name}Gradient/border/glow set's minor Dark-mode shift.
- **Dead code found, left alone rather than themed**: `Border.modRow`/`Border.prototypeRow`/
  `Border.worldTabHeader`/`Button.disableAction`/`Button.updateAction` are not referenced anywhere
  in current `MainWindow.axaml` navigation — confirmed via search before spending effort on them,
  matching the architecture doc's own suspicion from v0.7.48.0 ("legacy prototype-page remnants, not
  confirmed still reachable"). Only `Border.dataRow` (genuinely used, one live reference) was
  converted to themed resources.
- No `.axaml` structural changes needed for most of these families — `ThemeApplier.Apply()`
  overwrites the exact same resource keys `DesignSystem.axaml` already declares statically
  (Application-level resource overrides already proven to win over StyleInclude-declared ones, per
  v0.7.48.0's own `Card{Name}Gradient` precedent), so existing `{DynamicResource GlassOptionHoverGradient}`
  etc. references throughout the file needed no change. Only `dataRow`'s three states (previously
  literal hex directly on the Style selector, no named resource to override) needed real XAML edits.

## v0.7.55.0 — Website-Sourced MOD Descriptions

- Item 40's deferred half: MOD Library's MOD DETAILS panel (v0.7.40.0) shows real evidence but never
  a description from the mod's own source page. Asked directly how to scope it; the answer was to
  design for multiple sources up front (Steam Workshop + at least one more, e.g. Nexus Mods) rather
  than starting Workshop-only.
- **Two real, verifiable sources, not one**: Steam Workshop items are matched via the exact same
  local-content lookup `CheckModUpdateAsync` (v0.7.41.0) already performs, then described with a
  real, unauthenticated Steam Web API call (`ISteamRemoteStorage/GetPublishedFileDetails`) for that
  item's own title and description (Steam's own lightweight BBCode markup is stripped, not rendered —
  a disclosed, deliberate simplification, not a second markup renderer to maintain). Nexus Mods has
  no reliable API without an account/key, and most installed UE4SS/Lua mods aren't Workshop items at
  all — rather than guess at scraping Nexus, MODs with no Workshop match get a manually-set Source
  URL instead; when that URL is a `github.com` repository, its real description is fetched via
  GitHub's own public REST API. Any other URL is stored and shown as a plain link, not force-fetched.
- **Security/scope, decided before writing any fetch code**: every fetch is triggered only by an
  explicit user click (Fetch/Refresh) — never automatic, never on MOD selection, never on page load.
  Results are cached to disk (`mod-descriptions/` under the manager runtime root) so repeat views
  don't re-fetch. The only outbound calls this makes are read-only, unauthenticated GET/POST requests
  to Steam's and GitHub's own public REST APIs — no credentials sent, no write access requested.
- Backend: `HeadlessModManagementService.GetModDescriptionAsync`/`SetModDescriptionSourceAsync`, two
  new routes (`GET /mods/{type}/{package}/description`, `POST /mods/{type}/{package}/description/source`).
  Desktop: `ModDescriptionResultDto`, two new `IMystTiqApiClient` methods, a DESCRIPTION section added
  to the MOD DETAILS panel (Fetch/Refresh button, Source/Title/Description/link display, a Source URL
  text box for the manual-source path) — no other page touched.

## v0.7.54.0 — Per-Page Title Background Artwork

- Item 8: "Each page's title area could have its own background image" — confirmed directly earlier
  in this session to mean literal illustrated/photo artwork per page, not a subtle tint, needing its
  own scoping pass before implementation.
- **A genuine capability gap, resolved by testing an actual option rather than guessing**: this
  session has no built-in image-generation model and no API access to one. Tested whether the
  Browser pane could drive a free, publicly-accessible AI image generator instead — Bing Image
  Creator (Microsoft's DALL-E-backed tool) turned out to work completely anonymously, no sign-in
  required, for a limited number of generations per day. Produced two sample images first (Home,
  World) for sign-off on style direction before committing to a full run.
- **Style**: dark navy atmospheric fantasy landscape banners with silhouetted castle/tower motifs on
  layered low-poly mountain ridges, a subtle glowing tech circuit-line grid overlay, and an
  accent-colored glow matching each category's existing accent hue from v0.7.48.0's theme system —
  deliberately matched to the visual language the existing `dashboard-atmosphere-v3.png`/etc. assets
  already established, confirmed by inspecting that asset directly before writing any prompts. A
  Light-mode variant (bright pastel dawn sky, same motifs, softer glow) was generated alongside each
  Dark one.
- **Hit a real, hard limit partway through**: Bing's anonymous/guest session enforces a daily
  generation cap. After 4 successful images (Home Dark/Light, World Dark/Light), further attempts
  returned "You've reached today's guest creation limit." Continuing would have required signing
  into a Microsoft account — not something this session will do under any circumstances, since
  entering account credentials is outside what's ever done regardless of permission.
- **Scope decision, made directly rather than guessing at a workaround**: ship the two categories
  that do have real, generated artwork now; leave the other five (Server, Backups, Mods, Tools,
  System) with no page-header art at all, exactly as today, rather than filling the gap with a
  code-generated gradient/tint that would contradict the original finding's own "not a tint"
  instruction. The set is deliberately real-but-incomplete rather than complete-but-fake.
- Implementation: `ConnectionProfile`/`ThemeCatalog` untouched — this only needed two categories'
  worth of new state. Four new computed bools on `MainWindowViewModel`
  (`IsHomePageArtDark`/`IsHomePageArtLight`/`IsWorldPageArtDark`/`IsWorldPageArtLight`, combining the
  existing `IsV5HomeCategory`/`IsV5WorldCategory` category checks with `IsLightMode`), re-raised
  after both page navigation and theme-variant changes. Four new `Image` elements layered behind the
  existing page-header content in `MainWindow.axaml`, each gated by one of those bools, at low
  opacity (0.3) so the title/subtitle text stays legible on top.
- Assets added: `page-art-home-dark.jpg`, `page-art-home-light.jpg`, `page-art-world-dark.jpg`,
  `page-art-world-light.jpg` under `MystTiq.Desktop/Assets/`, picked up automatically by the
  existing `AvaloniaResource Include="Assets/**"` glob — no `.csproj` change needed.

## v0.7.53.0 — Dashboard Layout Density

- Item 13: the Dashboard should be laid out more like an older MystTiq build (v0.2.16.4 reference
  screenshot), with smaller/denser stat cards — grounded in the findings log but never actually
  assigned a version until v0.7.32.0's own implementation flagged the gap and appended it as its own
  future item.
- Same treatment v0.7.32.0 already applied to Server Setup/Backups/MOD Library/Server Doctor:
  explicit per-instance `Padding`/`FontSize` overrides, no changes to the shared `.card`/`.statuscard`
  styles themselves (so nothing on any other page is affected). The two 4-column stat-card `Grid`s
  (SERVER/ACTIVE WORLD/BACKUP/OVERALL HEALTH, and PLAYERS/GUILDS & BASES/MOD PLATFORM/CPU-MEMORY),
  the World Pulse strip, and the outer page's own `Spacing` all tightened — `Padding="10"` → `"8,6"`
  throughout, big-number `FontSize` reduced (18/17/16 → 15/14/13), `ColumnSpacing`/`Spacing` reduced
  by ~2px in each card.
- **Deliberately out of scope**: the same v0.2.16.4 reference also showed structural differences —
  ribbon buttons inline in the title bar instead of a separate row, and a collapsible-tree nav
  instead of today's category-tabs-plus-fixed-list — neither of which is a density change, both
  meaningfully larger changes than this item's own "smaller/denser cards" framing. Left untouched.

## v0.7.52.0 — "+" Flow Restructure

- Item 53: the "+" (add tab) flyout has, since it was built, had exactly one generic entry —
  "Set Up New Server" — that led into a wizard whose own first step then asked Local vs. Remote.
  Restructured into four explicit top-level choices, per that finding's own instruction to reuse
  existing wizard/clone commands rather than rebuilding them:
  - **Set Up New Server** — unchanged, still lands on the wizard's step-0 Local/Remote choice cards.
    Kept as the generic/exploratory entry point deliberately: investigated whether "install a fresh
    local server" and "connect to an already-running one" are actually different code paths in this
    wizard, and confirmed they are not — both resolve to the identical Local sub-view
    (`ChooseLocalConnection`/`DetectLocalServiceAsync`), so there was nothing architecturally
    distinct to split this entry into beyond framing text.
  - **Connect to Local Server** / **Connect to Remote Server** — new top-level entries that jump
    straight past the step-0 choice cards, calling the existing `ChooseLocalConnection`/
    `ChooseRemoteConnection` methods directly (unchanged) with tailored `Detail` framing text for
    each ("Connecting to an already-running local server." / "...remote MystTiq server.").
  - **Clone a Server** — new. Routes to the existing Fleet page's Clone World card
    (`NavigateCommand.Execute("Fleet")`), on whichever open tab is an already-connected local
    profile. Only shown in the flyout when one exists (`HasCloneableLocalTab`) — cloning has nothing
    to clone *from* without a connected local source, confirmed via `CloneWorldCommand`'s own
    existing `CanExecute` (`ManagementApiConnected`) and `BootstrapLocalCommand`'s `IsLocalProfile`
    gate, both already local-only. Matches the existing pattern of only adding a "Connect to
    {profile}" entry when a qualifying profile actually exists, rather than showing a permanently
    disabled item.
- The existing "Connect to {profile.Name}" list (per already-saved profile not currently open in a
  tab) is unchanged and still appears below the four new entries — a distinct concern (reconnecting
  to something already set up) from all four of the above (which each start a fresh wizard flow).
- No new backend logic anywhere in this version — every new flyout entry is a thin wrapper around
  methods (`ChooseLocalConnection`, `ChooseRemoteConnection`, `NavigateCommand`) that already
  existed and were already correct.

## v0.7.51.0 — Per-Tab Color Coding

- Item 2 from the original findings log: "Each tab should have an associated color that tints the background to match, so it's obvious at a glance which server you're working on." Resolved the three open questions the finding left unanswered, during implementation, per that finding's own instruction:
  - **Auto-assigned vs. user-assigned**: auto-assigned, round-robin over a fixed 10-color palette keyed by how many profiles already exist at creation time — no new settings UI, works immediately for every existing and future tab.
  - **Which surface tints**: a small colored stripe in the tab strip itself (column 0 of the tab's `Grid`), not the nav pane or main content area — those surfaces already carry *page*-identity meaning from v0.7.48.0's accent system (Server=blue, World=cyan, etc.), and layering a second, *server*-identity color on top of the same surfaces would make the two signals indistinguishable. The tab strip was the one surface not already claimed by page identity.
  - **Interaction with the existing health-based `StatusDotColor`**: kept fully separate, not merged — the stripe is server identity (fixed per profile), the dot is live health (green/amber/red/grey). Both render side by side in the same tab.
- New `ConnectionProfile.AccentColorKey` (optional, defaults to `"Blue"` for backward-compatible JSON deserialization of profiles saved before this version) and `ThemeCatalog.TabIdentityColorNames` — reuses the same 10 base color names (and their already-derived `{Name}AccentBorderBrush` resources) the v0.7.48.0 derivation engine already produces, rather than inventing a second color system alongside it. `"Neutral"` is excluded from the assignable pool — it's a deliberately muted grey tone meant for passive status decoration, not a distinct-enough identity marker.
- The color is assigned **once**, at profile creation, and persists across reconnects/edits: `BuildProfileFromEditor`/`BuildProfileFromTab` both construct a brand-new `ConnectionProfile` record on every save (an existing pattern, unrelated to this feature), so a new `ResolveAccentColorKey` helper looks up the already-known profile's own color by id first and only assigns a fresh one for a genuinely new profile — otherwise every edit or reconnect would silently reshuffle a server's color.
- `TabSession.AccentBrush` resolves the key to the actual live theme resource via `Application.Current.TryGetResource`, not `{DynamicResource}` in XAML — because it's consumed through a regular data binding (per-tab, from a dynamic `ItemsSource`), it does not auto-refresh when `ThemeApplier` rewrites the underlying resource value on a theme switch the way a real `{DynamicResource}` would. Fixed by having `MainWindowViewModel` explicitly re-notify every open tab (`RefreshTabAccentVisuals`) right after each of the two existing `ThemeApplier.Apply` call sites (accent theme change, Dark/Light toggle) — the same "manual refresh after a resource rewrite" pattern this codebase already needed to establish, just applied to a new consumer.

## v0.7.50.0 — Console Source Completeness (UE4SS.log)

- Found while scoping the next roadmap item (Native Console Capture, the DLL-proxy project) — not from the original findings log. Before starting the much larger native-code project, checked one of its own load-bearing assumptions directly against real data: `HeadlessMonitoringService.ResolveConsoleSources` has, since at least v0.7.46.0's own investigation, included a `"Pal.log"` source with the comment "where PalServer/UE4SS write this exact kind of native output" — that claim was never actually verified against a real server file, only inferred.
- **Verified live, directly against this machine's real, actively-modded local Palworld install** (`C:\GameServers\Palworld\Server`, documented in project memory as real production data, not a test fixture): a recursive search of the entire `Pal/Saved/Logs/` tree for `Pal.log` found **zero matches**. Also searched the whole server install tree for any non-MystTiq `*.log` file: found only PalDefender's own logs (a separate anti-cheat mod) and `ue4ss/UE4SS.log` — no `Pal.log` anywhere. This is consistent with, and reinforces, the v0.7.46.0/v0.7.47.0 finding that `-ABSLOG` also produces no file: Palworld's Windows dedicated server build appears to have no working text-log output for the base engine at all, on this real install.
- **`ue4ss/UE4SS.log` does exist and has real, substantial content** — 1947 lines from the current session alone (hook registrations, mod load order, `[AdminCommands]` command registration, Lua mod startup) — but `ResolveConsoleSources` never looked for it. Added it, plus its legacy-layout equivalent path (`RuntimeBinaryRoot/UE4SS.log`), to the source list.
- **Verified the fix live and read-only**, via a throwaway console harness (not part of the shipped codebase) that pointed `HeadlessMonitoringService` at the real server's `Pal/Saved/Logs`/`ue4ss` paths — genuinely reading real production files — while redirecting `ManagerRuntimeRoot` to an isolated temp directory so nothing touched the live production MystTiq instance's own runtime state (per this project's standing "never bind a test instance to the production port/state" convention). Confirmed: `GetLogTail` now reports `UE4SS.log` as one of its merged sources, and real UE4SS content appears in the tail.
- **A second, larger real finding surfaced as a side effect, not new code**: the already-existing "AdminCommands server log" source (unchanged by this fix) turned out to carry genuine player connect/disconnect narration with real player names and timestamps ("Wade ... has connected", "Melly ... has disconnected") — exactly the kind of content the original v0.7.46.0 bug report ("console doesn't capture events") was asking for. This was always technically reachable but was easy to miss when the console otherwise looked thin; adding UE4SS.log alongside it makes the overall console meaningfully richer without any further code change.
- No Desktop changes needed — the Console page already consumes `GetLogTail` unchanged; this is a pure server-side source-list fix.

## v0.7.49.0 — UE4SS Install/Rollback

- Next roadmap item per the approved plan, resuming item 45's other half — v0.7.47.0 shipped listing-only real release data for the UE4SS page's "Release source" dropdown; this is the actual install path. Mid-session, the user asked to defer the remaining ~240 hardcoded "glass" gradient colors from v0.7.48.0's theming pass to the very end of the 0.7.x line (v0.7.57.0+) rather than doing them next — the plan file was renumbered accordingly and this item moved up to fill v0.7.49.0.
- **Verified live against real release zips from both catalog sources before writing any install code, not guessed** — downloaded and inspected the actual zip contents of `Okaetsu/RE-UE4SS`'s `UE4SS-Palworld-g2281fa31.zip` and `UE4SS-RE/RE-UE4SS`'s `UE4SS_v3.0.1.zip`:
  - The Palworld Fork packages the **modern layout**: `dwmapi.dll` at the zip root, everything else (`UE4SS.dll`, `UE4SS-settings.ini`, `UE4SS_SDK_Backends/`, `LICENSE`, `MemberVariableLayout.ini`, `Mods/`) nested under a top-level `ue4ss/` folder — a 1:1 match for `Ue4ssRoot`/`Ue4ssModsRoot`.
  - The Official Upstream packages the **legacy flat layout**: `dwmapi.dll`, `UE4SS.dll`, `UE4SS-settings.ini`, `Mods/`, plus `Changelog.md`/`README.md`, all directly at the zip root — no `ue4ss/` subfolder at all, a 1:1 match for `RuntimeBinaryRoot`/`LegacyUe4ssModsRoot`.
  - **This was a genuine, previously-unconfirmed discovery** — the two sources are not the same file layout with different version numbers, they're fundamentally different packaging conventions. Guessing a flatten/nest transform between them without this verification would have risked silently corrupting a live install.
- **Design decision made directly from that evidence**: rather than force every release into one canonical layout (which would require an unverifiable transform for whichever source doesn't natively use it), install faithfully mirrors whatever the selected zip's own root structure already is onto the server's binaries folder — exactly what each project's own install instructions already tell a user to do by hand (extract to `Pal/Binaries/Win64`). Zero guessed transformation logic.
- **A real, disclosed safety gap this uncovered**: the fork's own release notes warn that having a workshop-installed UE4SS and this fork's copy both present "WILL crash due to attempting to load two copies of ue4ss at the same time." Since install now faithfully mirrors each zip's native layout rather than normalizing to one, installing a modern-layout release over an existing legacy-layout install (or vice versa) would leave both in place. Install now detects this via `Directory.Exists(Ue4ssRoot)` vs. legacy marker files (`UE4SS.dll`/`UE4SS-settings.ini` directly under `RuntimeBinaryRoot`) and refuses with a clear message rather than silently creating a broken mixed state.
- **Mod state is never touched, by construction**: any zip entry whose path contains a `Mods` segment at any nesting level (`Mods/` for the legacy layout, `ue4ss/Mods/` for modern) is skipped outright during install, so `mods.txt`/`mods.json` and every installed MOD folder are untouched — a rollback restores only the files an install actually wrote. `UE4SS-settings.ini` is skipped (not overwritten) if it already exists on disk, since it's commonly hand-edited — matching UE4SS's own real v3.0.1 release notes' framing of "replacing files while preserving custom configuration settings."
- New `HeadlessModManagementService` methods follow the same token-based Preview-then-Apply shape already established by backup retention cleanup (`PreviewRetention`/`ApplyRetentionAsync`), reused rather than inventing a new pattern:
  - `PreviewUe4ssInstallAsync(source, tagName)` re-resolves the selection against the live GitHub catalog server-side (the client never sends a raw download URL to the server, closing an SSRF-shaped gap before it existed) and returns a 15-minute token plus a plain-language summary of what will happen.
  - `ApplyUe4ssInstallAsync(token)` requires the exact token, requires PalServer stopped (its DLLs are loaded into the running process — installing over a locked file would fail or corrupt), downloads the release zip (200 MB cap, 3-minute timeout), takes an automatic snapshot of only the exact files about to be overwritten (not a whole-directory backup — precise and symmetric with what Rollback restores), then extracts.
  - `RollbackUe4ssInstallAsync()` restores that same snapshot on demand; also fires automatically if `ApplyUe4ssInstallAsync` fails partway through, so a failed install can't leave a half-overwritten engine on disk.
- New routes: `GET /ue4ss/install/status` (rollback availability), `POST /ue4ss/install/preview`, `POST /ue4ss/install/apply`, `POST /ue4ss/install/rollback` — the latter three gated `RequireRole(Admin)`, matching this route group's other install/mutation-class endpoints.
- Desktop: the UE4SS page's release list is now a selectable `ListBox` (`SelectedUe4ssRelease`) instead of a read-only `ItemsControl`. Three new ribbon buttons — Preview Install, Confirm Install, Rollback — replace the permanent "Installing a specific release isn't wired up yet" stub from v0.7.48.0; a status line shows the live preview summary or the last install/rollback result. Changing the selected release invalidates any outstanding preview token, so Confirm Install can never fire against a preview that no longer matches what's selected.
- **Exercised live, not just built** — per the plan's own instruction for this item ("needs... live isolated-copy verification... before ever touching a real server"), a throwaway console harness constructed `HeadlessModManagementService` directly against a fake path profile pointed at a temp directory and ran the real preview/apply/rollback code path against real GitHub release data (not mocked): fresh modern-layout install, fresh legacy-layout install, `UE4SS-settings.ini` preservation, and the layout-mismatch refusal all confirmed working — 21/21 assertions passed. Not yet run against a real, already-populated Palworld server install; disclosed in the architecture doc.

## v0.7.48.0 — Central Theme System Completion, Foundation Pass

- Direct live bug report, jumped ahead of the roadmap queue at the user's direct request: "we need to fix the different themes... have a central place where a variable can be changed that sets the theme colour so that the background and buttons all change to that setting. Right now it looks like some of it changes but not all. Same with the light [mode]... text colour may need to change, background colours and images need to change."
- **Small, quick fix folded in first, per direct request**: Update Center's UE4SS row was still reading the fork's old, now-superseded release tag following v0.7.47.0's own discovery that the fork changed release strategy — fixed to always fetch the full release list and pick whichever was published most recently, matching `HeadlessUe4ssReleaseCatalogService`'s own logic.
- **Investigated before writing any code**: a real central theme mechanism already existed — `ThemeCatalog.cs` defines every color per (accent theme x Dark/Light variant) combination, `ThemeApplier.Apply()` writes them all into `Application.Current.Resources` at startup and on every theme change, and every XAML consumer bound via `DynamicResource` picks up the change live. The bug was never architectural — it was incomplete wiring. A full read of both `MainWindow.axaml` (2636 lines) and `Styles/DesignSystem.axaml` (1389 lines) found 26 hardcoded colors in the former and roughly 300 in the latter that never touched this mechanism at all.
- **Real finding that reframed the whole scope**: most of DesignSystem.axaml's ~300 hardcoded colors are not arbitrary one-offs. They're the "card flare" per-page accent system (`accentHome`/`accentServer`/`accentWorld`/`accentBackups`/`accentMods`/`accentTools`/`accentSystem`, v0.7.4.0) and status glows (`glowGreen`/`glowRed`/`glowAmber`/`glowPurple`/`glowDarkGreen`/`glowCyan`/`glowViolet`), and their BoxShadow/BorderBrush literals already, by construction, duplicate `ThemeCatalog`'s own Accent/Semantic hex values (e.g. `accentServer`'s glow is literally `#35D5E8`, this catalog's own Cyan) — just hardcoded per-instance instead of referenced, so they never moved when a theme or variant changed.
- **Scope decision, asked and answered directly**: per-page accent tints now shift when the user picks a different accent theme (Emerald/Crimson/Violet), not just when switching Dark/Light — the larger of two options offered, since the smaller one (tints stay fixed) would have left the app's page-identity colors permanently locked to the Default theme's palette.
- **A real, previously-undiscovered fix to a disclosed limitation**: `ThemeCatalog`'s own header comment said BoxShadow's shorthand-string syntax "cannot bind to DynamicResource at all (a hard Avalonia limitation, not an oversight)" and that decorative glow colors would "stay at their current dark-tuned values in every theme/variant." Researched this directly: the shorthand-string limitation is real, but binding the *entire* `BoxShadow` attribute to a resource of type `BoxShadows` is not the same thing and works fine — confirmed by writing real `Avalonia.Media.BoxShadows` objects into `Application.Resources` from C# (`ThemeApplier`) and referencing them via `BoxShadow="{DynamicResource X}"` in XAML, which compiled and is now live. `DropShadowEffect.Color` was never actually blocked either — it's a normal bindable property on a normal XAML object, not the restricted shorthand string; the two had been conflated.
- **Given ~300 individual hand-tuned colors x 4 themes x 2 variants is not something that can be manually tuned with any confidence without being able to see the rendered result** (no way to screenshot/render the native Avalonia app in this environment), built a systematic, formula-driven derivation engine instead of hand-authoring every combination:
  - New `ThemeColorMath.cs`: `Blend`/`Lighten`/`Darken`/`WithAlpha` helpers, simple linear RGB math.
  - `ThemeCatalog.DerivedColorNames` + `ResolveDerivedBaseColor`: 11 named base colors (the 5 existing Accent slots, 3 existing Semantic colors, "Purple" as a confirmed exact alias of Violet, and two with no natural existing slot — "DarkGreen," derived as a darkened Green, and "Neutral," blended from the existing Border/Muted structural tones).
  - `ThemeApplier.Apply()` now also computes, per base color, per theme, per variant: `{Name}AccentBorderBrush` (a muted border tint), `{Name}AccentHighlightBrush` (a bright hover/focus/checked highlight — confirmed these need a different, lighter derivation than the muted border tint, not the same resource reused for both), `{Name}AccentGlowShadowLow`/`{Name}AccentGlowShadowHigh` (two BoxShadows strengths, for passive accent decoration vs. active status signaling), and `Card{Name}Gradient` (a 3-stop tinted glass-card gradient, dark-blended for Dark mode and light-blended for Light mode, mirroring `ThemeCatalog.Structural`'s own existing Card/CardStrong Dark-vs-Light approach). The `Card{Name}Gradient` resources overwrite the exact same keys (`CardGreenGradient`, `CardCyanGradient`, etc.) `DesignSystem.axaml` already statically declares, using the same Application-resource-wins-over-StyleInclude-resource precedence every other resource in this system already relies on — no XAML reference needed to change.
- **What actually shipped this version** (the highest-visibility, most-repeated elements first): the full 7-page accent-family system; every status glow class; category-tab checked/hover/pointerover colors (and a real inconsistency fixed along the way — Server/System categories were using their own one-off near-cyan hex values instead of reusing the same Cyan/Green every other category already reuses from its own page accent); all 5 `ribbonGroup.contextXxx` classes; status badges (verified/enabled/healthy/update); MOD Dashboard/Library's accent cards (modPlatform/modRuntime/modUpdates/modDetails); Backups' three accent cards; the general interactive hover/focus glow used throughout buttons, tabs, and nav (12 more literal instances of the app's bright `#91E4FF`/`#75C8FF`/`#72D0FF` highlight, unified into one derived resource); and all of `MainWindow.axaml`'s 26 hardcoded structural literals (borders, card backgrounds, status badges, table headers, dividers) — 23 converted, 3 deliberately kept as a disclosed exception (the Dashboard/RCON/Console monospace log views stay dark/green regardless of theme, matching the common "terminal panel stays dark" convention many apps use, rather than risking a light-background/dark-green-text contrast problem with no way to visually verify the result).
- **A real, separate gap closed along the way**: the v0.7.35.0 button color system (`inspectAction`/`targetAction`, item 32) added its own `InspectGradientStop0-2`/`TargetGradientStop0-2` resources directly as static `DesignSystem.axaml` declarations and never wired them into `ThemeCatalog` at all — meaning they were never audited by v0.7.36.0's original Theme System Audit either. Added with Dark values byte-identical to what was already hardcoded (no change to today's look) and computed Light variants, fixed across all four accent themes (matching how Success/Warning/Danger already work) since these represent a fixed action-type meaning, not page identity.
- **What's explicitly deferred, disclosed rather than silently dropped**: ~240 hardcoded colors remain in `DesignSystem.axaml`, overwhelmingly the elaborate multi-stop translucent "glass" gradients (`GlassOptionHoverGradient`, the `PrimaryGlass*`/`SuccessGlass*`/`DangerGlass*` families, the four `Nav*Glass*Gradient` variants, `DashboardGlassGradient`, the `Context*Gradient` family, the `*GraphFill` family, the `Backup*Gradient` family) plus a handful of smaller one-offs. These behave differently over a light vs. dark background in ways a simple lighten/darken formula can't safely reproduce without visual verification this environment cannot perform — tracked as its own follow-up version rather than risking a blind mass conversion. Nav pane icons (56x56 pre-rendered glossy sphere PNGs) and background/atmosphere art (`dashboard-atmosphere-v3.png` and others) were directly confirmed as part of what "images" meant, but are raster assets with no vector source — real theme-awareness for them needs asset regeneration or runtime hue-shifting, a different kind of work than this version's color-token plumbing, and is disclosed as out of scope here rather than attempted.
- **Real-world consequence, disclosed**: since `ThemeApplier.Apply()` runs unconditionally on every app start (confirmed in `App.axaml.cs`), the newly-computed derived resources replace today's hand-tuned static values immediately, for every user, including the Default theme — a deliberate, disclosed deviation from `ThemeCatalog`'s own stated "Default+Dark stays byte-for-byte identical" principle for the specific ~11 base colors this version touches, since hand-preserving exact byte-identical values for those while still deriving the other 7 (theme x variant) combinations would have needed reverse-engineering the original hand-tuning with more precision than is practical. The computed values are designed to closely match the original spirit (same hue, same dark-tinted-glass structure) — visual differences, if any, should be minor.

## v0.7.47.0 — UE4SS Release Catalog

- The next roadmap item after v0.7.46.0's live bug report was Native Console Capture — the real fix for "console doesn't capture PalServer events." Investigated it live first, in an isolated copy of the user's real server (never touching the live one), before writing any code:
  - **`-ABSLOG="path"`** (a standard, documented Unreal Engine file-logging flag): tested directly — no log file was ever created, even minutes after UE4SS finished loading all 6 of the user's real mods. Palworld's build appears to have this Unreal subsystem compiled out entirely.
  - **Launching `PalServer-Win64-Shipping-Cmd.exe` directly**, bypassing the `PalServer.exe` wrapper, with the exact same redirection MystTiq already uses against the wrapper: tested directly — zero lines captured, despite the process being confirmed alive and UE4SS actively registering hooks and loading mods the whole time. This ruled out "MystTiq is redirecting the wrong process" as the fixable root cause — Palworld's engine appears to allocate and write to its own console object internally, independent of the OS-level stdio handles the process was started with, regardless of which binary launches it or how.
  - **UE4SS's own Lua API**: confirmed via its documentation that no hook exists for Unreal's own engine log output, only UE4SS's own mod-loading diagnostics.
  - **Existing third-party tools**, investigated per direct instruction before proposing to build anything new: "PalServerLogger" is a real, working solution, using a DLL proxy technique (`d3d9.dll`) to inject into the PalServer process. Not safely integrable: it requires PalDefender (a separate anti-cheat mod) already installed as its own proxy base, and its license/redistribution terms couldn't be confirmed (Nexus Mods blocked automated access). Bundling an unverified-license binary that gets injected into the user's live game process isn't something to do without the user explicitly choosing that specific tool.
  - **Conclusion**: the real fix is a small, first-party DLL proxy MystTiq builds and owns itself — the same proven technique UE4SS/PalDefender/PalServerLogger already use, applied by MystTiq's own code instead of borrowed from an unverified third party. Genuinely large — needs a C++ toolchain this otherwise-all-.NET project doesn't have yet, real Win32 DLL-proxy code, and the same careful isolated testing used to rule out the three faster options above. Scoped in the roadmap as its own future project (v0.7.49.0+), not attempted here.
- **Picked up the next roadmap item instead**: item 45, the UE4SS page's "Release source" dropdown, which has been a client-side-only stub ("Palworld Fork"/"Official Upstream" options, no backend) since it was first built. New `HeadlessUe4ssReleaseCatalogService` gives it real data for the first time.
- **Grounded live against real GitHub release data for both repos before writing any code**: `Okaetsu/RE-UE4SS` (Palworld Fork) turns out to have exactly 2 releases — a legacy rolling `experimental-palworld` tag from 2025-02-20, and a new-style discrete `2281fa31` release from 2026-09-03. The maintainer switched release strategy recently ("Future releases will be a new release instead of updating the old one") — which also means v0.7.45.0's Update Center UE4SS row, which reads the old `experimental-palworld` tag by name, is now looking at stale data and should be revisited. `UE4SS-RE/RE-UE4SS` (Official Upstream) has a real, proper multi-release history (v2.5.1 through v3.0.1, plus an experimental-latest prerelease).
- **Real asset-selection logic, verified against both repos' actual (and differently-styled) asset sets**: each release attaches multiple downloadable files — a "developer" build (`-zDev`/`zDEV-` in the name) alongside the real one, and for the official upstream repo, unrelated extras (`zCustomGameConfigs.zip`, `zMapGenBP.zip`). The correct asset is chosen by a simple, verified rule (contains "UE4SS" in the name, does not contain "dev") — confirmed against both real release's actual asset lists before shipping, not guessed.
- New fleet-level route `GET /api/v1/ue4ss/releases` (`HeadlessUe4ssReleaseCatalogService`, no per-profile state needed — it's a pure, read-only GitHub query, registered the same way as the existing port-check diagnostic route). Wired into the UE4SS page's existing "Refresh Runtime" ribbon button, gated to only fire the extra GitHub call while actually on the UE4SS page.
- **Deliberately listing-only.** Real complexity surfaced while inspecting the actual release zip's contents against the user's real installed UE4SS layout: the zip's own `ue4ss/Mods/` folder contains UE4SS's bundled *default* mods and `mods.txt`/`mods.json` — a naive "extract the zip over the existing install" would silently overwrite the user's real enabled-mod state. The user's real server also has UE4SS's legacy root-level file layout coexisting with the modern `ue4ss/` subfolder layout, matching a warning MystTiq's own detection already logs ("Both modern and legacy... exist. Modern root is active."). A correct installer needs to target only the active layout's own core engine files, never `Mods/` or an already-customized `UE4SS-settings.ini` — real, separate work, deferred to its own version (v0.7.48.0+) rather than rushed in here.

## v0.7.46.0 — Console Newest-First Ordering

- Direct live bug report against the user's own real running server, not from the original 57-item findings log: "console still does not capture events from palworld server file. Also does not connect to the server backend, tells me operation failed."
- **Investigated live, against the real server, read-only, no destructive actions taken without explicit authorization**: confirmed via the running instance's own `/healthz`, `/api/v1/config`, `/api/v1/status`, and `/api/v1/rcon/status` routes that the management API itself was completely healthy and reachable — "operation failed" was not a connectivity problem.
- **Root cause found and fixed live**: the "default" profile's `LaunchArguments` never included `-port=8211` (a gap dating back to before v0.6.10.0's discovery that `PalWorldSettings.ini`'s `PublicPort` setting does NOT control PalServer's actual UDP bind port — only the `-port=` CLI argument does; that fix was applied to the Clone World feature at the time but never backported to the pre-existing default profile). Confirmed via `Get-NetUDPEndpoint` that the real running `PalServer-Win64-Shipping-Cmd.exe` was bound to UDP 27015 (Steam's default fallback), not 8211 — meaning MystTiq's own readiness check, which only ever polls 8211, could never succeed, so every Start/Restart timed out after `StartupTimeoutSeconds` (90s) and surfaced as "Operation failed," even though the process was alive and healthy the whole time. This may also have affected real player connectivity on the advertised port. Fixed by adding `-port=8211` to the profile's launch arguments via a live `PUT /api/v1/config/editable` call against the user's running instance, verified by re-reading the persisted `mysttiq.json` afterward. Applying the fix (restarting the MystTiq backend, then PalServer itself) was left to the user's own timing, since it's their live server with real players — no restart was performed without their go-ahead.
- **Root cause found, not yet fixed**: Console not capturing ongoing PalServer/UE4SS output. Inspected the real `MystTiq-PalServer-Console.log` spanning Aug 27–Sep 10 (13+ days, many restarts): every single session captures the initial UE4SS/MOD-load startup banner (~20-30 lines) and then goes completely silent for the rest of that run, no matter how many hours the server stayed up. Cross-checked against `UE4SS.log`, which stops updating at the exact same moment — both point to the same cause: MystTiq redirects stdout/stderr only from `PalServer.exe`, the thin wrapper process it directly launches via `Process.Start`, but the actual ongoing engine/gameplay output happens in `PalServer-Win64-Shipping-Cmd.exe`, a separate grandchild process with its own console (already documented, but never connected to this symptom, in this codebase's own `ApplyPostLaunchWindowPolicyAsync` comment about that process having an uncoverable window). This closes out item 14's original "needs a live check against an actual modded, running server" disclosure with real findings — the actual fix (capturing the grandchild's own output) is real feature work, tracked as its own future version rather than rushed in here.
- **What shipped in this version**: while confirming the port fix on the Console page, the user asked for the log view to show the most recent activity at the top instead of the bottom. `MainWindowViewModel.ApplyConsoleFilter` now builds `FilteredLogLines` from `LogLines.Reverse()` — the underlying `LogLines` collection keeps its original chronological (oldest-first) order unchanged, since the Dashboard's Live Activity panel (`DashboardActivityLines`) still reads from it directly and has its own unrelated ordering. Export Console now exports newest-first too, since it exports exactly what's on screen — intentional, not an oversight, so the exported file matches what the user was looking at.

## v0.7.45.0 — Update Center Overhaul

- Twenty-first version of the GUI/workflow/UX overhaul roadmap — item 51, the largest single item in the roadmap. Grounded before writing any code: `HeadlessServerDistributionService`/`HeadlessEnvironmentChecklistService` only ever checked component *existence* (does the file/folder exist), never a version number or whether a newer one was available, for any of the 11 components the v0.2.16.4 reference tracked.
- **Explicit scope decision, asked and answered mid-session**: build all 11 components in one pass (not a smaller starter slice), and include MystTiq's own self-update awareness against its GitHub releases — informational only, no auto-download/auto-install.
- **New `HeadlessComponentUpdateService`** (`MystTiq.HeadlessHost`) returns real installed/latest version data per component, grouped exactly like the reference: **Core Server** (MystTiq Server Manager, SteamCMD, Palworld Dedicated Server, UE4SS Runtime) and **Save & Runtime Dependencies** (Python Runtime, pip, Palworld Save Tools, PlM/Oodle Decoder, .NET Runtime, Visual C++ Runtime, Microsoft C++ Build Tools).
- **Real, verified sources per component, researched before committing to any of them** — no guessed endpoints: MystTiq's own updates via `GET /repos/Wad3M/MystTiq-Palworld-Server-Manager/releases/latest`; Palworld Dedicated Server's installed build id read from its own SteamCMD `appmanifest_2394010.acf`, compared against Steam's unauthenticated `ISteamApps/UpToDateCheck` Web API (no API key needed); UE4SS's already-tested `HeadlessModManagementService.GetInventoryAsync().Ue4ss.InstalledVersion` (reused, not duplicated) compared against the Palworld fork's `Okaetsu/RE-UE4SS` `experimental-palworld` GitHub release tag; pip and Palworld Save Tools compared against their real PyPI package feeds; .NET Runtime compared against the official `dotnet/core` `releases-index.json`.
- **Real, disclosed capability gaps instead of fabricated numbers**: Python Runtime, Visual C++ Runtime, and Microsoft C++ Build Tools have no simple, reliable unattended feed for "latest version" anywhere from their vendors — confirmed by research, not assumed — so those rows report the real installed version with Latest = "Unavailable" and say why, rather than inventing a comparison. PlM/Oodle Decoder has no single canonical upstream project at all (this project vendors it under its own internal folder naming, not a specific tracked repo) — same honest treatment. UE4SS's Palworld fork ships as a rolling re-pushed tag rather than an incrementing version number, so it reports both values with a "Check manually" status instead of a false Up-to-date/Outdated claim. SteamCMD has no version concept at all (it self-updates on every launch) — reported as such, not given a fake number.
- **UI**: new component-by-component table on the Update Center page (`MainWindow.axaml`), split into the same two labeled sections, color-coded by status (green Up to date, amber Update available, red Not installed, muted for every honestly-disclosed "can't compare" case). The existing Refresh ribbon button now also pulls this table, alongside the pre-existing distribution status it already refreshed.
- No change to any existing update *action* — the existing "Update Palworld Server" SteamCMD button is untouched; this release is read-only version reporting only, no new mutation surface.

## v0.7.44.0 — Palworld Instance Detection & Termination Tool

- Twentieth version of the GUI/workflow/UX overhaul roadmap — a direct mid-session request, not from the original 57-item findings log: "We need to add a tool to detect palworld instances and be able to close them." Motivated directly by a real collision hit twice during this session's own release verification (v0.7.42.0 and v0.7.43.0's `-RunBuild` gates): an unrelated real PalServer process was running, had no in-app way to be identified or stopped, and had to be handled manually outside MystTiq. The second collision revealed the actual risk this tool has to account for — MystTiq's own crash-recovery auto-restarted the process because a raw `Stop-Process` kill outside MystTiq looks exactly like an unexpected crash, not an intentional stop.
- **New machine-wide instance scan**: `IServerLifecycleService.FindAllInstancesAsync` (Windows and Linux) returns every Palworld process on the machine — not filtered to this profile's own configured `ServerRoot` the way the existing status/kill-processes tooling from v0.7.28.0 already is. Built entirely on the existing `IServerSessionInspector.FindProcessesByName` primitive that `FindManagedServerProcesses`/`FindProcessesWithMismatchedPath` already filter down from — no new low-level process enumeration needed. Each result is tagged `ManagedByThisProfile`, deliberately conservative: an instance with an unreadable/unknown executable path is reported as NOT managed (unlike the permissive rule the existing status display uses for the same edge case), because this flag now gates which stop path the UI offers.
- **Two distinct termination paths, not one blanket kill button**: an instance confirmed as this profile's own managed process reuses the existing, already-safe `ForceStopServerCommand` (→ `StopAsync(TimeSpan.Zero)`, which writes `StopRequested=true` before touching the process, so this profile's own crash-recovery correctly stands down). An instance NOT confirmed as managed gets a new, separate raw-kill route — `IServerLifecycleService.TerminateUnmanagedInstanceAsync` — that terminates the PID directly and deliberately never touches this profile's own persisted lifecycle state, since the target may not be this profile's process at all.
- **The real, disclosed limitation**: MystTiq cannot know in advance whether an "unmanaged" PID is actually being tracked by a different local MystTiq session's own crash-recovery. Killing it directly may look like an unexpected crash to that other session and trigger an auto-restart there, exactly as happened twice this session. The UI states this plainly before the kill button is reachable, rather than presenting a raw kill as a clean, risk-free stop.
- **New Server Doctor panel** ("ALL PALWORLD INSTANCES ON THIS MACHINE"), reusing the list-plus-details-panel pattern MOD Dashboard/MOD Library already established (v0.7.40.0/v0.7.43.0): select an instance to see its details and the one stop action that applies to it. New ribbon button "Detect Instances" (Doctor page); Run Doctor also refreshes this list as a side effect, since it's the same "what's actually running" concern.
- New routes: `GET /server/instances`, `POST /server/instances/{processId}/terminate` (both `RequireRole(Operator)`), intentionally not run through the same `IOperationCoordinator` lock as Start/Stop/Restart/Force Stop — they don't mutate this profile's own lifecycle/world state, and a raw kill of an unrelated instance must not queue behind this profile's own in-progress lifecycle operations.
- No change to any existing route, DTO, or behavior for this profile's own managed-process tracking (`/status`, `/server/force-stop`, the v0.7.28.0 MANAGED PROCESSES panel) — this is new, additive capability only.

## v0.7.43.0 — Findings Completeness Fixes

- Nineteenth version of the GUI/workflow/UX overhaul roadmap — a direct mid-session request: "review the required changes from all my previous commands to ensure they are done... For instance the MOD dashboard." An audit pass cross-checked every one of the 57 original findings against current code (not just the roadmap's own version-to-finding mapping) and found two real gaps, both fixed here.
- **MOD Dashboard never got its own mods list** (items 21 and 40): v0.7.40.0 only ever applied the Installed-MODs-list-plus-details-panel work to MOD Library, despite the original ask — echoed independently by two findings — being "all of the running Mods should be here [on MOD Dashboard] listed like the old version, the Mod on the left and the description on the right." Added a read-only Installed MODs list + MOD DETAILS panel to Dashboard (no Enable/Disable/Delete/Rollback/Update actions, no Install Validated ZIP), matching the page's own pre-existing framing ("Open MOD Library to browse, install, or change enabled state") — Dashboard stays at-a-glance, mutation stays on MOD Library. Evidence surfaces MystTiq's own real evidence, not a website description; that part of item 40 is now separately scoped as its own future version.
- **Footer "working on" indicator never got more detail** (item 6): unlike every other finding, item 6 was never assigned a version slot anywhere in the original 22-version roadmap, and the footer was unchanged from the original ask. Added a live elapsed-time ticker (`BusyElapsedText`, a dedicated 1-second timer started/stopped with `IsBusy`) and which server the busy operation belongs to (`StatusBarText` now includes the active tab's profile name alongside the reason). Real sub-step/percent-complete progress remains a disclosed gap — the original finding itself flagged this as likely needing backend changes too.
- **Nav pane icons doubled again** (28×28 → 56×56, direct mid-session request, the same 25 destinations touched in v0.7.32.0) — the nav row height grew 58→72px and the icon column 32→60px so the larger icon doesn't clip, following the same paired-scaling approach v0.7.32.0 established.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.42.0 — Cross-Page Consistency Sweep

- Eighteenth version of the GUI/workflow/UX overhaul roadmap — a direct mid-session request, not from the original 57-item findings log: apply the same patterns already established on updated pages (ribbon relocation above all) to every page that hadn't received them yet.
- **Requested next as "v0.7.43.0"; shipped as v0.7.42.0** — every version this session has been strictly sequential with no gaps, so the originally-planned v0.7.42.0 (Update Center Overhaul, the largest single item in the roadmap) is deferred to v0.7.43.0+, with UE4SS Release Catalog Backend, Per-Tab Color Coding, "+" Flow Restructure, and Dashboard Layout Density each shifting down one slot accordingly.
- **13 pages got new per-page ribbon groups**, the only ones left with in-page header action buttons after v0.7.26.0-v0.7.28.0's three original ribbon-consolidation passes: World Explorer (Refresh World), World Transactions (Validate, Export Report, Refresh Ops), Guilds/Bases (Refresh Evidence), Players (Discover Saves, Export CSV), Network Diagnostics (Run Diagnostics, Repair Firewall, Restart Server), Save Tools (Refresh, Self-Tests), Notifications (Refresh, Self-Test, Mark All Read, Export), Automation (Refresh), Alert Center (Refresh), Security (Refresh), Fleet (Refresh), Crash Analyzer (Refresh History, Run Analysis), Update Center (Refresh, Preview Plan).
- **Two real bugs fixed along the way, not just relocated**: Network Diagnostics' "Re-run Network Tests" button was a literal duplicate of "Run Diagnostics" — same command, both present at once — removed rather than relocated. Its "Open Server Log" button was redundant with the always-present global ribbon Quick Actions "Console" button — removed too.
- **Deliberately left in-page**: sub-feature-local actions embedded in a labeled workflow card rather than a page header — Fleet's Backup All/Doctor All/Update All, World Transactions' confirm-gated Repair Center apply button, and Alert Center's per-collapsible-section Discord Bot/Anti-Cheat refresh buttons — matching how Backups' Retention Cleanup apply button was kept in-page in the original roadmap rather than relocated. Only page-level primary actions move.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.41.0 — MOD Update Detection

- Seventeenth version of the GUI/workflow/UX overhaul roadmap: item 52 — per-MOD "check for update"/"update" actions in MOD Library, deliberately kept out of the Update Center per that finding's own instruction, rather than duplicating MOD rows into the Update Center table the reference build used.
- **Investigated before building**: confirmed no update-comparison infrastructure exists anywhere in this codebase for any MOD source — Steam Workshop scanning (`ScanWorkshopAsync`) only discovers what's already downloaded locally, never queries Steam's servers for a newer version. Real update detection via a live Steam Web API call is substantial new infrastructure (API keys, network calls, ToS surface) on the same scale as item 45's UE4SS Release Catalog Backend — explicitly deferred in the roadmap as its own multi-version item, not something to improvise inside a UI-overhaul pass.
- **Built what's real instead of faking it**: a MOD matching a locally-scanned Steam Workshop item can be checked against real, on-disk evidence — whether Steam's own local Workshop content cache is newer than what's actually installed (i.e., Steam already silently re-downloaded an update in the background that hasn't been re-imported yet). No network calls, no external dependency, no fabricated "latest version" number. New `HeadlessModManagementService.CheckModUpdateAsync`/`GET /mods/{type}/{package}/check-update`.
- **MODs with no matching local Workshop source** (most UE4SS/Lua mods from other sources) get an honest "no known update source for this MOD" message — matching the app's own established "BACKEND REQUIRED" convention for a real capability gap, not a silent failure or a guess.
- **Update reuses the existing, already-tested Workshop-import route** (`ImportWorkshopItemAsync`) unchanged — no new mutation logic, just a new way to discover that it should run.
- Wired into MOD Library's MOD DETAILS panel (built in v0.7.40.0): Check for Update / Update buttons plus a result text area, scoped to whichever MOD is currently selected.

## v0.7.40.0 — MOD Library Layout & Details Panel

- Sixteenth version of the GUI/workflow/UX overhaul roadmap: covers items 40 (the layout/details-panel part) and 42, plus the "move Install Validated ZIP to the top" note folded into this version's scope.
- **Side-by-side 3-column layout** (item 42): Installed MODs and Available Local Steam Workshop Mods, previously two of three full-width cards stacked vertically, now sit side-by-side matching the v0.2.16.4 reference's layout — each list's row template was trimmed to fit the narrower half-width columns.
- **New MOD DETAILS panel** (item 40's layout half): a third column showing Overall Health, Installation (type/package/path/enabled state), Runtime, Compatibility, and Evidence for whichever MOD is selected in the Installed MODs list. `SelectedMod` previously only targeted the Enable/Disable/Delete/Rollback buttons — nothing rendered its details anywhere. A new `HasSelectedMod` computed property on the ViewModel gates the panel between its content and a "select a MOD" placeholder.
- **Install Validated ZIP moved to the top of the page**, above the lists, instead of being sandwiched between Installed MODs and Available Workshop Mods.
- **Explicitly out of scope**: the website-sourced MOD description part of item 40 — confirmed during the original walkthrough as genuinely new, unbuilt work (mods come from different sources with no single uniform place to fetch a description from, and an outbound fetch from an admin tool deserves its own scope/caching/rate-limiting design pass, not a quick add-on here). The panel's Evidence field surfaces MystTiq's own existing runtime evidence instead.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.39.0 — Mod Install Type Auto-Detection

- Fifteenth version of the GUI/workflow/UX overhaul roadmap: closes a real correctness gap in the MOD install flow.
- **Confirmed gap**: the existing per-row Installed MODs list already correctly distinguished PAK vs. UE4SS for MODs already on disk — that part worked fine. The gap was specifically in installing a *new* MOD: `ModInstallType` was a plain dropdown defaulting to "PAK", sent as-is to the install route with zero inspection of the actual ZIP contents. Forgetting to flip it silently installed a UE4SS/Lua mod as if it were a PAK, or vice versa.
- **Server-side auto-detection**: `HeadlessModManagementService.InstallZipAsync` now inspects the archive after extraction — the presence of any `.pak`/`.ucas`/`.utoc` file anywhere in it is an unambiguous PAK signal (no UE4SS/Lua mod ever ships those); everything else installs as UE4SS. This is authoritative regardless of what the client requested, and the result/activity log both reflect the real detected type, noting when it differed from the request.
- **The manual type dropdown is gone** from MOD Library's "Install Validated ZIP" card — nothing left for a user to get wrong. The client still sends a fixed "PAK" hint on the wire, unchanged from today's prior default, used only by the pre-existing `CaptureSnapshot` rollback-safety check (which looks for a same-named mod already installed under that type before overwriting) — install correctness itself no longer depends on it at all.
- No configuration schema change.

## v0.7.38.0 — Configuration Page Fixes

- Fourteenth version of the GUI/workflow/UX overhaul roadmap: covers items 24-28, comparing against the v0.2.16.4 reference screenshot.
- **Notification strip relocated** (item 24): the preset-loaded message, "N unsaved setting change(s)", validation text, and the "Simple Settings shows…" helper line moved from directly under the Simple/Advanced toggle row down to sit just above World Settings — next to the section they're actually reporting the state of. The file path and the Advanced Settings helper line stayed where they were, since they're relevant regardless of which settings section is showing.
- **"Gameplay Rates" renamed to "World Settings" and split into three labeled sub-sections** (item 25): World (Daytime/Nighttime Speed, Experience Rate, Pal Capture/Spawn Rate, Supply Drop Interval, Base Decay Rate), Player & Pal (all player/Pal hunger/stamina/health/damage rates), and Items & Work (drop/respawn/work-speed/durability rates) — replacing one flat 22-row list. Grouping is data-driven: `PalworldSimpleSettingItem` gained a `Group` property, and the former single `SimplePalworldSettings` collection split into three (`SimpleWorldRateSettings`/`SimplePlayerPalRateSettings`/`SimpleItemsWorkRateSettings`).
- **Advanced Settings keeps the same Server Identity and Network/Access & Limits cards Simple view has** (item 26): previously Advanced Settings was "a totally different page layout" — a bare flat OptionSettings table with no identity/network context at all. Both cards are now shared and always visible; only the settings list beneath them differs (World Settings' three groups for Simple, the full flat table for Advanced).
- **Generate button** (item 27): already fixed in v0.7.35.0 (bigger, labeled "🎲 Generate", `inspectAction` styling) — verified nothing further was needed here.
- **Reference note** (item 28): the actual grouping/section design above was cross-checked against the real current curated settings data, not guessed from scratch.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.37.0 — Backups Page Layout

- Thirteenth version of the GUI/workflow/UX overhaul roadmap: three layout fixes to the Backups page, comparing against a v0.2.16.4 reference screenshot.
- **Removed the redundant "N backup(s) / total size" header text** — fully covered by the TOTAL ARCHIVES summary card's own count and size, directly below it.
- **Two-column layout**: the backup table now sits in a wider left column, with the Selected Backup (Verify/Restore/Delete Selected) and Retention Cleanup cards stacked in a narrower right-hand column, instead of one long full-width vertical stack. The reference build's third right-column card, "Backup Locations," has no equivalent to move here — its one action (Open Backup Root) already moved into the ribbon back in v0.7.27.0.
- **Created now leads the backup table's columns**, ahead of the filename (was `Backup, Created, Size, Status`; now `Created, Backup, Size, Status`).
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.36.0 — Theme System Audit

- Twelfth version of the GUI/workflow/UX overhaul roadmap: closes the concrete gap behind a reported bug — with Light mode enabled, the main content area lightened correctly but the title bar and footer status bar stayed dark navy.
- **Root cause confirmed as two separate, distinct issues, not one**: `ThemeCatalog`'s own header comment already discloses that `BoxShadow`/`Effect` colors on individual style selectors are deliberately out of scope (Avalonia's `BoxShadow` shorthand string syntax cannot bind to `DynamicResource` at all — a real platform limitation). That disclosed gap is unrelated to what was actually broken here: the title bar and footer status bar `Border`s were both using a **hardcoded** `Background="#F1081422"`/`BorderBrush="#28445D"` rather than any theme resource at all, while the nav pane container right beside them correctly used `DynamicResource`.
- **Fixed by reusing already-defined, already theme-registered resources** — `StatusSurfaceGradient` and `BorderSoftBrush` (both already present in `DesignSystem.axaml` and registered in every `ThemeCatalog.GradientStops`/`Structural` combination, `StatusSurfaceGradient`'s name and near-identical Dark/Default color values making it clearly built for exactly this surface) were simply never wired up to the title bar or footer. No new resources, no `ThemeCatalog` changes — pure `MainWindow.axaml` rewiring.
- **Scope note**: audited the rest of `MainWindow.axaml` for similar hardcoded structural hex values and found none at this scale — the remaining hardcoded colors are thin 1px dividers, per-value status badges, table-header row tints, and deliberately theme-independent terminal-style console/RCON output panes, all matching the same "secondary decorative accent" category `ThemeCatalog` already disclaims, not primary chrome containers like the title bar/footer/nav pane.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.35.0 — Button Color System

- Eleventh version of the GUI/workflow/UX overhaul roadmap: a cross-cutting button color system, superseding the narrower per-page notes from items 19, 20, and 27.
- **Root cause confirmed**: every unstyled `Button` in the app shares the identical `ButtonGradient` background (`Styles/DesignSystem.axaml`'s base `Button` selector with no class) — Browse, Open, Verify, Manage, Generate, and many more all rendered pixel-identical regardless of what they did.
- **Two new semantic button classes, designed once**: `Button.inspectAction` (cyan `InspectGradient`) for non-mutating "look at/confirm" actions, and `Button.targetAction` (violet `TargetGradient`) for "act on a specific thing" actions. Neither reuses the Primary/Success/Danger/Warning gradients already spoken for by Save/Start/danger/warning buttons elsewhere.
- **Applied consistently, not one-off per page**: Workspace's 4 Browse buttons and Server Setup's VERIFY/RESCAN row actions now use `inspectAction`; Workspace's 10 Open-in-explorer buttons and Server Setup's MANAGE/INSTALL/CREATE/ENABLE row actions now use `targetAction`. Server Setup's per-row classification is data-driven — new `EnvironmentChecklistItemDto.IsInspectAction`/`IsTargetAction` computed properties key off the row's real `Action` string (`VERIFY`/`RESCAN` vs. everything else that's actually enabled), bound via `Classes.inspectAction`/`Classes.targetAction`.
- **Configuration's Generate button** (item 27) is now bigger and labeled "🎲 Generate" instead of an icon-only `🎲`, using `inspectAction` since it doesn't persist anything by itself (Save Changes is still required afterward) — it was already functionally enabled, just easy to mistake for disabled given its size.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.34.0 — Category Tab & Nav Pane Styling

- Tenth version of the GUI/workflow/UX overhaul roadmap: two small, precisely-scoped style-resource swaps.
- **Category tab checked/hover gradients were swapped.** `ToggleButton.categoryTab:checked Border.categoryTabGlow` was using the bright `CategoryActiveGradient` and `:pointerover` was using the darker `GlassOptionHoverGradient` — backwards from the intent, and directly contradicted by the stylesheet's own nearby comment ("Category selection is dark glass; semantic color lives on the overline, underline and glow"). Selecting a category tab now settles into the darker glass look; hovering shows the brighter gradient.
- **The navigation pane's background changed** from `NavGlassSurfaceGradient` (a flatter fill) to `GlassOptionHoverGradient` — the same darker glass-hover gradient already reused across ribbon buttons, category tabs, and nav buttons elsewhere in the app, for a more visually consistent dark-glass look.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.33.0 — Home Dashboard Fixes

- Ninth version of the GUI/workflow/UX overhaul roadmap: three fixes to the Home Dashboard.
- **Removed the ONLINE PLAYERS card that duplicated the top-row PLAYERS summary card.** The top-row card already shows online/known counts with its own OPEN button to the Players page; the second card (a live `ListBox` of names, sitting beside the LIVE SERVER / MANAGER LOG card) added nothing the first didn't already cover. The LIVE SERVER / MANAGER LOG card now takes the full row width in its place.
- **The BACKUP card now gets an amber tint and an OPEN button**, matching the pattern the ACTIVE WORLD card right next to it already used (`Classes="card glowDarkGreen"` + an OPEN button) — swapped `accentHome` for the existing-but-previously-unused `glowAmber` style and added `OPEN` → `Backups`.
- **Fixed the real cause of sparse console output, especially right after a server starts.** Every log-bearing call site in the Desktop was requesting only 120 lines from `GetLogTail`/`GetStatusPolling`, well under the headless service's own 500-line cap — merging that budget across 2-4 sources (MystTiq stdout, Pal.log, AdminCommands logs) left as little as ~40 lines per source, nowhere near enough to show a modded server's full UE4SS/MOD LOAD startup output. Raised to the server's actual max at every call site that feeds a visible log view: initial tab connect, the recurring per-tab refresh timer, the shared monitoring-refresh core behind manual/Console-page refresh, and both the mid-operation poll and the post-operation authoritative poll around Start/Stop/Restart. Left untouched: the Players page's own aggregate-status call, which fetches log data as a side effect of reusing that endpoint but never displays it.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.32.0 — Visual Density Pass

- Eighth version of the GUI/workflow/UX overhaul roadmap: nav-pane icon sizing, and a compact-card pass across four pages.
- **Navigation pane icons are noticeably larger** — 20×20 → 28×28 (the icon column widened 24px → 32px to match) across all 25 nav destinations, via the same shared literal pattern in every entry.
- **Server Setup's 4 summary cards** (COMPONENTS/READY/ATTENTION/ENVIRONMENT HEALTH) are now compact: explicit tight padding, smaller fonts, and shortened descriptions — the prior full-sentence descriptions were what actually drove card height via text wrap, more than padding did, since these cards had no base `.statuscard`-only Padding/FontSize to shrink in the first place. The READY card's count text is now centered.
- **Backups' 4 summary cards** get the same compact treatment.
- **Server Doctor's per-check finding cards** get tighter padding/margin/spacing and smaller fonts, so more checks fit on screen without scrolling.
- **MOD Library's 6 summary cards are removed** — they duplicated MOD Dashboard's own copy of the same cards (both pages previously shared one `IsModInventoryPage`-gated grid); now gated to `IsModDashboardPage` only, so Dashboard keeps them and Library doesn't.
- **Fixed a carried-over oversight found while touching this card**: v0.7.26.0 moved "Verify Files" into the ribbon but never actually removed the original button embedded in the Server Setup card — removed now.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.31.0 — Duplicate-Install Prevention

- Seventh version of the GUI/workflow/UX overhaul roadmap. Requested directly: "there should be no reason to have multiple tabs connecting to the same server. They can have the same port, but the location of the install should be different" — two tabs are the same server if their install directories match, regardless of what host/port each was reached through.
- **Root constraint found during investigation**: a saved `ConnectionProfile` (the persisted record behind a tab) has no concept of install location at all — just a name, a `BaseAddress`, and a certificate pin. The install directory only becomes knowable *after* successfully connecting and asking that server about itself, so this can only ever be a post-connection check, not something that blocks the setup wizard before a connection exists.
- New `TabSession.ServerRoot` caches each tab's last-known install directory, captured from `GetServerDistributionStatusAsync`'s response (`ServerDistributionStatusDto.ServerRoot`) — data already being fetched on every routine Dashboard refresh, so no new network calls were needed. New `MainWindowViewModel.RecomputeDuplicateInstallWarnings()` groups all open tabs by `ServerRoot` (case-insensitive) and flags any tab whose root is shared by another tab, run after every refresh and on tab close (so a resolved duplicate — a tab closing, or a later poll finding the roots actually differ — clears correctly).
- A new amber warning banner on the Dashboard (`ActiveTab.HasDuplicateInstallWarning`/`DuplicateInstallWarning`) names the shared install path when a match is found, rather than silently letting two tabs operate on what's actually the same server without either one knowing.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.30.0 — Confirmed Bug Fixes: Status Text

- Sixth version of the GUI/workflow/UX overhaul roadmap: three more confirmed bugs fixed, plus one honest correction to a fourth suspected bug that didn't hold up under closer investigation.
- **Backups' TOTAL ARCHIVES/VERIFIED/PENDING-REVIEW summary cards no longer always read 0.** `BackupItems` was populated at three separate call sites, but only one of the three raised change notifications for the three derived count properties — the other two correctly repopulated the list (which is why it rendered fine) but silently left the summary cards frozen. All three now go through one new shared `PopulateBackupItems` helper, so the notification can't be forgotten a fourth time.
- **UE4SS no longer reports the meaningless placeholder "0.0.0.0" as its installed version.** `DetectUe4ssVersion`'s DLL-`FileVersionInfo` fallback (`HeadlessModManagementService`) can legitimately return the literal string `"0.0.0.0"` for a DLL whose version resource exists but was never stamped with real numbers — a non-null, non-empty string that used to be returned as-is. A new `IsMeaningfulVersion` check treats an all-zero version the same as no version found, falling through to the existing "Installed — version metadata unavailable" case instead.
- **Server Doctor's "UNKNOWN" label is no longer unexplained.** That label is legitimate, correct logic (it means "all checks passed, but the server isn't running, so calling it 'Ready' would be dishonest") — the bug was that the backend's own explanation for it (`OverallHealthDetail`, e.g. "Server is not running; no health issues detected.") was already being computed but never read on the Desktop side. `DoctorSummary` now appends it.
- **Correction, not a fix**: the roadmap's original item 48 ("UE4SS Runtime Health: Unverified / Runtime Mods Root: Not reported") assumed Pal.log carried independent UE4SS-native evidence that detection wasn't consulting. Closer inspection this release found that assumption wrong — those console lines are `HeadlessModManagementService`'s **own** diagnostic restatement of the same `ResolveUe4ss()` result the UE4SS page already displays, not separate raw evidence. No code change was made for this item; see this release's architecture doc for the full explanation.
- No new architecture — all three fixes stay within existing status/config/diagnostics code paths.

## v0.7.29.0 — Confirmed Bug Fixes: Detection & Config

- Fifth version of the GUI/workflow/UX overhaul roadmap: two bugs found and pinpointed during the original page-by-page walkthrough, now fixed.
- **Official/Vanilla config preset no longer overwrites Server Name/Description/passwords.** `ApplySelectedConfigurationPreset`'s Official branch (`MainWindowViewModel.cs`) used to reset every field in `SimpleConfigurationNames` to its default — a set that legitimately includes identity fields for the purpose of deciding what the Simple Settings *view* shows, but was being reused as "what a preset should mutate," a different concern. New `GameplayRateConfigurationNames` (a proper subset, gameplay rates only) is used for the reset instead, matching the scope the Balanced/Relaxed presets already correctly had via `GetQolPreset`. `SimpleConfigurationNames` itself is untouched — its view-filtering usage was always correct.
- **A genuinely-running PalServer at an unexpected install path no longer silently reports as "Stopped."** `WindowsServerLifecycleService.GetStatusAsync` previously collapsed "not running" and "running somewhere the configured `ServerRoot` doesn't match" into the identical generic "PalServer is not running." message. New `FindProcessesWithMismatchedPath()` checks for this specific case (a process matching the expected name, but not the configured path) before falling back to the generic message, producing a distinct, actionable detail naming the expected and actual paths.
- **The Dashboard actually shows the improved detail now.** Fixing the backend alone wasn't enough — `DashboardSessionText` and `DashboardHealthDetail` (`MainWindowViewModel.ApplyStatus`) both used to show a hardcoded generic string whenever the server wasn't Ready, discarding `status.Detail` entirely. Both now surface the backend's real explanation.
- No new architecture — both fixes stay within the existing lifecycle-status and configuration-preset code paths.

## v0.7.28.0 — New Ribbon Actions

- Fourth version of the GUI/workflow/UX overhaul roadmap: three genuinely new ribbon capabilities, rather than relocating existing buttons like the two prior versions.
- **Force Stop Server**: a "Danger" ribbon group (Force Stop) is now reachable from every page, not just when exiting the app. `ForceStopServerAsync` (`IMystTiqApiClient`) already existed and was already reachable from `ShutdownForExitAsync(force: true)` — this is its first user-facing exposure, wired through the same `RunLifecycleAsync` helper Start/Stop/Restart already use.
- **Install Missing**: Server Setup's "Environment" ribbon group (added in v0.7.26.0 for "Verify Files") gains a second button. `InstallMissingEnvironmentAsync` already existed but was previously only reachable indirectly through the per-row action dispatch on the component checklist — now has its own standalone command.
- **Managed process list + Kill Processes** on Server Doctor: the page's status card now shows the real OS-level processes associated with the active server (name + PID + responding state) at the top-right, sourced from `ServerStatusDto.Processes` — that data already flowed end-to-end from the headless host's `FindManagedServerProcesses` through `ServerLifecycleSnapshot`, just was never read on the Desktop side before this. "Kill Processes" in the Doctor ribbon group reuses `ForceStopServerCommand` rather than inventing a second kill mechanism, since it's functionally the same action.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.27.0 — Ribbon Consolidation Pass 2

- Third version of the GUI/workflow/UX overhaul roadmap: the second wave of page-local buttons relocated into the ribbon.
- **Workspace** gains a "Workspace" ribbon group with Refresh (`LoadConfigurationCommand`) — "Validate All" stays on the page, not part of this relocation.
- **Backups** gains a "Backups" ribbon group with Create, Verify All, Refresh, and Open Root (`CreateBackupCommand`/`VerifyAllBackupsCommand`/`RefreshBackupsCommand`/`OpenBackupRootCommand`).
- **MOD Dashboard and MOD Library** share one new "MODs" ribbon group with Refresh MODs and Verify & Scan — these two pages already shared one in-page toolbar via the existing `IsModInventoryPage` (Dashboard-or-Library) condition, kept unchanged here rather than splitting it into two separate groups.
- **UE4SS** gains its own "UE4SS" ribbon group with Refresh Runtime — the same `RefreshModsCommand` as MODs' Refresh, just labeled and gated for this page specifically, matching the in-page button it replaces.
- **Server Doctor** gains a "Doctor" ribbon group with Run Doctor and Export Report (`RunDoctorCommand`/`ExportDoctorCommand`).
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.26.0 — Ribbon Consolidation Pass 1

- Second version of the GUI/workflow/UX overhaul roadmap: the first wave of page-local buttons relocated into the ribbon, made possible by v0.7.25.0's shrink-to-fit infrastructure.
- **Server Setup** gains an "Environment" ribbon group with "Verify Files" (the existing `VerifyEnvironmentCommand`, previously embedded in the page's Environment Health card).
- **Configuration** gains a "Configuration" ribbon group with Import, Export, Save, and Reset — and drops "Load Active" entirely as redundant, along with its now-fully-unused `LoadPalworldConfigurationCommand` wrapper (the underlying `LoadPalworldConfigurationAsync` method stays; it's still called from three other internal code paths). Import/Export still open a real file picker, which stays in code-behind per this codebase's established convention (needs a `TopLevel`/`Window` to open against) — the ribbon's shared button template now supports both Command-bound and native-dialog actions via a new `NativeDialogAction` dispatch (`RibbonAction_OnClick`), rather than every ribbon action needing to be a plain `ICommand`.
- **Console** gains a "Console" ribbon group with Refresh/Pause/Clear/Export (the existing `RefreshConsoleViewCommand`/`PauseConsoleCommand`/`ClearConsoleViewCommand`/`ExportConsole_Click`, previously in the Console Log card's own header). The RCON card also moves above the Console Log card, per direct request.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.25.0 — Ribbon Adaptivity

- First release of a broader GUI/workflow/UX overhaul: the user walked through MystTiq's pages one by one, flagging 57 concrete fixes (layout, workflow, and outright bugs — several with root causes pinned down during the walkthrough itself, e.g. the Backups page's summary cards always reading 0, the Official/Vanilla config preset wrongly overwriting Server Name, and UE4SS reporting "0.0.0.0" for its installed version). Sequenced into a 21-version roadmap, front-loading shared infrastructure and low-risk fixes before larger feature work.
- **This release is that infrastructure's first piece**: the ribbon (the row of action buttons under the category tabs — Refresh/Start/Restart/Stop, Backup/Console/Doctor) is now built from data (`RibbonGroupViewModel`/`RibbonActionViewModel`, new `Models/RibbonActionViewModel.cs`) instead of two hand-authored `Border` blocks in `MainWindow.axaml`. `MainWindowViewModel.VisibleRibbonGroups`/`OverflowRibbonGroups` split whichever groups don't fit the actually-measured window width into an overflow flyout (reached via a new "»" button) — the exact same shrink-to-fit pattern the tab strip already uses (`VisibleTabs`/`OverflowTabs`, v0.7.16.0), reusing its width-measurement technique (`RibbonHost_OnSizeChanged` → `UpdateRibbonWidth` → `RecomputeRibbonLayout`) rather than inventing a new one.
- Ribbon content is unchanged today (still just Server Control and Quick Actions, byte-identical buttons/commands/icons/colors to before) — this version is purely the adaptive infrastructure every subsequent ribbon-relocation version in the roadmap needs, since each of those adds more buttons that could otherwise silently overflow the row with no way to reach them.
- Icon colors moved from inline `Foreground="{DynamicResource AmberBrush}"` (etc.) per-button to new `TextBlock.flatIcon.amber`/`.green`/`.blue`/`.red`/`.cyan` style classes (`Styles/DesignSystem.axaml`), toggled via `Classes.amber="{Binding IsAmberIcon}"` the same way several other conditional visual states already work in this codebase (e.g. `Classes.glowGreen`). Kept deliberately on `DynamicResource`, not a brush resolved once at construction time, so ribbon icons keep following live theme changes — no regression relative to today, and no conflict with the theme-audit work later in the roadmap (item 50/v0.7.36.0).
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.24.0 — Roadmap Doc Audit

- Fifth and final release from the approved backlog plan (v0.7.20.0–v0.7.24.0). Rather than assuming the two `docs/roadmap/WINDOWS_BACKPORT_REGISTRY.md` rows flagged during the v0.7.19.0 audit were both stale labels, each was individually re-checked against the actual current code — the same discipline the v0.7.11.0 doc-consistency audit established.
- **"Service-style watchdog/recovery behavior inspired by Linux/systemd supervision"** (row target v0.4, labeled "Discovery backlog" since the registry's creation): confirmed already satisfied. `HeadlessFleetCrashRecoveryService` (shipped v0.6.13.0, see `docs/architecture/v0.6.13.0-fleet-wide-crash-recovery.md`) wraps a per-profile `HeadlessSupervisor` — the exact systemd-inspired crash-detect/backoff/auto-restart loop `service-run` already used — and gives every profile under `api-run` (the mode Desktop's own sidecar actually runs) the same guarantee. Flipped to "Shipped" with a citation.
- **"Improved structured log rotation/retention if Linux implementation proves useful"**: investigated rather than assumed stale. `HeadlessConsoleLogWriter` (`MystTiq.HeadlessHost`, shared by both platforms — there is no separate Linux-only implementation to backport from) appends every line to `MystTiq-PalServer-Console.log` indefinitely, with no size cap, rotation, or retention logic anywhere in the codebase. Confirmed genuinely open on both platforms; left as "Discovery backlog" with that confirmation recorded in the registry instead of an unverified guess, rather than attempting the open-ended feature work itself as a side effect of a doc audit.
- No code change — this release is documentation-only (`docs/roadmap/WINDOWS_BACKPORT_REGISTRY.md`, plus the standard version-bump/changelog/architecture/release-notes set).

## v0.7.23.0 — Specific Background-Tab Connection Errors

- Fourth of the planned backlog sequence. `RefreshTabLightweightAsync` (the health check background tabs poll on their own cadence, separate from the active tab's full `RefreshAsync`) had a bare `catch { }` that discarded the actual exception entirely and always reported `tab.ConnectionState = "Connection failed"` — even a real, informative version mismatch collapsed into the same generic text as an actually-unreachable server. The active tab's own connection check (`RefreshAsync`, hitting the identical `GetHealthAsync` endpoint) already classified these correctly, throwing `InvalidOperationException` for a version/component mismatch and `UnauthorizedAccessException` for a missing bearer token.
- **`RefreshTabLightweightAsync` now mirrors that exact classification**: the same two conditions, the same two exception types, now surfaced as `tab.ConnectionState = "Incompatible API version"` / `"Needs bearer token"` respectively, with the true catch-all `"Connection failed"` reserved for everything else. `ConnectionState` already renders as visible text in two places once a tab becomes active (a status badge and a status card, both already bound to it) — no XAML change needed for that value to become visible.
- `TabSession.StatusDotColor`'s switch gained the two new strings alongside the existing `"Connection failed"`/`"Invalid profile"` cases, so they still render the same red failure-state dot rather than silently falling through to the neutral grey default.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.22.0 — Per-Action Busy Status

- Third of the planned backlog sequence. The footer's `ProgressBar` (`MainWindow.axaml` ~2288) has always sat next to a `StatusBarText` binding that, whenever `IsBusy` was true, showed a single hardcoded `"Working…"` literal (`RaiseIsBusyDependents`) — no indication of which of the app's many possible operations was actually running.
- **New `BusyReason` property** on `MainWindowViewModel`: when set, `RaiseIsBusyDependents` shows it in place of the generic literal (`StatusBarText = value ? (BusyReason ?? "Working…") : ...`) — the existing footer `TextBlock` binding needed no XAML change at all, since it was already wired to `StatusBarText`.
- Threaded through the highest-value operations first, not attempted as blanket coverage of every `IsBusy = true` site in the file: **server start/stop/restart** (all three already funnel through one shared `RunLifecycleAsync(activity, ...)` helper, which already had an `activity` string — `"Starting…"`/`"Stopping…"`/`"Restarting…"` — one insertion point covers all three), **backup create** (`CreateBackupAsync`), **backup restore** (`RestoreBackupAsync`), and **world transaction apply** (`ApplyWorldTransactionAsync`).
- Every other operation not yet threaded through keeps the exact same generic `"Working…"` fallback as before — a disclosed, intentional gap (matching the plan's explicit scoping), not a claim of full coverage.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.21.0 — Experimental Real-World Map Positions

- Second of the planned World Map sequence (see `docs/architecture/v0.7.20.0-world-map-presets.md`). v0.7.0.2 previously tried and abandoned real coordinate calibration because the fetched formula didn't reconcile with its own source's worked example — this pass does not repeat that mistake.
- **The formula is now numerically verified, not just plausible-looking**: research found the open-source `palworld-coord` project (github.com/palworldlol/palworld-coord) reverse-engineered Palworld's `.sav`-world-coordinate-to-map-UI conversion (translate `(123888, 158000)`, scale `459`, axis-swapped). Two independent lookups of that project's *documentation* disagreed on the axis order — the exact failure mode that sank v0.7.0.2 — so this pass went to the project's actual source code instead. New `PalworldMapCoordinates.ToMapUnits`/`ToCanvasPosition` (`MystTiq.Desktop/Services`) reproduces that source's own published worked example exactly (`sav_to_map(-167230, 96430)` → `(-134, -94)`, confirmed both by direct calculation and against the source's README), and its constants reconcile precisely with the documented `.sav` coordinate bounds (both axes span exactly `1000 × 459` with zero residual error).
- **What's still unverified, and why this ships opt-in rather than as the default**: the conversion math is solid, but which corner of the Palpagos map *image* its output coordinate space corresponds to — and whether the map's Y-axis needs inverting for screen rendering — isn't independently confirmed anywhere in the source material. A wrong-but-plausible calibration is worse than the existing, already-correct relative-spread view, so a new **"Experimental: real-world positions (Palpagos only)"** checkbox on the World Map card is off by default, only appears when the Palpagos preset specifically is the active background (`MainWindowViewModel.IsPalpagosMapActive`, derived from whichever file path is actually loaded — no separately-tracked state to drift out of sync), and its tooltip says outright that dots may look mirrored or offset pending confirmation against a real server.
- `RebuildPlayerMapPoints` gained the calibrated branch, used only when the toggle is on and Palpagos is active; every other case (World Tree, a browsed image, no background, or the toggle off) keeps the exact same auto-fit relative-spread logic unchanged.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.20.0 — World Map Presets

- Requested directly, first of a planned sequence (see `docs/architecture/v0.7.20.0-world-map-presets.md` and the broader plan discussion): two real Palworld map images (`docs/images/palpagos.webp` — the base game map, "Palpagos Islands" — and `docs/images/worldtree.webp`) get bundled into the World Map card as selectable presets, so a user doesn't have to find and crop their own screenshot before the map has any geography-shaped background.
- **New `MapPresetService`** (`MystTiq.Desktop/Services`): extracts a bundled `avares://` asset to a real file under the same local config root `LocalMapPreferencesStore` already uses (once, cached on disk), then hands that path to the **existing, completely unchanged** `SetMapBackgroundImagePath(path)` — a preset is, from that point on, indistinguishable from a browsed file as far as the rest of the app is concerned. No parallel loading/persistence path was added.
- Both images converted from their original 4096×4096 WEBP source to 1024×1024 PNG (the World Map card only ever renders them at 480×480 — the source resolution was unnecessary weight for no visible benefit) and bundled under `Assets/Maps/`.
- `MainWindow.axaml`: the World Map card's header row simplified back to just the expand/collapse toggle; the previous Browse/Clear button pair moved into the expanded content area alongside two new preset buttons (Palpagos/World Tree), all in one `WrapPanel` row.
- **Deliberately unchanged this release**: `RebuildPlayerMapPoints` still auto-fits player dots to the bounding box of currently-online players — dots do not yet line up with real in-game geography on either preset. That's real coordinate calibration, sequenced as its own separate, higher-risk follow-up specifically so it doesn't block this release's real, working, low-risk preset feature.

## v0.7.19.0 — Full Navigation Icon Set

- Requested directly, following on from a backlog check. A dedicated audit of `docs/roadmap/PRODUCT_ROADMAP.md` (which calls for an "original Palworld-inspired MystTiq icon family" across all of Dashboard/Doctor/Players/Guilds/Pals/Worlds/Backups/Mods/Updates/Automation/Network/Performance/Settings) against what actually shipped in v0.7.0.0 found the icon pass was always a partial build-out, not a finished item: only 7 of the app's 25 nav-sidebar destinations (Dashboard, Server Setup, Players, Bases, Backup Center, Security, Fleet) got real hand-authored `StreamGeometry` vector icons; the other 18 (Configuration, Console, Workspace, Inspector, Guilds, MOD Dashboard, MOD Library, UE4SS, Update Center, Server Doctor, Crash Analyzer, Save Tools, Diagnostics, Settings, Notifications, Activity & Audit, Automation, Alert Center) were left on plain Unicode-glyph text prefixes (⚙, ▰, ♛, ◆, ▦, ↻, ✚, ▲, ▤, !, ●, ≣, ⏱, ⚠) — v0.7.0.0's own architecture doc says as much explicitly.
- **A complete, consistent 25-icon set** — one glossy "glass orb" icon per nav destination, each a distinct accent color with a purpose-matched glyph (a gauge for Dashboard, a vault for Backup Center, a shield for Security, and so on) — replaces every nav-sidebar icon uniformly: both the 7 old vectors and the 18 old glyph prefixes.
- **Shipped as PNG, not `StreamGeometry`**: unlike the app's existing single-color vector icons (a flat path re-tinted at runtime via `{DynamicResource TextBrush}`), this set uses layered radial/linear gradients, clip paths, and drop-shadow blur filters — a `StreamGeometry` (Avalonia's own simple-path vector primitive) can't express any of that, and this codebase has no SVG-rendering dependency to reach for the raw `.svg` source files instead. Rendered to 240×240 PNGs (already generated alongside the source SVGs) and embedded as `<Image>` controls at 20×20, matching this codebase's existing convention for every other raster asset (app icon, wordmark, dashboard background) rather than adding a new NuGet dependency for this alone.
- The 7 now-unused `StreamGeometry` resources (`IconDashboard`/`IconServer`/`IconPlayers`/`IconFleet`/`IconSecurity`/`IconBase`/`IconBackup`) were removed from `Styles/IconGeometries.axaml` rather than left as dead code; `IconAddTab` (the tab bar's "+" button, unrelated to nav) stays.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.18.0 — Player Directory Name Deduplication

- Requested directly, the last item of a follow-up list raised earlier this session: "we should have a selectable feature to remove duplicate names for users." The same physical player can legitimately show up more than once in the Players page's Directory list under the same display name — a rejoin under a different platform ID, a stale record left over from an old save — and there was previously no way to declutter the list without losing any of the underlying data those extra `PlayerId`s still genuinely identify.
- **New "Hide duplicate names" checkbox** on the Players page, next to the existing search/filter controls. Purely a view-side filter, applied after the existing search/online/save-state/admin filters in `ApplyPlayerFilters()`: groups the already-filtered list by `PlayerName` (case-insensitive; blank names are never grouped together, so multiple genuinely-nameless records don't collapse into one), keeping exactly one record per name — preferring, in order, currently online, then has a save at all, then most recently written. Every other record for that name is hidden from the list, not deleted or merged; unchecking the box always restores every record instantly, since nothing about the underlying `PlayerRecords` data ever changes.
- The visible-count text now reports how many duplicate names were hidden (e.g. "42 visible / 51 known (9 duplicate name(s) hidden)") when the filter is active, so it's clear at a glance that something is being hidden rather than that the server actually has fewer known players.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.17.0 — Fix `api-remote-enable` CLI Bug

- Closes the CLI bug found (but explicitly not fixed) while building v0.7.13.0's remote-secured verification rig: `mysttiq-server.exe api-remote-enable --bind-address <LAN-IP> ...` could not actually be used to promote a fresh config to a secured remote binding, making the documented `api-token-create` → `api-tls-create` → `api-remote-enable` workflow (`docs/linux/COMMAND_REFERENCE.md`, `README.md`) non-functional from the CLI.
- **Root cause**: `Program.cs` runs a blanket "effective configuration" validation before the command switch, for every command including `api-remote-enable`/`api-remote-disable`. It built the effective config from the CLI's *new* `--bind-address` override but the *old*, not-yet-updated `Authentication`/`Tls` flags — so a fresh/loopback config's `Authentication.Enabled=false`/`Tls.Enabled=false` always failed validation against a non-loopback bind address, rejecting the command before `HeadlessRemoteApiEnrollmentService.EnableRemoteApi` — the handler that would have produced a valid, fully-enabled config — ever ran.
- **Fix**: `api-remote-enable` and `api-remote-disable` now skip that blanket pre-command check entirely — neither command reads the effective configuration it computes, and each already validates its own resulting configuration internally via `HeadlessRemoteApiEnrollmentService`. Every other command (including `api-run`/`service-run`, which do consume it) keeps the exact same pre-check as before; a fresh config still cannot start listening on a non-loopback address without auth+TLS enabled.
- New `scripts/Test-v0.7.17.0-RemoteEnableSmoke.ps1` — permanent, real-CLI-invocation regression coverage for the exact repro: `config-write-default` → `api-token-create` → `api-tls-create` → `api-remote-enable` → `api-run`, then confirms the running instance actually enforces authentication and TLS (an unauthenticated request to a protected route is rejected, `/healthz` reports `authentication:true`/`tls:true`). Verified directly on the real Linux VM in addition to Windows.
- **Also restored the Linux packaging/deployment pipeline** (`Build.ps1 LinuxHeadless` / `Deploy-Test-MystTiqLinux.ps1`), broken for every release since v0.6.1.0 because it hard-requires a version-specific acceptance-script pair that had never been recreated (~15 releases). New `scripts/Test-v0.7.17.0-LinuxAcceptance.sh`/`Test-v0.7.17.0-ProductionReadiness.sh`, adapted from the v0.6.1.0 originals, then expanded from that release's endpoint coverage to essentially the full current read-only/safe API surface at the user's explicit request (~30 previously-untested read-only endpoints, safe self-test/preview-only POST checks, and fully reversible automation-rule/security-principal create-delete roundtrips). Surfaced and fixed four real bugs along the way: every ephemeral CLI invocation against the live config crashed with an unhandled permission exception writing lifecycle state under the live install's root-owned `RuntimeRoot` (fixed with an isolated `--runtime-root` per invocation); a boot-scoped fatal-journal scan could never recover from one old, unrelated crash-loop event on a long-uptime host (narrowed to a 30-minute recent window); three checks wrongly expected HTTP 200 where the real, correct behavior is a documented graceful-failure status (RCON doctor 424, ban-list 409 when RCON is disabled, `palworld/config/defaults` 400 when the config already exists); and `PalworldSettingsConfigurationService.ValidateDefaultRequest` threw an unhandled `NullReferenceException` on any request omitting an optional string field, turning a clean validation response into a 500 for any caller (not just this test) — fixed with null-safe checks. Full extended deploy against the real VM now passes 118/118 (2 expected warnings, 1 expected skip).
- **Then manually verified the mutation-path endpoints too** (world import, guild/base ownership transfer, backup restore, MOD install/rollback/delete, config writes) — deliberately excluded from the automated script since this VM's own world had no real players/guilds to mutate — by copying this machine's real Windows Palworld save (read-only) into a fully isolated Linux scratch copy and running real preview→apply→verify cycles against it, restoring original state afterward and removing the copy when done. Found and fixed a fifth real bug this way: `HeadlessBackupRetentionRequest.IncludeClasses` was typed `IReadOnlySet<BackupClass>?`, which `System.Text.Json` cannot deserialize from a JSON array — any real caller passing that (documented, non-default) field would crash with a 500 instead of getting filtered results; changed to `IReadOnlyCollection<BackupClass>?`. See `docs/architecture/v0.7.17.0-linux-acceptance-pipeline-restored.md` for the full list of what was verified for real and what remains explicitly out of scope.

## v0.7.16.0 — Responsive Tab Strip

- Requested directly, item 3 of a 3-item follow-up list raised alongside this session's other work: "the extra server tabs will need to be able to expand when full screen to utilize more room. If they run out of room then there should have a double arrow or icon pointing to the right that when clicked shows the hidden servers and the option to create new."
- **The tab strip's ListBox had a hardcoded `MaxWidth="720"`** regardless of how much window width was actually available — on a maximized or wide window, tabs stayed capped at that width while real space went unused, and once more tabs were open than fit within it, the extras were silently clipped off the edge with no scroll, no indicator, and no way to reach them at all.
- **`MainWindowViewModel` now tracks the tab strip's real measured width** (`UpdateTabStripWidth`, called from a new `TabStripHost_OnSizeChanged` handler on the tab strip's own host panel — not the whole window, since that panel's actual available space also shifts with the fixed-width brand column and the window-chrome column beside it) and splits `Tabs` into two new collections, `VisibleTabs` and `OverflowTabs`, recomputed whenever the strip resizes, a tab opens or closes, or the active tab changes. The tab `ListBox` now binds to `VisibleTabs` instead of `Tabs` directly.
- **A new chevron button** ("»", matching this codebase's existing convention of plain Unicode glyphs for ribbon/nav icons rather than always reaching for the Segoe Fluent Icons font) appears only once `OverflowTabs` is non-empty, opening a flyout listing the hidden tabs — clicking one switches to it — plus a "Set Up New Server" entry, the same one the "+" button's own flyout already offers, so creating a new tab stays reachable even when the strip is already full.
- **The active tab is never the one hidden**: if recomputing the layout would push the currently active tab into the overflow slice, it's swapped with the last naturally-visible tab instead — switching tabs (including from the new overflow flyout) always keeps the one you're looking at in the visible row.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.15.0 — Temporary Bans & Historical FPS Charting

- Requested directly, continuing in order through the backlog list raised earlier this session (temporary bans with auto-expiry, "Remove Admin" feasibility, give-items/give-Pals, mod load-order/per-mod config editing, historical FPS charting). A feasibility research pass read the actual RCON/REST command set this session has already tested against a real server, the existing mod-management and history-persistence code, and this codebase's own prior architecture docs before committing to scope.
- **Temporary Bans** (new `HeadlessTemporaryBanService`, mirroring v0.7.10.0's `HeadlessWhitelistService` shape): bans a player immediately via the existing ban path (`PlayerModerationCoordinator`, unchanged), persists an expiry timestamp, and auto-unbans once it elapses via the same `/status/poll` cadence Whitelist already uses. New `GET /players/temp-bans` and `POST /players/{id}/temp-ban` routes; a manual Unban (the existing button/action) now also clears any tracked expiry for that player so the sweep doesn't attempt a redundant second unban later. Desktop: a duration field + "Temp Ban" button next to the existing Kick/Ban controls, and a new collapsible "Temporary Bans" card (matching the Ban List/Whitelist collapsed-by-default pattern) listing active entries with time remaining and a "Lift Now" early-cancel per row.
- **Historical FPS charting**: `HeadlessHistoricalMetricSample`/`HeadlessHistoricalMetricsSnapshot` gained nullable `ServerFps`/`ServerFrameTimeMs` and `AverageFps`/`PeakFps` fields — null (not 0) whenever a sample's poll had no real Palworld REST data, the same convention `RuntimeMetricsSnapshotDto.ServerFps` already established in v0.7.9.0. The Dashboard's `ResourceHistoryChart` custom control gained a third plotted series (green, matching the existing FPS color convention) that only draws across consecutive samples that both actually have a value, so a REST-disabled stretch reads as a gap rather than a misleading flat line at zero. A new `HistoryFpsSummary` text line sits alongside the existing CPU/Memory history summaries.
- **Investigated, resolved as cleanup, not built as features**: the research pass found "Remove Admin — BACKEND REQUIRED" is an orphaned stub inherited from the pre-rewrite GUI with no real target concept — Palworld's RCON has a single server-wide `AdminPassword`, not a per-player admin flag, so there was never a capability for this button to eventually point at. Removed outright rather than left permanently disabled. Also found and removed a second, unrelated stale stub — "Whitelist — BACKEND REQUIRED" — left sitting in the same button row since Whitelist itself shipped a full, working implementation elsewhere on this same page back in v0.7.10.0.
- **Explicitly not attempted this release, with reasons recorded**: give-items/give-Pals (Palworld's RCON/REST, as tested throughout this session, has no such command; the only proven path is offline save-file container mutation, a materially larger and riskier undertaking than the existing Pal Editor) and mod load-order/per-mod config editing (no standard schema exists across mods to build a UI against — load order is filename/manifest convention-dependent and per-mod config formats vary arbitrarily mod-by-mod). Both remain backlog, unscoped, rather than shipped half-built.
- New permanent `scripts/Test-v0.7.15.0-RouteSmoke.ps1`, carried forward the same way v0.7.12.0's route-smoke script already is: verifies the temp-ban routes' reachability and graceful-failure shape (and that a failed ban attempt does not leave behind a phantom tracked entry), and that `/history` reports the new FPS fields as null rather than 0 when nothing has been recorded yet.

## v0.7.14.0 — Accessibility & Interaction Feedback Pass

- Requested directly, the part of a much earlier multi-part ask that had never been worked yet: "Check to see if there are UI improvements that can be made based on best practices, Microsoft docs, and known good methods." Everything prior (v0.7.1.0 ordering, v0.7.4.0 card flare, v0.7.5.0 no-scroll density, v0.7.13.0's wizard redesign) addressed narrower, separately-requested asks — this is the first pass against Fluent/Avalonia best practices specifically. A dedicated audit cross-referenced the current design system (`Styles/DesignSystem.axaml`, `App.axaml`) and `MainWindow.axaml` against current Microsoft Fluent 2 guidance and Avalonia's own accessibility documentation.
- **Zero `AutomationProperties.Name` anywhere in the app, despite 50+ `ToolTip.Tip` usages.** A tooltip is not exposed as a control's accessible name to Narrator/NVDA/UIA — every icon-only button (window chrome, tab-close, add-tab, ribbon actions, the two random-name-generator dice buttons) was silent to screen readers. Fixed by adding explicit `AutomationProperties.Name` to all 16 genuinely icon-only controls in the file. Buttons whose visible `Content` is already real text (nav sidebar items, most page-action buttons) were left alone — that text already serves as their accessible name.
- **Keyboard focus ring silently disappeared on ghost/ribbon/category-tab buttons.** `App.axaml` defines a generic `Button:focus-visible` ring (2px `#75C8FF` border), but `Button.ghost`, `Button.ribbon`/`.ribbonCompact`/`.ribbonMedium`, and `ToggleButton.categoryTab` each set their own always-on `BorderBrush` at equal selector specificity, declared later in document order via `StyleInclude` — so tabbing to the window-chrome buttons, the tab-close "x", any ribbon action, or the 7 category tabs showed no visible focus indicator at all (a WCAG 2.4.7 gap). Fixed by adding matching `:focus-visible` overrides to each of those classes in `DesignSystem.axaml`, re-asserting the same ring color.
- **Tab-close button was a 20x20px hit target**, below Fluent's ~32-40px minimum recommended pointer target and the smallest interactive element in the app, sitting inside a dense horizontal tab strip where mis-clicks were likely once several servers were open. Bumped to 28x28.
- **Window-chrome buttons (Minimize/Maximize/Close) had no `ToolTip.Tip`**, unlike virtually every other icon-only control in the app. Added, alongside their new `AutomationProperties.Name`.
- **A 5th audit finding was investigated and found not to hold**: "no per-action busy/disabled feedback during async operations," citing Save Notes/Add Warning/Kick/Ban/Restart Server/Apply Migration/Apply-With-Fresh-Safety-Backup as examples. Checked each named command directly in `MainWindowViewModel.cs` — every one already gates on `!IsBusy` (plus its own more specific precondition) inside its `AsyncCommand`'s `CanExecute` delegate, which Avalonia's standard `Button`/`ICommand` wiring already uses to auto-disable the button. The audit's own methodology (grepping for an explicit XAML `IsEnabled="{Binding !IsBusy}"` binding) missed this, since the codebase's established pattern gates through `CanExecute` instead. Nothing was changed here — adding a redundant explicit `IsEnabled` binding on top would have overridden that existing precondition gating (e.g. re-enabling Kick/Ban once not busy regardless of whether a player is actually selected and online), which would have been a real regression, not a fix.

## v0.7.13.0 — New-Server Workflow Redesign

- Requested directly, framed explicitly as "the next 0.7 task": a roadmap for opening a new tab that isn't part of the regular chrome and can't do anything until connected, modeled on how other applications build a "new connection" flow — a first Local/Remote step, a network-scan option for Remote, and the regular ribbon/nav chrome hidden until a tab is actually connected or a new server is created. Tested by running 2-3 local servers plus a connection styled after a real remote deployment.
- **The wizard moved out of the Settings page entirely.** Previously "Set Up New Server" was a set of cards embedded in the same card stack used to edit an already-saved profile, gated by `IsCreatingNewProfile`/`IsWizardStepN` conditions mixed in among ordinary editing controls — reachable and escapable through ordinary page navigation, with no visual distinction from regular app chrome. It now lives in a new dedicated host (`MainWindow.axaml`'s `Grid Grid.Row="3" IsVisible="{Binding IsCreatingNewProfile}"`) that replaces the whole nav sidebar, ribbon toolbar, and category tabs for as long as a tab is mid-setup — modeled on how connection-manager apps (SSH/database/remote-desktop clients) keep "New Connection" a focused, separate sequence rather than regular chrome with some buttons disabled. The Settings page's own "Connection profiles" card is simplified back down to editing-only — every wizard-only branch that used to live there was dead code in that location anyway, since `IsCreatingNewProfile` can no longer be true while that page's content is visible.
- **New Step 0 — Local or Remote.** Before Connection Details, a new first step asks which kind of server this is. Choosing **Local** pre-fills the draft URL to the default loopback endpoint and shows a "Local Service" sub-view: an Auto-Detect button (`DetectLocalServiceCommand`, reusing the same `LocalManagementBootstrapper`/service-discovery pieces the existing "Bootstrap Local Service" button has always used) plus manual Name/Service URL fields for a second or third local instance running on a different port. Choosing **Remote** clears the default loopback URL and shows the existing LAN-scan (`DiscoverServicesCommand`) plus manual Name/Service URL/Bearer token/TLS pin fields. `TabSession` gained a new per-tab `ConnectionKind` field ("Local"/"Remote"/empty) for the same reason `WizardStep` is per-tab — two tabs can each be mid-setup with a different choice at once.
- **Verified live against 3 real, simultaneous sidecar instances** (no GUI click-through capability in this environment, the same disclosed gap as every prior version — verification is at the service/API level the wizard's code actually calls): two ordinary loopback sidecars on different ports, both answering `/healthz` at once with distinct process IDs (re-confirming multi-server operation still holds after this restructuring); a third sidecar rebound to the machine's real LAN interface with authentication and TLS both enabled, its `/healthz` correctly reporting `api:"remote-secured", authentication:true, tls:true`, an unauthenticated request correctly rejected with 401 `missing-bearer-token`, and the endpoint reachable exactly as a genuinely separate remote machine's client would reach it (LAN IP, self-signed cert, bearer token) — the same transport/security path the Remote branch's Connect action exercises.
- **A real, unrelated CLI bug found while building the remote test rig, not fixed here**: `mysttiq-server.exe api-remote-enable --bind-address <LAN-IP> ...` cannot actually be used to promote a fresh config to a secured remote binding — `Program.cs`'s blanket pre-command configuration validation runs before `api-remote-enable`'s own handler and rejects the non-loopback bind address against the *old*, not-yet-updated authentication/TLS flags, before the command that's supposed to enable them ever runs. Worked around for this release's own verification by generating the token/certificate directly (`api-token-create`/`api-tls-create`) and hand-writing the resulting config, exactly what `HeadlessRemoteApiEnrollmentService.EnableRemoteApi` would have produced. Flagged as a separate follow-up task rather than fixed here since it's `MystTiq.HeadlessHost` CLI dispatch, unrelated to this release's Desktop-only scope.
- **Backlog, not yet started** (raised mid-session, explicitly queued rather than folded into this release to keep it bounded): a selectable way to de-duplicate player names in the Players list; server tabs expanding to use full window width and collapsing into an overflow ("show hidden tabs + create new") affordance once there are too many to fit; confirmed already satisfied by this release's own restructuring — clicking "+" → "Set Up New Server" already opens a new tab straight into the Step 0 Local/Remote choice with no intermediate prompt.

## v0.7.12.0 — Test Coverage: Whitelist Enforcement & Route Smoke

- Requested directly: a test-coverage audit found that every feature shipped in v0.7.8.0 (Unban/ban-list/teleport/save-now), v0.7.9.0 (server FPS metrics), and v0.7.10.0 (whitelist) had zero automated regression coverage beyond `Test-vX.Y.Z.W-Logic.ps1`'s regex-on-source checks — those prove "the code pattern still exists," not "the endpoint behaves correctly." A working feature silently broken by a later refactor (wrong RCON verb, swapped parameters) would pass every existing check.
- **`HeadlessWhitelistService.EnforceAsync` real-object-graph harness** (new `scripts/Testing/MystTiq.LogicHarness/`, a standalone throwaway C# console project, deliberately not added to `PalworldServerManager.slnx`/`Build.ps1`'s normal pipeline): fakes only the one thing that genuinely needs faking (`IPlayerModerationProvider`), then runs the real `HeadlessWhitelistService` against real `HeadlessPlayersSnapshot` data. 6 scenarios: disabled config takes no action; an enabled config kicks a non-whitelisted player and leaves an allowed one alone; the same still-online player isn't re-kicked on a second poll (the dedup set); a player is re-kicked after leaving and rejoining; saving a new config resets the dedup set; an unavailable snapshot is ignored entirely. This is the one piece of business logic in the whole v0.7.10.0 release that had never actually executed before this.
- **`scripts/Test-v0.7.12.0-RouteSmoke.ps1`** (new, permanent): starts a real isolated sidecar and hits every previously-untested v0.7.8.0/v0.7.9.0/v0.7.10.0 route — `/players/ban-list`, `/world/save-now`, `/players/{id}/teleport-to-me`, `/players/{id}/teleport-to-player`, `/players/{id}/action` with `action=unban` (confirming it resolves to `providerId=rcon`, not the REST-only admin provider), `/players/whitelist` GET+PUT round-trip, and `/metrics`'s `serverFps`/`serverFrameTimeMs` fields. Checks reachability, correct response shape, and the correct graceful-failure behavior when RCON/REST are genuinely unconfigured (the same environment constraint every RCON-touching feature this session has had) — not the routes' real in-game effect, which still needs an actual Palworld server this environment cannot run.
- **Also fixed**: a stale code comment in `src/MystTiq.Core/Providers/ProviderModels.cs` still listing "whitelist, teleport" as part of an undifferentiated deferred-capability list — both shipped (v0.7.10.0, v0.7.8.0 respectively) since that comment was written; noticed while building the harness above and reading that file's context.
- Both new tools are meant to be carried forward in every future version's `-Logic.ps1 -RunBuild` block the same way `Test-v0.5.1.5-RuntimeSmoke.ps1` already is — the audit's own caveat is that nothing *structurally* guarantees this (there's no CI runner; everything is manually invoked), only the same discipline that has kept the existing runtime smoke suite wired in release after release.

## v0.7.11.0 — App Lifecycle: Single-Instance, Tab-Close Confirmation, Tray Reminder

- Requested directly: verify multi-server operation, confirm before closing a running tab, remind the user MystTiq is still running when minimized to tray (only fully closing when nothing is running), and block a second instance from launching. Also folds in a small documentation consistency audit's findings.
- **Window close (X button) no longer always minimizes to tray** — it previously did so unconditionally, with no way to fully quit the app that way even when nothing was running. `MainWindow`'s `Closing` handler now checks every open tab (`Tabs.Any(t => t.ServerIsRunning)`, not just the currently-focused one — Fleet allows several servers running at once) and performs a real, clean exit (equivalent to the tray menu's existing "Exit GUI Only") when nothing is running, only minimizing to tray when something still is.
- **Tray reminder when minimized with something running**: Avalonia's `TrayIcon` has no built-in balloon/notification API (confirmed against the current Avalonia docs and source before building this) — added a small, self-positioned, auto-dismissing `TrayReminderToast` window that stands in for one, shown whenever the main window is hidden to the tray while at least one server is still running.
- **Tab-close confirmation**: closing a tab whose server is running previously always silently left it running with zero confirmation. A new `ConfirmCloseTabDialog` now asks Stop & Close / Leave Running / Cancel first. The tab-close button moved from a plain `Command`/`CommandParameter` binding to a `Click` handler in code-behind (consistent with this codebase's existing pattern of code-behind owning anything that needs a native dialog), and a new `MainWindowViewModel.StopTabServerAsync(TabSession)` stops the *specific* tab being closed — built the same way the existing background-tab lightweight refresh already operates on a tab's own fields directly, never through the `ActiveTab`-delegated properties, since those would silently target whichever tab is currently focused instead of the one actually being closed.
- **Single-instance enforcement**: `Program.Main` now takes a named `Mutex` before starting the Avalonia lifetime; a second launch shows a native message box (Windows) or a console message (other platforms) and exits immediately rather than opening a second GUI against the same local sidecar/config.
- **Multi-server simultaneous operation, verified live**: started two full sidecar instances at once, on different ports, against two separate isolated server roots. Both stayed healthy simultaneously with distinct process IDs; each independently created a backup, and each instance's backup list contained only its own file — confirmed zero cross-contamination between instances. No code changes were needed here (each profile already gets its own fully isolated `IServerPathProfile`/`IServerLifecycleService`/`IOperationCoordinator`-scoped locks per the v0.6.2.0 fleet architecture) — this was a live verification pass, not a fix.
- **Documentation consistency audit**: found and fixed 4 stale labels in `docs/roadmap/` contradicting already-shipped work — two "(Current Candidate)"/"(Current Release Candidate)" headers on long-shipped v0.3.1.0/v0.5.1.1 milestones, a `WINDOWS_BACKPORT_REGISTRY.md` table still showing "In progress"/"Planned" for shipped v0.3/v0.4 items, and three "(planned)" tags on v0.6.16.0–v0.6.18.0 in the grouped roadmap doc that were never updated after those milestones shipped. `README.md`'s roadmap table, `CHANGELOG.md`, and per-version architecture/release-notes files were all confirmed already fully consistent.
- No server-side change — this release is entirely `MystTiq.Desktop` plus documentation.

## v0.7.10.0 — Whitelist System

- Fifth and final release from the broader review requested this session: the last of the verified competitive feature gaps from the survey against other Palworld server-management tools, and — like teleport in v0.7.8.0 — named as an explicit deferred roadmap item in the codebase's own comments (`Providers/ProviderModels.cs`) before this release.
- **`HeadlessWhitelistService`** (new, `MystTiq.HeadlessHost`): opt-in per-profile allow list, persisted as JSON under `ManagerRuntimeRoot/players/whitelist.json`. When enabled, `EnforceAsync` (called from `GET /status/poll`, the same poll cadence `HeadlessPlayerRegistryService.Observe` already uses — no new background timer) kicks any online player whose PlayerId isn't listed, reusing `PlayerModerationCoordinator`'s existing REST-first-RCON-fallback kick path rather than a third, whitelist-specific moderation route. A per-player "already handled this session" debounce prevents re-kicking on every single 5-second tick before the previous kick's effect (the player actually leaving) is observed.
- **New `WhitelistConfig`/`WhitelistEntry` models** (`MystTiq.Core`), new `GET`/`PUT /players/whitelist` routes (`MystTiq.HeadlessHost`) — config is read/replaced as a whole, matching the existing Discord Bot config's GET+PUT convention (add/remove entries locally in the UI, one explicit Save persists the full list) rather than inventing granular per-entry REST endpoints.
- **Desktop**: new collapsible "Whitelist" card on the Players page (collapsed by default, matching the v0.7.5.0 no-scroll pattern and v0.7.8.0's Ban List placement) — enable/disable checkbox, add/remove entries, Save button.
- **Verified live**: started a real sidecar, confirmed `GET /players/whitelist` returns the correct default (`disabled, no entries`), `PUT` persists a new entry, a subsequent `GET` reflects the saved state (confirming real file persistence, not just in-memory), and `/status/poll` continues to respond correctly with the new enforcement call wired into its handler. Testing the actual auto-kick behavior requires real online players against a real Palworld server, which is outside what this environment can run — the same disclosed limitation as every RCON/REST-touching feature added this session.
- This closes out the full v0.7.6.0→v0.7.10.0 sequence from this session's codebase bug scan and competitive feature survey.

## v0.7.9.0 — Real Server FPS/Frame Time

- Fourth release from the broader review requested this session, closing out the verified competitive feature gaps except whitelist. Confirmed via Palworld's official documentation: `/v1/api/metrics` returns `serverfps`/`serverframetime` alongside player/uptime counters MystTiq already tracks by other means — actual in-game simulation performance, which degrades with base/Pal count independent of host CPU%, and the thing operators actually watch for lag. MystTiq previously only surfaced host-level process CPU/RAM, never the game's own perf numbers.
- **`HeadlessMonitoringService`**: new `GetGamePerformanceAsync` reuses the exact REST client construction `GetPlayersAsync` already established (same `PalWorldSettings.ini` reads for `RESTAPIEnabled`/`RESTAPIPort`/`AdminPassword`, same Basic auth) rather than duplicating a second copy of that setup. Best-effort and independent of host-level process discovery: `HeadlessRuntimeMetricsSnapshot` gained `ServerFps`/`ServerFrameTimeMs` (both nullable, same "unavailable vs. genuinely zero" convention as the existing `CpuPercent` field), populated from every return path in `GetMetricsAsync` including the "process not found" branches, since the Palworld REST API's own reachability is independent of MystTiq's process-lifecycle tracking.
- **Desktop**: `RuntimeMetricsSnapshotDto` gained the matching fields; new `ServerFpsText`/`ServerFrameTimeText` on `MainWindowViewModel`. Surfaced on the Monitoring page's Activity & Audit stat row (widened from 3 to 5 columns) alongside the existing CPU/RAM/Threads cards — left the Dashboard's already-dense compact stat tile untouched rather than cramming more into it.
- **Verified live**: started a real sidecar and confirmed the new `serverFps`/`serverFrameTimeMs` fields appear in `/api/v1/metrics`'s response shape and are correctly `null` (not a crash) both with no `PalWorldSettings.ini` present and with `RESTAPIEnabled=True` pointed at an unreachable port. A real Palworld dedicated server with the REST API actually serving `/v1/api/metrics` would be needed to verify the positive-path values, which is outside what this environment can run — the same disclosed limitation as every RCON/REST-touching feature this session.
- No `MystTiq.Core` model changes — the new fields live entirely in `MystTiq.HeadlessHost` and `MystTiq.Desktop`.

## v0.7.8.0 — RCON-Powered Admin Tools

- Third release from the broader review requested this session. This one ships verified competitive feature gaps: a competitive survey of other Palworld server-management tools (PalSupervisor, palworld-admin, PalAdmin) found several claimed advantages that turned out to already be built (scheduled restarts, raw RCON console, RBAC, outbound Discord alerts) once checked against the actual code, but confirmed several genuine gaps — including two the codebase's own comments already named as deferred roadmap items.
- **Unban + Ban List**: Palworld RCON's `UnBanPlayer`/`BanList` commands are now wired through — `RconPlayerModerationProvider` gained `unban` support (Palworld's REST API has no unban endpoint at all, so this is RCON-only by necessity, unlike kick/ban which try REST first). Un-stubs the "Unban" button that had sat disabled in the Players page UI since it was first added. A new collapsible Ban List card (matching the v0.7.5.0 no-scroll pattern) shows the raw RCON ban list.
- **Admin teleport**: "Teleport to Me" (summon the selected online player to your admin character) and "Teleport to Player" (move your admin character to them), via RCON's `TeleportToMe`/`TeleportToPlayer` — both require an admin character actually present in the world, disclosed in the button tooltips. Named as an explicit deferred roadmap item in the codebase's own comments before this release.
- **Save World Now**: one-click force-save via RCON's `Save` command, added to the Dashboard's quick-action row. Previously only reachable by typing `Save` into the raw RCON console by hand.
- **New HeadlessHost routes**: `GET /players/ban-list`, `POST /players/{id}/teleport-to-me`, `POST /players/{id}/teleport-to-player`, `POST /world/save-now` — each a thin, purpose-named wrapper around the same `PalworldRconService.ExecuteAsync` primitive `/rcon/command` already uses, returning the identical `RconCommandResult` shape (no new Desktop-side DTO needed).
- **Real bug found and fixed while wiring the above**: Kick/Ban/Unban/Teleport all read `SelectedPlayerRecord` (the Players page's "Directory" selection) — a *different* property than `SelectedOnlinePlayer`, which v0.7.6.0's stale-tab-data fix had cleared on the assumption (from the bug scan that prompted it) that it was what these actions used. `SelectedPlayerRecord` was left completely unaddressed, and its staleness window is worse than what v0.7.6.0 fixed: it's populated only by a page-navigation trigger (`RefreshPlayersPageAsync`), not the 5-second timer, so it stays showing the previous tab's player directory indefinitely if the user stays on the Players page across a tab switch — exactly the kind of wrong-tab-action risk v0.7.6.0 set out to close, just via a mechanism that inspection missed at the time. Fixed in this release: `ActiveTab`'s setter now also clears `SelectedPlayerRecord` and immediately re-runs `RefreshPlayersPageAsync` for the newly active tab when already on the Players page; `RefreshPlayersPageAsync` itself gained the same stale-response-discard guard added to `RefreshAsync`/`RefreshStatusPollingAsync` in v0.7.6.0.
- No server-side change beyond the four new thin RCON-wrapper routes — no `MystTiq.Core` model changes.

## v0.7.7.0 — Fix: Backup/Restore & Lifecycle Locking Race Conditions

- Second release from the broader review requested this session (bug scan + competitive feature survey). This one fixes every finding from the `MystTiq.Core`/`MystTiq.HeadlessHost` half of the bug scan.
- **`HeadlessBackupService` never registered with `IOperationCoordinator`** — unlike `HeadlessWorldTransactionService`/`HeadlessGuildOwnershipService`/`HeadlessBaseOwnershipService`/`HeadlessCharacterMigrationService` (which all hold the coordinator's `world-mutation` lock for their own save-mutating operations), `RestoreAsync` mutated `paths.SaveRoot` guarded only by its own local semaphore — a restore could run concurrently with one of those transactions on the same save tree. Now takes the same lock; `CreateAsync`/`DeleteAsync`/`VerifyAsync` deliberately stay lock-free since they don't mutate `SaveRoot` and `CreateAsync` is called *by* those already-locked services for their own safety backups.
- **`/diagnostics/network/restart` bypassed the coordinator entirely** — every other lifecycle-mutating route goes through it; this one called the lifecycle service directly. Now takes the same `lifecycle` + `world-mutation` lock as `/server/restart`.
- **A successful restore whose cleanup step failed was misreported as "restore failed"** — the post-restore deletion of the now-redundant rollback copy ran inside the same try/catch as the actual restore, so e.g. a transient file lock on that cleanup (an AV scanner, say) reported total failure even though the save data was already successfully restored. Cleanup failure is now reported as a note on a successful result, not a failure.
- **A rollback-of-rollback could throw and get silently swallowed** — the failure-recovery path deleted whatever the world explorer's own "is this a real world" check reported as the active world, rather than the actual, already-known destination path; content corrupted enough to fail that check (but not `ValidateWorldDirectory`'s own, separate check) left the destination never cleared, so restoring the rollback copy on top of it threw and was silently caught, reporting `RolledBack=false` with no indication that automatic recovery itself had failed. Now deletes the known destination path directly, and a rollback failure is now included in the reported error message instead of being swallowed.
- **Mod-snapshot rollback skipped the ZIP path-traversal guard** used everywhere else in the same file (`InstallZipAsync`'s established pattern) — snapshot ZIPs are normally self-generated, but a rollback should not trust a tampered/corrupted snapshot file on disk any more than a fresh upload.
- This is the one release in the v0.7.6+ line that touches `MystTiq.Core`/`MystTiq.HeadlessHost` rather than being Desktop-only.

## v0.7.6.0 — Fix: Multi-Tab Stale Data (Wrong-Tab Actions)

- Prompted by a full internal review: a codebase-wide bug scan (two independent passes over `MystTiq.Core`/`MystTiq.HeadlessHost` and `MystTiq.Desktop`) plus a competitive feature survey against other Palworld server-management tools, requested to scope v0.7.6.0 and beyond. This release addresses the single most serious finding.
- **The bug**: switching tabs did not trigger a data refresh — `OnlinePlayers`, console logs, dashboard state, and the server-health glow color are plain fields shared across all tabs, updated only by each tab's own 5-second background timer. For up to 5 seconds after switching tabs, the UI kept showing the *previous* tab's data. Concretely: an admin managing a fleet of servers switches from Server A to Server B and clicks Kick/Ban inside that window — the request goes out against Server B's connection using Server A's still-displayed player ID.
- **Fix**: switching tabs now immediately clears any selected online player/backup/mod/Pal/base (so a destructive action can't fire against a stale target left over from the previous tab) and triggers an out-of-cycle refresh of the newly active tab instead of waiting for the next timer tick. `RefreshAsync`/`RefreshStatusPollingAsync` now capture which tab a request was made for and discard the response if the user has since switched to a different tab, instead of letting a late-arriving response for a backgrounded tab silently overwrite the newly active tab's display.
- **Also fixed** (found during the same review): the "Set Up New Server" wizard step was a single field shared by the whole app instead of scoped per tab — two tabs simultaneously mid-setup would corrupt each other's step (moved to `TabSession.WizardStep`). Editing a saved profile's name/URL/certificate pin didn't propagate to any other tab already connected to that same profile, which kept a stale copy indefinitely. Deleting a profile didn't check whether another open tab was actively using it first (mirrors the existing duplicate-tab-open guard). A closed tab's timer is now nulled out after being stopped, not just stopped.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.5.0 — No-Scroll Initiative, Pass 1

- First pass at the deferred second half of v0.7.4.x: reduce how much scrolling most pages need at a typical window size, without cutting any content. A fresh height audit (page heights shift release to release as features land) found Players had become the single worst offender at ~2450px — a direct consequence of relocating the World Map and Pal Editor there in v0.7.1.0 — ahead of Diagnostics Center (~1800px) and Alert Center (~1580px, still three unrelated feature areas concatenated in one page).
- **Real bug fixed along the way**: the Settings page's "Managed Server / Headless Configuration" and "Security" cards had no wizard gating at all — they rendered unconditionally underneath every step of the v0.7.3.0 new-server wizard instead of only appearing once a profile actually exists to configure. Both now follow the same `!IsCreatingNewProfile` gate already used by Save/Delete Profile, so the wizard no longer shows irrelevant, half-populated cards below itself.
- **Global denser spacing** (three small, broadly-applied levers, zero content removed): `Border.card` padding 14→11, the shared `Button`/`TextBox`/`ComboBox` minimum height 34→31, and every page-root `StackPanel`'s spacing 16→12 (18 pages, mechanically verified). Individually small; in aggregate this alone removes a meaningful slice of height from every single page.
- **Collapsible secondary sections** (new pattern: a lightweight `Button.sectionToggle` header with a ▸/▾ indicator, driving a plain bool `IsXExpanded` property — collapsed by default, no new library dependency): Players' World Map (fixed 480×480 canvas) and Pal Editor; Diagnostics Center's WAN/External Reachability and Local Machine Diagnostics; Alert Center's Discord Bot and Anti-Cheat & Save-Integrity Scanning. Each is a large, self-contained, occasional-use tool rather than the page's primary content, so collapsing them by default is a one-click-away trade, not a content cut. Threshold Rules stays expanded on Alert Center (primary content), as does Network Diagnostics on Diagnostics Center.
- Explicitly deferred to a future pass: the Simple Settings tile `WrapPanel` (data-volume-dependent, can grow past 2000px with many settings present), the Doctor page (data-dependent on finding count), and splitting Alert Center's remaining sections into their own reachable nav pages rather than collapsing them in place. The no-scroll initiative remains directional/multi-release, consistent with the original v0.7.1.0→v0.7.4.x plan.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.4.0 — Per-Category Card Flare

- First half of v0.7.4.x, the last item in the v0.7.x UX plan: "add some flare to the different cards." Of 176 total card-styled panels across the app, 172 were completely plain (no color accent at all), concentrated almost entirely on the Dashboard — the user chose a broader decorative pass over a status-only extension.
- **`Styles/DesignSystem.axaml`**: 7 new `Border.card.accentX`/`Border.statuscard.accentX` style pairs, one per nav category (Home/Server/World/Backups/Mods/Tools/System), each a subtle border-tint + soft low-intensity glow (much lower alpha than the existing status-driven glows) reusing that category's own existing tab-accent color family (Blue/Cyan/Violet/Amber/Magenta/Orange/Green respectively) — same visual language as the existing `glowGreen`/`glowRed` cards, just gentler and decorative rather than a status signal. Declared *before* the status-glow selectors in the stylesheet so a genuinely meaningful status glow always wins the rare case where both classes land on the same element.
- **`MainWindow.axaml`**: ~169 `Classes="card"`/`Classes="statuscard"` borders tagged with their page's category accent class (129 card + 40 statuscard), covering every page except the 3 Dashboard cards that already carry their own dynamic status glow (explicitly excluded, so the two systems never compete for the same element) and the always-visible nav-sidebar status footer (not tied to any one category).
- Verified visually before committing: rendered all 7 accent treatments as HTML swatches in the Browser pane and inspected a real screenshot — each category reads as clearly distinct and none overwhelm the existing content, same verification technique introduced in v0.7.0.0.
- No server-side change — this release is entirely `MystTiq.Desktop`.
- The no-scroll initiative (the other half of v0.7.4.x) remains queued, explicitly framed as an ongoing, multi-release effort rather than a single deliverable.

## v0.7.3.0 — New-Server Creation Wizard

- Third slice of the broader v0.7.x UX plan: new-server creation should go through a wizard rather than one flat form.
- Investigation found the existing "Set Up New Server" flow (`OpenNewServerTab()`/`BeginNewProfile()`, already cleanly separated from connecting to an existing profile since v0.6.19.0) already followed a natural 3-step sequence at the command level — connect, optionally create in-game defaults, save the profile — just presented as one continuous flat form with no forced ordering.
- **New `NewServerWizardStep`** (1/2/3) on `MainWindowViewModel`, reset to 1 every time "Set Up New Server" is opened, with `WizardAdvanceCommand`/`WizardBackCommand` and `IsWizardStep1`/`IsWizardStep2`/`IsWizardStep3` (all gated behind `IsCreatingNewProfile`, so editing an already-saved profile is completely untouched by any of this).
- **Step 1 — Connection Details**: the existing Connection Profiles card, unchanged, with a "Next: Server Defaults →" button (enabled once connected) added alongside Connect. Save Profile/Delete Profile — which only make sense once a profile is being edited, not created — now show only while editing an existing profile.
- **Step 2 — In-Game Server Defaults**: the existing card (now including v0.7.2.0's live port-conflict warnings), with "← Back" and "Next: Confirm & Save →" alongside the existing "+ Create Default Settings" button — creating defaults here is optional, so Next is always available.
- **Step 3 — Confirm & Finish** (new): a short summary (server name/address/connection state) plus "← Back" and "Save Profile" — reusing the exact same `SaveProfileCommand` Step 1 always had, not a new save path.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.2.0 — Port-Conflict Warnings for New-Server Creation

- Second slice of the broader v0.7.x UX plan: new-server creation should warn if a typed Game/REST port is already in use by another running server, instead of silently allowing the collision.
- Investigation found a real, working "list every bound TCP/UDP endpoint + owning process" capability already existed (`INetworkDiagnosticsPlatformService.GetTcpListenersAsync`/`GetUdpEndpointsAsync`, Windows via `netstat`, Linux implementation also present) but was only ever invoked for one already-running profile checking its own configured port — never for an arbitrary candidate port during setup.
- **Core**: new `PortAvailabilityService` — a thin wrapper reusing the existing platform capability as-is, answering "is port N in use, and by what" for any port, independent of any configured profile.
- **HeadlessHost**: new standalone `GET /api/v1/diagnostics/port-check?port=N&protocol=UDP|TCP` endpoint (global, not scoped to any server profile, since it's most useful before any profile exists yet).
- **Desktop**: `IMystTiqApiClient.CheckPortAsync`; the "IN-GAME SERVER DEFAULTS" card's Game Port/REST Port fields now check live as you type and show an amber warning naming the process already holding a conflicting port — not a hard block, since a stale/zombie listener shouldn't be able to prevent recovery.
- **Verified live, not just statically**: built the real sidecar, started it, occupied a real TCP port with a listener, confirmed the new endpoint correctly reported `InUse=true` with the actual process name and PID, and confirmed a free port correctly reported `InUse=false` — the strongest evidence available in this environment for a server-side change, going beyond the usual static-contract-check verification.
- This is the one release in the v0.7.x UX plan that touches `MystTiq.Core`/`MystTiq.HeadlessHost` — every other release in the plan is Desktop-only.

## v0.7.1.0 — Page Ordering, Tab Bar & Simple Settings Visibility

- First slice of a broader UX pass, following a research pass across all 22 `MainWindow.axaml` pages plus a deep dive into a reported Simple Settings issue. Full plan spans v0.7.1.0 → v0.7.4.x; see `docs/architecture/v0.7.1.0-*.md`.
- **Page-ordering fixes** (7 concrete issues found by the audit, each a localized reorder):
  - Players: the 480×480 World Map card no longer sits between the search bar and the list it doesn't actually filter — moved below the player list/detail section.
  - Backups: removed the duplicate Verify/Restore/Delete Selected buttons from the top action bar (they already exist, correctly gated, in the "Selected Backup" card below).
  - Automation: the Rules list now appears above the New Rule creation form, so opening the page shows what already exists first.
  - Monitoring / Activity & Audit: the CPU/RAM/Threads stat row now appears above the audit log instead of after it.
  - Fleet: Server Profiles now appears above Clone World, so existing profile IDs are visible before typing a new one.
  - Diagnostics Center: "Restart Server" is now visually separated (danger-styled, behind a divider) from the safe read-only diagnostic actions it used to sit alongside.
  - Settings vs. Workspace: the four server-path fields (`ConfigServerRoot`/`ConfigSteamCmdPath`/`ConfigBackupRoot`/`ConfigRuntimeRoot`) were editable on both pages; Settings now shows a read-only summary with a "Manage in Workspace →" link, leaving Workspace as the one authoritative place to edit them.
- **Pal Editor relocated**: moved from the Guilds page (where it was wedged between Guild Ownership Operations and the page footer, with no conceptual tie to guild ownership) onto the Players page, alongside the rest of this app's player-focused tools.
- **Simple Settings visibility**: `RebuildSimplePalworldSettings()` previously silently skipped any of the 34 curated settings not found in the live `PalWorldSettings.ini` (e.g. an older Palworld server build) with zero indication. Now shows a visible amber note naming which curated settings aren't available on this server. The Simple Settings code itself was already correct against the v0.6.18.3 design (34 settings, 3 labeled sections, filter above both views) — there was no bug to fix there.
- **Tab bar**: each tab now shows the server name with "Local"/"Remote" underneath instead of the connection URL (`TabSession.ConnectionKindText`, new); tabs widened slightly (180→200 min width) to give the name more room.
- **"+" icon**: replaced the earlier capture-sphere-styled icon with a plain plus sign, per feedback that the request was specifically for an actual plus symbol.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.0.1 — Fix Release (Fleet Page Nav Sidebar Bug)

- User report, with a screenshot of the running app: the Fleet page's left nav sidebar was completely blank while on the Fleet page itself, and none of the top category tabs (Home/Server/World/.../System) appeared selected.
- Root cause: `IsV5SystemCategory` (which gates the entire System category's nav sidebar `StackPanel`, and also drives the "System" category tab's `IsChecked` state) checked `SelectedPage` against every System-category page **except** `NavigationPage.Fleet` itself. The moment you actually navigated to Fleet, its own category's visibility check evaluated false — the whole nav sidebar vanished, and the System tab lost its checked state, making Fleet look and feel disconnected from the rest of the app the instant you opened it. This was a pre-existing bug, not something introduced by (or fixed by) v0.6.19.1's earlier Fleet page changes — the previous fix addressed the page's *content* (status dots, error messages), not this navigational bug, which is why the user reported Fleet as "still orphaned" afterward.
- Fixed by adding `NavigationPage.Fleet` to `IsV5SystemCategory`'s pattern match. Also fixed the same omission in `IsSystemGroupSelected`, an unused-in-XAML sibling property with the identical bug, to avoid the same mistake resurfacing if that property is ever wired up later.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.7.0.0 — Themes, Skins, UI Personalization & Custom Icon Set

- The next roadmap line, unscoped since v0.3.0.4: "Themes, skins, UI personalization and original Palworld-inspired MystTiq icon set." Investigation found a single hardcoded dark palette with no theme-swap mechanism, ~55 gradient resources whose stops were hardcoded hex (not derived from the base palette), and no image-generation capability in this environment for literal custom art.
- **Real accent + light/dark theming**: 4 accent themes (Default/Emerald/Crimson/Violet) × true Dark/Light variants, selectable from a new "Appearance" card on the Settings page, applied live (no restart) and remembered across launches (`Services/LocalThemePreferencesStore.cs`, mirrors the existing `LocalMapPreferencesStore` pattern).
- `Services/ThemeCatalog.cs` + `Services/ThemeApplier.cs`: every swappable resource (9 structural surface colors, 3 semantic colors, 5 accent colors, ~76 gradient-stop resources across the ~20 gradients that define the app's primary chrome — buttons, cards, the command bar, the nav sidebar, the tab bar, category-selection glass, success/warning/danger) is written directly onto `Application.Current.Resources` on selection; every consumer already binds via `DynamicResource`, so the whole UI re-flows live.
- The Default+Dark combination is byte-for-byte identical to the pre-v0.7.0.0 hardcoded values — today's look is unchanged unless a theme is actually picked.
- Migrated `MainWindow.axaml`'s ~85 previously-hardcoded inline status/severity `Foreground`/`Fill` colors (green/red/amber/blue/cyan/violet) to the same theme-aware brush resources, so status text and dots also follow the selected theme instead of staying fixed dark-tuned hex.
- **A small, original, hand-authored vector icon set** (`Styles/IconGeometries.axaml`): 8 icons (monitor+pulse for Dashboard, wrench for Server Setup, paw print for Players, hub-and-spoke network for Fleet, shield for Security, tent for Bases, vault for Backup Center, a capture-sphere motif for the "+" tab button) — generic monster-taming/base-building iconography, not any specific Palworld character or logo art. Verified visually before committing (rendered as SVG in the browser pane and inspected via screenshot, then translated to Avalonia `StreamGeometry` path data) rather than guessed blind.
- **Disclosed, bounded scope** (see the architecture doc for the full rationale): the many smaller decorative `BorderBrush`/`BoxShadow`/`Effect` colors scattered through individual `<Style>` selectors in `DesignSystem.axaml` are not covered this pass — `BoxShadow`'s shorthand string syntax cannot bind to `DynamicResource` at all (a hard Avalonia limitation), and the rest are secondary hover/glow accents rather than primary surfaces. They stay at their current dark-tuned values in every theme/variant. The remaining ~17 nav-sidebar destinations keep their existing Unicode-glyph icons rather than getting custom vector icons this pass.
- No server-side change — this release is entirely `MystTiq.Desktop`.
- **Disclosed verification gap, larger here than any prior milestone**: no GUI click-through capability in this environment. This is an inherently visual feature; verification here is compile-clean plus static contract checks proving the wiring is structurally correct, not a visual/behavioral confirmation of the finished app. Actual contrast/readability of the light theme and the on-screen look of each accent palette and the new icons are the user's to review once built.

## v0.6.19.1 — Ribbon Cleanup and Fleet Page Fixes

- User feedback: "the ribbon tab items that mirror the navigation pane... we dont need both," plus "the fleet navigation is missing other UI components."
- **Ribbon cleanup**: removed the five category-conditional ribbon groups (World/Server/Mods/Tools/System) that exactly duplicated, button-for-button, that same category's left nav sidebar list — e.g. the "Server" ribbon group's Setup/Config/Console/Workspace buttons were identical shortcuts to the Server category's own nav sidebar. The always-visible "Server Control" (Start/Stop/Restart/Refresh) and "Quick Actions" (Backup/Console/Doctor) ribbon groups are untouched — they aren't per-category nav duplicates (Server Control has no nav-sidebar equivalent at all; Quick Actions is a fixed cross-category shortcut set, not a same-category mirror).
- **Fleet page**: the Server Profiles list showed only plain status text with no visual indicator, unlike every other status surface in the app (tab bar, dashboard glow cards) — added a colored status dot (green running / red crash detected / grey stopped), reusing the same status-color pattern from v0.6.19.0's tab bar. The "Last Fleet Action Results" list showed only a server ID and a bare `Success` boolean — a failed action's actual `Error` message (already returned by the API, just never bound) is now shown beneath a failed row.
- No server-side change — this release is entirely `MystTiq.Desktop`.
- The Fleet feedback was broad ("missing other UI components"); these two fixes address the concrete, unambiguous gaps found by inspecting what data the API already returns but the page didn't show. Broader Fleet page additions are left for a future pass if more specific feedback follows.

## v0.6.19.0 — True Multi-Tab Server Connections

- User feedback on the tab bar: pressing "+" should open a new tab that either launches a new server or connects to an existing one, and should never let a tab connect to a server that's already open in another tab. Investigation showed the existing "tabs" were really a bookmark list over one shared connection — switching which profile was "selected" tore the whole connection down and rebuilt it (wiped the bearer token, reset connection state). Presented as a scope choice (a smarter one-connection-at-a-time picker vs. true simultaneous multi-tab); the user chose true multi-tab.
- New `TabSession` (`ViewModels/TabSession.cs`): per-tab connection state — profile, editor fields, bearer token, connection/busy/running flags, and its own 5-second poll timer. `MainWindowViewModel.SelectedProfile`/`ProfileName`/`ServerUrl`/`CertificateSha256`/`BearerToken`/`ManagementApiConnected`/`ConnectionState`/`IsBusy`/`ServerIsRunning` are now pass-through accessors over whichever `TabSession` is `ActiveTab`, so the ~170/~131 existing call sites reading `SelectedProfile`/`BearerToken` keep working unchanged, now scoped to the active tab instead of the whole app.
- **Two-tier polling**: the active tab's timer runs the same full page-data refresh the old single shared timer always ran; a background tab's timer runs a lightweight, tab-scoped health poll only (keeps its bearer token alive, keeps its own status dot accurate) — not the full Console/World Explorer/Player Registry/Configuration refresh. Switching tabs is instant (no re-auth) with a data refresh on focus, the same latency as switching nav pages today.
- **"+"** now opens a chooser (Set Up New Server / Connect to Existing Server), the latter filtered to profiles not already open in another tab — the duplicate-connection guard lives at this one "open a tab" step rather than scattered through the codebase.
- Tab bar item now shows each tab's own live status dot (green connected / amber connecting / red failed / grey idle) and a close button; the last remaining tab can't be closed.
- **Explicitly deferred this pass**: full live page data kept warm per background tab (only connection + a lightweight status summary stay live); a zero-tabs empty state; restoring every previously-open tab across an app restart (relaunch still opens with one tab, today's existing behavior).
- No server-side change — this release is entirely `MystTiq.Desktop`.
- **Disclosed verification gap**: this environment cannot click through the Avalonia GUI. Verified everything provable without one — compile-clean, the property-delegation wiring is structurally correct by inspection, the duplicate-guard filter is present and correct. Real interactive confirmation (opening two tabs, confirming both stay connected, confirming a third tab can't reconnect to an already-open profile) is the user's to do once built — the same standing gap disclosed in every checkpoint since v0.6.4.0.

## v0.6.18.4 — Server Control Button State

- User feedback: the Start/Restart/Stop ribbon buttons stayed clickable regardless of whether the server was actually running or stopped.
- New `ServerIsRunning`, set directly from the real status poll (`ServerStatusDto.NativeProcessId.HasValue || Ready` — the exact same "is the server running" definition already proven server-side for gating `HeadlessPalEditService.ApplyAsync`'s precondition, not re-derived from scratch). `NativeProcessId` alone catches "process launched but not yet Ready" during startup, so Start doesn't stay clickable mid-launch.
- `StartCommand` now also requires `!ServerIsRunning`; `StopCommand`/`RestartCommand` now also require `ServerIsRunning`.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.6.18.3 — Configuration Page Overhaul

- Eight pieces of direct user feedback on the Configuration page, acted on after investigating what each part actually did before changing anything.
- **Quote-stripping**: Server Name/Description/Admin Password/Server Password now display without the INI's literal quote characters; MystTiq re-adds them automatically when writing to `PalWorldSettings.ini`. Purely a Desktop display concern — Core already round-tripped the real value correctly.
- **"Generate Server Name"** moved into the Server Identity section next to the field it actually populates (it was never broken, just misplaced in the preset row).
- **QoL presets now auto-apply** on selection — the separate "Apply Preset" button is gone. A new **"Save As Preset"** feature lets you save whatever is currently loaded as a new named preset, stored locally on this machine — that's how a "MystTiq" preset gets created too, not a value map guessed and baked into shipped code.
- **Advanced Settings now highlights, live**, any row whose current value differs from Palworld's default — updates as you type, not just what was already non-default when the page loaded.
- **Simple Settings expanded from 8 to all 34 settings** in the codebase's own already-curated (but previously unused) list — organized into Server Identity, Network/Access/Limits, and Gameplay Rates groups, with the right control per type (slider, checkbox, or text field).
- **Search/filter moved** to sit directly above the settings list, and now applies to Simple Settings too (previously Advanced-only).
- Removed the page's redundant header card and a second dead, always-hidden legacy block found while reading the page; Import/Export are now the first, most prominent actions.
- No server-side change — this release is entirely `MystTiq.Desktop`.

## v0.6.18.2 — Server Setup Page Cleanup

- User feedback on the Server Setup page: the top hero card was "useless," the "First-Run Server Defaults" card didn't belong on a settings page someone might revisit, and "Check for Updates"/"Install Missing" duplicated functionality that already lives on Update Center.
- Removed the hero card outright — its description text was a verbatim duplicate of what the shared global page header already shows above every page. Its one real piece of content (the ENVIRONMENT HEALTH badge) now lives in an expanded 4-up stat row alongside Components/Ready/Attention, with a single Verify Files action folded in.
- Removed "Check for Updates" and "Install Missing" from Server Setup entirely — a code comment already on record confirmed these were thin duplicate wrappers around Update Center's own authoritative implementation (`PreviewDistributionPlanAsync`/`UpdatePalworldServerAsync`), which remains the one real place this mutation happens. The per-row "Install" action for a genuinely missing component (SteamCMD/Palworld Dedicated Server) is untouched.
- "First-Run Server Defaults" relocated to the Settings page's connection-profile editor, visible only while creating a brand-new profile (`SelectedProfile is null`) — reuses every existing field/command as-is, now labeled "In-Game Server Defaults (new server only)" to read as clearly distinct from the connection Profile Name directly above it.
- No server-side changes — this is entirely `MystTiq.Desktop` view-model/XAML.

## v0.6.18.1 — Fix Release (Code Review Findings)

- A user-requested code review of the four milestones built this session (v0.6.15.0–v0.6.18.0) found and fixed three real logic bugs:
- **Pal Editor**: `HeadlessPalEditService.VerifyMutation`'s Gender check used `EndsWith("Male")`, which also matches "Female" (it ends in "...male" case-insensitively) — the independent re-decode verification step could never actually catch a failed Male-gender mutation. Fixed with an exact-suffix comparison.
- **Anti-Cheat**: the Pal stat-anomaly cooldown key was keyed by owner (or the shared literal `"(unowned)"`), not by Pal instance — a second anomalous Pal owned by the same player, or a second unowned anomalous Pal, was silently dropped for up to 30 minutes with no notification or log entry. Fixed by keying the cooldown on the Pal's own instance ID; enforcement (Kick/Ban) still correctly targets the real owner.
- **Discord Bot**: the "3 consecutive 401s → mark Failed" bad-token detector scanned every Discord.Net log line regardless of severity for a bare `"401"` substring, risking a false-positive teardown of a healthy, long-running connection on unrelated Debug/Verbose gateway traffic (session IDs, latencies, snowflake ID fragments). Fixed by scoping the check to Warning-or-worse severity from the `"Gateway"` source specifically, matching the exact real pattern observed live.
- No new features, no schema changes. Full solution rebuild is clean (0 warnings/0 errors) and the v0.6.18.0 static logic gate (18/18) still passes.

## v0.6.18.0 — Anti-Cheat & Save-Integrity Scanning

- **The fourth and final finding from the v0.6.15.0 competitive survey**: real, cited grounding — SteamID64's genuine 17-digit format, and Palworld 1.0's real vanilla level cap of 80 — rather than guessed thresholds, since neither the survey nor its cited competitors (PalSupervisor, Sphere) publish concrete numbers.
- Three configurable rules, each defaulting to `Flag` (notify + log only — **no player is ever auto-kicked/banned until an admin explicitly opts a rule into enforcement**): invalid Steam ID on join, impossible player/Pal level (configurable maximum, default 80), and Pal stat anomaly (Level/Talent/Rank outside the same bounds the v0.6.15.0 Pal Editor already enforces on write).
- Live checks run on the existing 15-second automation background loop — no new polling. The Pal stat scan reuses `HeadlessPalEditService.ListPalsAsync` exactly as-is, strictly read-only, self-throttled to every 10 minutes since it requires a full `Level.sav` decode.
- `Kick`/`Ban` responses reuse `PlayerModerationCoordinator` — the exact mechanism the v0.6.17.0 Discord bot's `/mysttiq-kick`/`/mysttiq-ban` commands already call.
- New "Anti-Cheat & Save-Integrity Scanning" card on the Desktop's Alert Center page.
- This closes out the full four-milestone sequence the v0.6.15.0 competitive survey scoped (v0.6.15.0 → v0.6.18.0).

## v0.6.17.0 — Two-Way Discord Bot Control

- **The third finding from the v0.6.15.0 competitive survey**: before designing anything, the roadmap doc's premise ("MystTiq's Discord dispatch is outbound-only today") was re-checked against the actual code and found wrong — the only Discord-related code anywhere was one unimplemented enum case that just logged "not implemented." This release builds real Discord integration for the first time, in both directions.
- **Outbound fixed**: notifications routed to the "Discord" channel now post a real, formatted embed to a configured Discord webhook URL — no bot required for this half.
- **Inbound, new**: a real Discord bot (`Discord.Net.WebSocket`, new dependency) connects outbound to Discord's gateway and registers eight guild-scoped slash commands — `/mysttiq-status`, `/mysttiq-players` (Viewer), `/mysttiq-start`, `/mysttiq-stop`, `/mysttiq-restart`, `/mysttiq-broadcast` (Operator), `/mysttiq-kick`, `/mysttiq-ban` (Admin) — gated by a configurable Discord-role-to-MystTiq-role mapping checked against the caller's real Discord roles. Every command composes directly onto existing lifecycle/RCON/moderation services; no new game-control logic.
- Bot token is write-only over the REST API — `GET /notifications/discord-bot` never returns it, only whether one is configured. Discord-triggered start/stop/restart share the same `OperationCoordinator` lock as REST- and Desktop-triggered lifecycle actions, so they can never race each other.
- **A real bug found and fixed during this release's own live verification**: Discord.Net's connection manager retries a gateway 401 indefinitely by design, so an invalid bot token would have hammered Discord's real servers forever without ever surfacing a clear failure. Fixed: three consecutive real 401 responses from Discord and the bot now stops, marks itself `Failed`, and logs why — re-verified live against Discord's actual servers.
- New "Discord Bot" card on the Desktop's Alert Center page.

## v0.6.16.0 — Live World Map (Player Positions)

- **The second finding from the v0.6.15.0 competitive survey**: six separate competitor tools have a live world map showing player positions — the single most common feature MystTiq lacked entirely. This release closes that gap, scoped to player positions only.
- Grounded in real, cited research: Palworld's own official REST API (`GET /v1/api/players`, already polled by MystTiq every status tick) returns real, live `location_x`/`location_y` per online player — confirmed against the official docs and cross-checked against an independent OpenAPI spec. No mod, no save-file decoding, and no new data source needed; the fields were simply never captured before.
- New "World Map — Live Player Positions" card on the existing Players page: auto-fit plotting of every online player with a valid position, refreshed on the existing 5-second poll cadence. A player with a missing/unparseable coordinate is excluded from the map rather than plotted at a wrong default.
- Ships with a plain coordinate-grid background (Palworld's actual map art is Pocketpair's copyrighted asset). "Browse for Map Background" lets you drop in your own sourced/licensed image at any time, stored as a Desktop-local preference only; "Clear Background" reverts to the grid.
- Wild Pal positions (not exposed by Palworld's REST API) and base positions (needs new, unbuilt `Level.sav` decode work) are explicitly deferred, not silently dropped — see `docs/architecture/v0.6.16.0-live-world-map-player-positions.md`.

## v0.6.15.0 — Save-Data Edit Engine Foundation (Pal Editor)

- **The top finding from a user-requested competitive survey of 21 other Palworld server-management GitHub projects**: three independent, actively-maintained tools with hundreds of stars each (PalworldSaveTools, Palworld Save Pal, Palworld Pal Editor) do real item/Pal-stat/inventory editing MystTiq couldn't do at all — a gap explicitly deferred since the v0.6.7.0 checkpoint. This release builds that foundation for individual Pals.
- New Pal Editor: edit a Pal's Nickname, Level, Rank, the three IVs (Talent HP/Shot/Defense), Gender, and its Lucky (`IsRarePal`) flag, on the same Preview → Safety Backup → Server-side Transaction → Verify → Apply pipeline already proven for Guild/Base Ownership repair — including the same "refuses while PalServer is running" and "stale preview is rejected" safety guarantees.
- Grounded in real, cited research into `palworld-save-tools`'s actual save format, then cross-checked directly against a real, already-decoded production save on this machine before writing any code — not guesswork.
- **A real bug found and fixed during this release's own live verification**: the first test against real save data returned zero owned Pals out of 85 real ones, including Pals independently confirmed to be owned. Root cause: ownership was read from the wrong field (the map entry's `key.PlayerUId`, which is always zero for a genuine Pal — it only carries meaning for a player's own character-body entry) instead of `OwnerPlayerUId` inside the Pal's own save data. Fixed and re-verified live: 76 of 85 real Pals now correctly resolve to their real owning player.
- Live-verified end-to-end: applied a real edit to a real owned Pal (Level 7→50, toggled Lucky, renamed), confirmed via an independent fresh read that every changed field landed exactly as requested while every untouched field stayed byte-identical — genuinely surgical, not a wholesale rewrite.
- New roadmap sequence added for the findings beyond this release: Live World Map (v0.6.16.0), Two-Way Discord Bot Control (v0.6.17.0), and Anti-Cheat & Save-Integrity Scanning (v0.6.18.0) — see `docs/architecture/v0.6.15-plus-competitive-survey.md` for the full 21-repo comparison.

## v0.6.14.0 — Console Live-Refresh Fix

- **Fixes a real, directly user-reported bug**: the Console page showed nothing during a server Start/Stop/Restart. Root-caused to two independent, compounding bugs, both fixed and both verified live against a real running PalServer instance.
- **Bug 1**: the passive 5-second auto-refresh timer skips its tick entirely while a lifecycle operation is in flight (`IsBusy`), and the Console page was only refreshed once — after the whole operation finished. A real Start/Stop can take anywhere from several seconds to the configured timeout, so the console stayed frozen for that entire window, then dumped everything at once at the end. Fixed with a lightweight, console-only tail poll that now runs concurrently with the operation itself, cancelled the moment it completes.
- **Bug 2 (found live while verifying bug 1's fix)**: the server-side multi-source log merge (`HeadlessMonitoringService.GetLogTail`) applied a trailing global crop to the concatenated result of all sources, which could — and, reproduced live, did — completely evict the freshest source (MystTiq's own real-time lifecycle/stdout narrative) whenever a later, more voluminous source (a real historical `AdminCommands` log) filled the requested window on its own. Fixed by removing that trailing crop; every source is already independently bounded, so nothing is unbounded, and nothing gets silently zeroed out anymore.
- Live-verified: a real Start operation's fresh `[MYSTTIQ] PalServer ready...` line and real-time MOD pre-start diagnostics now appear in the console tail the moment they're written, alongside historical log sources that used to crowd them out entirely.

## v0.6.13.0 — Fleet-Wide Crash Recovery

- **Closes a real architectural gap the v0.6.12.0 gap audit flagged**: crash-detect-and-auto-restart (`HeadlessSupervisor`) was only ever active inside the installed OS service (`service-run`), and only for the default profile. Since Clone World (v0.6.10.0) makes creating additional profiles routine, this meant every non-default profile — and every profile at all under `api-run`, the mode Desktop's own sidecar actually uses — had zero crash recovery.
- Every `api-run` session now gets automatic, per-profile crash recovery for its whole fleet. New `HeadlessFleetCrashRecoveryService` reuses `HeadlessSupervisor`'s already-proven crash-detect/backoff/window logic (extracted into `RunCrashRecoveryLoopAsync`) — including its existing safety guard that never auto-restarts a profile an admin or Idle Auto-Stop intentionally stopped. Started and stopped exactly where `HeadlessAutomationService` already is, per profile.
- New `--server-id <id>` CLI option lets an admin install a genuinely independent, unattended, boot-time-supervised OS service per profile — `service-install`/`service-uninstall`/`service-status`/`service-run` and the direct single-server verbs (`status`/`start`/`stop`/`restart`) all accept it. The default profile's service/unit name is byte-identical to every prior version (an upgrade never orphans an already-installed service); a non-default profile gets a clearly suffixed name (`MystTiqPalworld-{id}` on Windows, `mysttiq-palworld-{id}.service` on Linux).
- Verified live, end-to-end, against two genuinely running PalServer instances (source + a Clone World target) on an isolated copy of production-derived data: force-killed a non-default profile's process directly to simulate a real crash, confirmed it was detected and auto-restarted within the configured backoff window with a fresh PID while the untouched sibling profile was never affected, then confirmed an intentionally-stopped profile stayed stopped and was never auto-restarted across multiple poll cycles.

## v0.6.12.0 — Gap Audit, Real Bug Fixes & Logic/Runtime Test Pass

- Full project gap audit: reviewed every prior checkpoint's own "Known gaps" disclosures (v0.5.1.5 through v0.6.11.0) plus live bug/logic testing against real isolated data, and added the results to the roadmap as this milestone.
- **Fixes a real, ~9-month-old bug**: the Players/Guilds/World Explorer read-only views relied on a static `Level.sav.json` sidecar that nothing ever regenerated after a guild/base/character mutation committed (first disclosed v0.5.2.0, reconfirmed unfixed through v0.6.7.0). Every mutation service already independently re-decodes the just-committed save as its own verification step — that JSON was just being thrown away instead of also being persisted to the sidecar. Fixed with `HeadlessSaveCodecService.RefreshExplorerSidecar`, wired into all four commit sites (Guild Ownership, Base Ownership x2, Character Migration). Verified live end-to-end: a real Transfer Leadership operation against isolated production-derived save data correctly updated the sidecar and the explorer view immediately reflected the new leader, with no external regeneration step needed.
- **Fixes a real bug found by this session's own bug-testing methodology**: `config-write-default` silently ignored its own documented `--server-root`/`--steamcmd`/`--backup-root`/`--runtime-root` CLI overrides (it wrote before the override logic every other command uses ever ran), always producing a config with the hardcoded built-in default path regardless of what was passed — caught as a genuine near-miss while setting up an isolated test environment. Fixed by threading the same override-application pattern through it.
- **Found, documented, not fixed (low severity)**: automation rule creation accepts negative/invalid numeric trigger values (e.g. `idleThresholdMinutes: -5`) without validation. Not a safety bug — the evaluation path already clamps to a safe minimum at use-time — but the persisted/displayed value doesn't match what's actually enforced.
- Live bug-testing pass against isolated, production-derived data covered malformed JSON, invalid operation types, expired preview tokens, cloning onto self, and more — no unhandled exceptions found. Full existing regression/runtime-smoke suite (29 real checks against a genuinely running headless API) re-run and passing.
- Documented remaining real gaps in the roadmap: two carried-forward verification gaps still open (elevated Windows Service live cycle, Linux deployment of latest source — both blocked on resources this session doesn't have), and three real architectural gaps scoped for a future milestone (fleet-wide crash-recovery supervision, per-page server selector wiring, cross-profile automation rules).

## v0.6.11.0 — Stale-Instance Fix, UI Polish & WAN Reachability Diagnostics

- Fixes a real bug found live this session: `LocalManagementBootstrapper`'s "reuse an already-running local instance" check probed a coarse, historically-always-1 `apiVersion` integer and never compared the actual version string. A leftover v0.5.5.0 `mysttiq-server.exe` from an earlier session was still listening on the default port, and a freshly-launched v0.6.10.0 Desktop silently attached to it instead of starting its own backend — explaining a visible (not hidden) PalServer console window, a stale Console page, and a "Stopped / Not ready" status mismatch. Fixed with a new `VersionMatches` check comparing the probed version against the running assembly's own version; on a mismatch, MystTiq now starts a fresh backend on a free loopback port and tells the user plainly that an older version is still running elsewhere, rather than silently reusing it. Confirmed live: the real stale v0.5.5.0 process's `/healthz` reports `"version":"0.5.5.0"`, which the new check correctly rejects against the current build.
- UI polish: Start button softened to a green/success style matching Stop's intensity (both less saturated than before); the nav-selected background darkened from a light-lavender fade to match the header tab's dark tone; a "Palworld Server Manager" tagline added next to the MystTiq wordmark in the header.
- Adds WAN / External Reachability diagnostics, closing the last gap in this session's network-checks request (local Windows Firewall inspection/repair for the game port already existed and was already wired into the Diagnostics Center page — nothing to build there). New `WanReachabilityService` (platform-independent, hand-rolled UPnP IGD client, no new NuGet dependency): detects the machine's public IPv4 via a single well-known endpoint, and queries the LAN router via SSDP/SOAP for an existing UPnP port-forward mapping on the configured game port, with a matching "Add UPnP Port Mapping" repair action mirroring the existing firewall-repair UX. True internet-side UDP reachability confirmation needs infrastructure this project doesn't own, so that last mile ships honestly as a manual hand-off (Copy IP:Port / Open Port Checker) rather than a faked pass/fail. Verified live against this machine's real router and real internet egress: real public IP detected, real UPnP IGD ("Arcadyan OpenWrt Device") discovered via SSDP, and a real "not currently mapped" result correctly reported for the configured port.
- New "WAN / External Reachability" card on the Diagnostics Center page, right after the existing Network Diagnostics card.

## v0.6.10.0 — Clone World & Extended Live Verification

- Adds a real Clone World feature: `HeadlessWorldCloneService` duplicates a server profile's entire installation (game binaries + world/config) into a brand-new, independent profile — a full real copy, not a shared junction, so future SteamCMD updates to one instance never affect the other. Assigns distinct ports and reuses the existing v0.6.2.0 fleet-registration path unchanged. New `POST /server/clone` route and a Desktop Fleet-page card.
- Real bug found and fixed during live verification: editing `PalWorldSettings.ini`'s `PublicPort` alone does not control PalServer's actual UDP bind port — a live two-instance test found the clone silently falling back to the next free port instead of its configured one. The game port is actually controlled by the `-port=` launch argument; fixed by injecting it alongside the ini edit, then re-verified live that both instances bind their correct, distinct ports.
- Extended live verification this session hadn't yet performed: a genuine sustained PalServer run (started via MystTiq's own lifecycle route, not a side-channel launch); two real, simultaneously-running PalServer instances confirmed independent at the OS socket level and stable across a 5-minute polling window; a real authenticated cross-machine HTTPS connection from this Windows machine to an independently-running Linux MystTiq instance (401 correctly rejected without auth, 200 with the real bearer token); and full end-to-end verification of v0.6.9.0's previously-deferred Idle Auto-Stop (idle detection → warning countdown → final recheck → real stop, with a separate profile proven unaffected).
- Observed, not chased down: graceful shutdown consistently fell back to forced termination across every stop this session (the fallback worked correctly every time — not a defect, but worth a closer look); a stale-crash-state edge case after force-killing the MystTiq management process itself (not a normal admin workflow) that required an explicit Start, not just Stop, to clear.

## v0.6.9.0 — Advanced Intelligence, Remote Clients, Simulation & UX Completion (real foundation slice)

- Adds Idle Auto-Stop with warning and final player recheck: a new `AutomationTriggerKind.IdleEmpty` on the existing v0.6.1.0 automation engine fires a `StopServer` action once the server has had 0 online players continuously for a configurable number of minutes. Reuses the already-proven warning-countdown RCON broadcast and lifecycle-stop execution path completely unchanged — this only adds the idle-detection layer on top.
- Immediately before the actual stop (after any warning countdown finishes broadcasting), a live player count is fetched one more time and the stop is aborted — not just delayed — if a player joined during the countdown. This is the roadmap's specifically-named "final player recheck," and it's a genuinely new safety check, not decorative.
- Configurable from the Desktop Automation page like any other trigger kind (a threshold-minutes field alongside the existing time-of-day/interval fields).
- Confirmed against `docs/roadmap/PRODUCT_ROADMAP.md` before building: this is a narrow, single-server, opt-in convenience, genuinely distinct in scope from the future v0.8.x multi-tier adaptive fleet resource-policy engine — building it now does not duplicate that later work.
- Verified live against an isolated instance: created a real IdleEmpty rule via the API and confirmed it round-trips correctly, confirmed it's permanently excluded from the fixed-schedule `NextDueUtc` polling path (stays `null` through creation/disable/re-enable), and confirmed several real automation ticks ran cleanly with no errors. Full end-to-end idle-to-stop verification wasn't performed this session — reaching a genuine "Running" status requires a real PalServer process with its UDP game port confirmed open, which no synthetic stand-in can satisfy without an actual dedicated server.
- Explicitly deferred, not silently dropped: versioned game-data metadata generation and diffing, anti-cheat/world-integrity scanning (both need Pal/save-struct decode infrastructure that still doesn't exist anywhere in this codebase), scheduled availability/automatic start, web/mobile administration, the Remote Operations Center, simulation/mock providers, deterministic GUI/service acceptance scenarios, resource-based localization, portable Windows mode, and dashboard customization.

## v0.6.8.0 — MOD/UE4SS Platform & Monitoring (real foundation slice)

- Adds MOD backup, staged install & rollback: `CaptureSnapshot`/`RollbackAsync` on the already-mature `HeadlessModManagementService` — a lightweight, package-scoped snapshot (one per MOD, overwritten on each new mutation) is now taken automatically before every Install and Delete. `RollbackAsync` restores the prior version's exact content, or — when a package didn't exist before its last install — removes it entirely, via an explicit absent-marker rather than silently no-op'ing either case. New `POST /api/v1/mods/{type}/{package}/rollback` route and a "Rollback Selected" button on the Desktop MOD Dashboard.
- Adds Alert Center integration for MOD/UE4SS-specific health: a new `ModHealthDegraded` rule on the existing v0.6.1.0 Alert Center (composition, evaluated on its existing throttled tick, no new background loop) fires a real notification naming the specific MOD(s) and their health state whenever the existing MOD inventory health computation reports `Degraded` — previously only visible if an operator happened to check the MOD Dashboard page themselves.
- Verified end-to-end against an isolated instance: overwrite-then-rollback restored exact prior content; fresh-install-then-rollback correctly deleted a MOD that never existed before; delete-then-rollback correctly restored it; rollback of an untouched package failed honestly rather than pretending success; an induced real Misconfigured MOD (mismatched `.ucas`/`.pak`/`.utoc` set) correctly triggered a real Alert Center notification on the next evaluation tick, naming the exact MOD and its health state.
- Explicitly deferred, not silently dropped: mod load order/dependencies/profiles; release discovery and one-click update; Nexus integration; latest-vs-tested-vs-installed version tracking; PalDefender-specific special-casing (it already surfaces through the existing generic UE4SS scanner like any other MOD); the explainable/reversible/audited optimization advisor.

## v0.6.7.0 — Safe World Editing, Administration & Player Recovery (real foundation slice)

- Adds `RemoveBrokenMember`, a new operation on the already-proven `HeadlessGuildOwnershipService` (composition, not a new engine) — removes a guild member reference that has no matching player save file. This is the one legacy-repair capability from the old WPF app's `GuildBaseRecoveryService` scan findings ("Repair membership") not already covered by the existing Claim/Transfer/Add-Player/Base-Transfer/Base-Recovery operations. Reuses the exact Preview → Safety Backup → Server-side Transaction → Encode → Verify → Commit pipeline unchanged; reachable from the existing Desktop guild ownership UI.
- Adds Player Identity Mismatch Detection: two new read-only diagnostic findings (category `"Identity"`) merged into the existing unified diagnostics report — a Steam ID observed under multiple distinct player identities, and a player observed online via REST with no matching save file. Built on v0.6.6.0's Player Registry (Steam ID/UID capture) cross-referenced against the existing guild explorer's save evidence; no new save-decode work. This is the read-only foundation the roadmap explicitly frames as what a future interactive UID-remapping wizard would build on — the wizard itself is not shipped.
- Identity findings are explicitly excluded from the Overall Health rollup that drives the Dashboard badge — they're advisory/analytical, not server-operational problems. Verified live that without this exclusion, the routine and expected "just joined, save not written yet" case would incorrectly flip the badge to Degraded.
- Verified end-to-end against an isolated instance using a real copy of production save data with one player's `.sav` deliberately removed (same technique v0.6.3.0 used to create a genuine test scenario): Preview correctly offered Remove Broken Member only for the dangling reference and correctly rejected it for players with a valid save; Apply staged, encoded, independently re-verified the removal inside the transaction, and committed successfully after one transient Windows file-lock retry; both new Identity findings appeared with correct evidence and were confirmed excluded from the health/warning counts.
- Reconfirmed, not newly introduced: the `Level.sav.json` sidecar staleness gap already documented in v0.6.3.0's architecture doc — the read-side guild explorer doesn't reflect a just-applied mutation until that sidecar is externally regenerated, identical to every prior guild-mutation operation.
- Explicitly deferred, not silently dropped: the surgical byte-level EditPlan engine and everything built on it (Pal editor, container resizing, bulk base-Pal operations, provider-based teleport/summon/kits) — confirmed via research that no Pal/inventory/container struct decoder exists anywhere in the codebase, legacy or new; player progression/inventory administration; the interactive UID remapping wizard itself (detection ships this pass, the guided repair workflow does not).

## v0.6.6.0 — Player Registry, Activity Intelligence & World Explorer 2 (real foundation slice)

- Adds `HeadlessPlayerRegistryService`, a persistent per-player identity/session record: first/last seen, total sessions, cumulative playtime, and real Steam ID/Player UID mapping captured directly from live Palworld REST player data (previously observed on every poll but never persisted anywhere). Follows the exact "sample on the existing status-poll cadence" pattern `HeadlessHistoricalMetricsService` established in v0.6.1.0 — no new background timer added to the app.
- Join/leave transitions are detected and recorded as a bounded, persisted event history (`GET /api/v1/players/registry/events`). Per-tick playtime accrual is capped so a large gap between observations (nobody polling, a MystTiq restart) can never be misattributed as playtime for a player who was online before and after.
- New `GET /api/v1/players/registry` route, surfaced on the Desktop Players page as a per-player summary line (first/last seen, sessions, tracked playtime) alongside the existing notes/warnings panel.
- Player-count/known-player history needed no new work — it's already covered by v0.6.1.0's `HeadlessHistoricalMetricsService`.
- World Explorer 2: adds `AbandonedBaseIds` to the existing guild/base explorer snapshot — a cheap, real derivation from data already computed (any base belonging to a guild already flagged `"Orphaned / Needs Review"`), rather than a new detection pass.
- Verified end-to-end against an isolated instance with a mock Palworld REST endpoint standing in for a real dedicated server: confirmed a real Join is detected and recorded exactly once (no duplicate on a still-online poll), playtime accrues correctly between polls, a Leave is detected and recorded on disconnect, and session count increments correctly on rejoin while preserving cumulative history.
- Explicitly deferred, not silently dropped: the high-performance Canvas world map and its overlays (Pals/bosses/NPCs/fast travel/dungeons/relics/base-radius visualization); activity heatmap, peak analysis, and maintenance-window recommendations; searchable chat/activity history and queued offline moderation (no chat-capture source exists anywhere); multi-source presence reconciliation with confidence scoring; global item search and storage/container inspection.

## v0.6.5.0 — Provider Framework & Configuration Intelligence

- Adds `IPlayerModerationProvider`, the first real provider contract, with two working implementations: `HeadlessPalworldAdminService` (the existing REST kick/ban logic, unchanged, now also registered as the "rest" provider) and a new `RconPlayerModerationProvider` using Palworld's Source RCON `KickPlayer`/`BanPlayer` commands. `PlayerModerationCoordinator` tries REST first (today's pre-v0.6.5.0 default, unchanged for REST-only servers), skipping any provider that reports itself Unavailable/Misconfigured, and falling through to the next on a real execution failure — never a silent no-op.
- Closes a real, common-configuration gap: a server with REST disabled and only RCON enabled previously had no working kick/ban path at all (`"Palworld REST API is disabled in PalWorldSettings.ini."`, full stop). Kick/ban now automatically routes through RCON in that case. Whisper/promote/give-item stay exactly as honestly unsupported as before — neither interface exposes them.
- Found and fixed a real bug during live verification that the fallback design depends on: `HeadlessPalworldAdminService`'s REST HTTP call had no exception handling, so a connection-refused failure (REST enabled per config but not actually reachable) threw an unhandled `HttpRequestException` straight out of `PlayerModerationCoordinator`'s loop — HTTP 500, and RCON was never even attempted. Now caught and returned as a normal `Supported=true/Success=false` result, letting the coordinator fall through as designed.
- Ships Configuration Intelligence's first real slice: cross-setting port-conflict detection (Game/REST/RCON ports, only counting a port whose interface is actually enabled) merged into the existing v0.6.4.0 unified diagnostics report as a new "Configuration" category finding — appears on both the Doctor page and the Dashboard health badge with zero Desktop code changes, since both already render the unified `DiagnosticFinding` list generically.
- Adds `GET /api/v1/players/moderation/providers` (provider health) and shows it on the Players page. Full 7-capability/6-provider-type roadmap surface (presence/world-data/chat/whitelist/teleport/commands; GameData/save/PalDefender/MOD providers), the central configuration schema, and minimal configuration writes are explicitly deferred, not silently dropped.
- Verified end-to-end on Windows against a real isolated instance: confirmed provider health correctly reflects PalWorldSettings.ini state (both enabled → both Healthy; REST disabled → Unavailable, kick/ban routes straight to RCON); confirmed a real network failure on REST now falls through to RCON with an honest combined result instead of a 500; confirmed the port-conflict finding flips to Fail with the correct evidence when two enabled ports collide and back to Pass when reverted; confirmed whisper/unsupported actions are completely unchanged.

## v0.6.4.0 — Troubleshooting & Diagnostics Platform

- Consolidates `HeadlessDoctorService` and `HeadlessEnvironmentChecklistService` into one `HeadlessDiagnosticsService`, without rewriting either (both stay unchanged, already-proven services) — a thin unifying layer maps both into one `DiagnosticFinding` list, de-duplicating the 3 facts both independently checked (SteamCMD/PalServer-executable/BackupRoot existence) into a single merged finding each, verified live: each fact now appears exactly once with correct math (8 passed, 6 warnings, 2 failures on a real test instance, matching hand-verified totals).
- The Dashboard's "OVERALL HEALTH" badge — previously computed independently from raw lifecycle status alone, reflecting neither Doctor nor Environment findings at all — now derives from the same unified report, closing the roadmap's "no health deduction should exist without a corresponding visible Doctor finding" gap. `ServerHealthState` (an enum built in v0.6.0.0, explicitly framed as this exact seam, never consumed by anything until now) is finally real.
- Ships real "Fix Automatically" for what already has a safe mutation path: backup-root creation (new, tiny), and SteamCMD/PalServer install (reuses the existing distribution-update route) — verified live end-to-end (deleted the backup root, confirmed the finding flipped to a fixable Warning, confirmed Fix Automatically actually recreated the directory). Everything else stays honestly gated `BACKEND REQUIRED`, unchanged — no fake fix buttons for capabilities that don't safely exist yet.
- Adds per-check Recheck (re-runs the owning source, returns just the one updated finding).
- Adds local-PC diagnostics — entirely client-side, running even when the server is fully unreachable: a staged DNS → TCP connect → TLS handshake (+ certificate pin verification) → HTTP `/healthz` probe against the selected connection profile, replacing today's single opaque `.NET` exception message with a specific, staged diagnosis (e.g. real `SocketErrorCode` distinguishing connection-refused from timeout from host-unreachable). Plus two always-available local-machine checks (.NET runtime, local disk space). Confirmed via research this is genuinely new — the existing `NetworkDiagnosticsService` only ever checked the *server's* own inbound firewall/listener state.
- Verified end-to-end on Windows against a real isolated instance: confirmed the 3 overlapping facts merge correctly (not duplicated), confirmed Fix Automatically and Recheck both work for real, confirmed a non-fixable finding returns its real "not supported" reason rather than a silent no-op.

## v0.6.3.0 — Windows Service Hardening, Character Migration & Setup/Update Cleanup

- Wires up `WindowsServiceManager` (an `sc.exe` wrapper that existed since v0.4.0.0 but was never called from anywhere) into `service-status`/`service-install`/`service-uninstall`/`service-run`, previously hard-gated to Linux only. Adds a real `Microsoft.Extensions.Hosting.WindowsServices`/`AddWindowsService()` integration so `sc.exe stop` gracefully shuts the process down instead of the Service Control Manager eventually force-killing an unresponsive console app.
- Renames/generalizes `LinuxHeadlessSupervisor` → `HeadlessSupervisor` (it never used anything Linux-specific) and shares it across both platforms — this is what actually makes `RecoveryBackoffSeconds`/`MaximumRecoveryAttempts`/`RecoveryWindowSeconds` real on Windows for the first time; they were previously dead config with no code path consulting them.
- Adds a real SCM-backed `WindowsSystemServiceStatusProvider`, replacing a previously-hardcoded-false status stub for the service-run path.
- Adds character/account migration (e.g. Xbox → Steam): `MystTiq.Core.Migration.PlayerMappingEngine` (ported identity-matching heuristics) + `HeadlessCharacterMigrationService`, following `HeadlessGuildOwnershipService`'s exact Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit pattern, migrating a source player's guild membership/leadership to an already-existing destination player identity within the same live world. Ships real Keep/Archive/Delete source-character disposition; `Reset` is explicitly rejected with a clear message rather than silently no-op'd, since it needs a fresh-character save encoder this build doesn't have. Full level/XP/inventory/Pal/equipment transplant is explicitly deferred — it needs new individual-player-`.sav` decode/encode infrastructure nothing in this codebase (legacy or new) has ever implemented.
- Fixes a real, confirmed Setup/Update Center bug: Server Setup's per-row "SteamCMD"/"Palworld Dedicated Server" action and "Install Missing" button silently called the same full SteamCMD update/reinstall route as Update Center's dedicated button, even when clicked on an already-installed row reading "Verify". Setup now only mutates directly for genuinely-missing (first-run) components; otherwise it navigates to Update Center, which stays the one authoritative place the update mutation happens.
- Two real bugs found and fixed during live verification against a real save (not just static contract checks): the migration preview's `canApply` computation counted an informational identity-match description as a blocking finding, making it permanently false; and the new `CharacterDisposition` enum was initially missing its `JsonStringEnumConverter` attribute, breaking request deserialization.
- Verified end-to-end on Windows against a real isolated copy of a live save: previewed and applied a real guild-membership migration (Melly → a synthetic destination identity), confirmed via safety-backup file-hash diff that `Level.sav` genuinely changed, confirmed the migration journal appears in Transaction Center history (`mode=character-migration`), and confirmed Archive and Reset disposition behavior.

## v0.6.2.0 — Multi-Server Fleet (Runtime Providers deferred)

- Removes the single-server assumption baked into every layer: `HeadlessConfiguration.Server` (one object) becomes `Servers` (a list of `HeadlessServerProfileConfiguration`, schema v3), with a `MigrateV2` path that wraps an existing single-server config into one `"default"` entry — zero behavior change for an existing deployment.
- `LocalManagementApiHost.Create` now constructs one full, already-existing service graph (`ServerProfileHost`) per configured server profile instead of one graph for the whole process — no internal service rewrites; isolation comes entirely from each profile getting its own `IServerPathProfile`/`IServerLifecycleService`. Every route is mapped twice: once at its classic unprefixed path (resolving to the `"default"` profile only, so every pre-fleet client keeps working unchanged) and once under `/api/v1/servers/{profileId}/...` for every profile.
- `OperationCoordinator` becomes a fleet-level singleton with resource locks partitioned by `(ServerProfileId, resourceKey)` — a `"world-mutation"` lock on one server no longer blocks another. `HeadlessRbacService`/`HeadlessAuthAbuseGuardService` stay fleet-wide too (one principal/auth surface for the whole app); `MystTiqPrincipal` gains an optional `ScopedServerProfileId` for per-server permission grants, enforced by `RequireRole`.
- Found and fixed a real cross-talk bug from the previous session: `LocalManagementBootstrapper.EnsureAvailableAsync` treated any process answering `/healthz` as reusable with no identity check, so a "local" connection profile could silently attach to the wrong already-running MystTiq instance. `/healthz` now reports `serverProfileIds`; the bootstrapper verifies the expected profile is actually present before reusing an instance.
- Adds `GET/POST /api/v1/servers` (list/add) and `DELETE /api/v1/servers/{id}` (remove) for fleet membership, plus staggered bulk actions `POST /api/v1/fleet/{backup,doctor,update}-all` (configurable `FleetStaggerSeconds`, default 5s, so N servers don't fire the same disk/network-heavy action simultaneously).
- Adds a runtime-provider seam (`ServerRuntimeKind`) with two real implementations (Windows/Linux native, wrapping the existing lifecycle services unchanged) — Docker and Wine-on-Linux are explicitly deferred to a follow-up revision, not silently dropped, since they need real Docker/Wine environments to verify against.
- Desktop: new Fleet page listing every configured server profile with independent status, plus Backup All / Doctor All / Update All buttons. Existing single-server pages are unchanged in this pass (per-page server selection is explicitly deferred alongside Docker/Wine).
- Two real bugs found and fixed during manual verification against a real two-profile isolated instance: profile-scoped routes were double-prefixed (`/api/v1/servers/{id}/api/v1/...`) due to a route-group misuse, and the config's new `ServerRuntimeKind` enum lacked its `JsonStringEnumConverter` attribute, both caught by actually exercising the fleet endpoints rather than relying on contract-presence regexes alone.
- Verified on Windows against a real two-server isolated instance: independent config/backups/lifecycle status per profile confirmed via direct API calls, `Backup All` created isolated, correctly staggered backups on both servers, concurrent `Server Start` on both profiles failed independently without cross-blocking (proving the coordinator's per-profile lock partitioning), and a real legacy v2 config migrated cleanly to v3.

## v0.6.1.0 — Advanced Administration, Automation & Analytics Platform

- Adds the app's first and only background loop: a real Trigger→Condition→Action automation engine (`MystTiq.Core/Automation/`, `HeadlessAutomationService`, a `PeriodicTimer`-driven scheduler) with `DailyTime`/`Interval` triggers and `CreateBackup`/`StartServer`/`StopServer`/`RestartServer`/`SendNotification`/`SendRconCommand` actions, including RCON warning-countdown broadcasts before Stop/Restart. Rules and run history persist under `ManagerRuntimeRoot/automation/`.
- Found and fixed a real gap: the manual `/server/start|stop|restart` routes previously used only a local semaphore, never the `OperationCoordinator` — a restart could race an in-flight World Transaction/Guild/Base Apply. Retrofitted onto the coordinator with new resource keys `"lifecycle"` (Stop) and `"lifecycle"+"world-mutation"` (Start/Restart, so they correctly refuse to run mid-mutation).
- Adds formal backup classes (`Manual`/`Scheduled`/`Emergency`/`Safety`) via a classification manifest; unclassified/legacy backups default to `Manual`. Retention pruning now defaults to `Scheduled`-only, protecting Manual/Emergency/Safety backups from routine cleanup by default.
- Adds minimal, fully backward-compatible RBAC: `MystTiqRole`/`MystTiqPrincipal` in `MystTiq.Core/Security/`, enforced via a new `RequireRole` Minimal API endpoint filter. The existing single shared bearer token keeps resolving to a full-access `LegacyOwner` principal unchanged — zero-config deployments are unaffected. Includes an in-memory auth-abuse guard (lockout after repeated failures).
- Adds an Alert Center (`HeadlessAlertCenterService`) with CPU/memory/disk-space threshold alerts and a real linear disk-space-exhaustion projection, reusing the existing Notification Center (`HeadlessNotificationService.Create`) as its sink rather than a parallel pipeline. Also adds honest gap/restart markers to the historical metrics chart so an offline period no longer draws as a straight line.
- Adds a real (not decorative) slice of notification routing: a `NotificationChannel` model with an actually-implemented `Webhook` dispatch path (retry-once) and typed `Discord`/`Email` stubs that log "not implemented" rather than silently dropping, plus reusable notification templates.
- Adds a `PlayerFlag` (Watchlisted/Banned/Trusted) annotation to player metadata — data/annotation only in this pass, not auto-enforced.
- Desktop: three new pages (Automation, Alert Center, Security) under System, following the existing page/DTO/API-client conventions; RBAC's `WhoAmI` result disables buttons the current principal's role can't use.
- Deliberately scoped down from the milestone's full roadmap bullet list: event-based automation triggers, real Discord/Email dispatch, fine-grained per-capability RBAC, and the typed RCON command catalog (owned by a later milestone) are explicitly deferred, not silently dropped — see `docs/architecture/v0.6.1.0-advanced-administration-automation.md`.
- Verified end-to-end on Windows via a real isolated authenticated instance: legacy-token backward compatibility, automation RunNow and the background scheduler actually firing a rule on its own, RBAC role-tier enforcement (403 on an under-privileged route), and the new lifecycle/world-mutation cross-lock all confirmed working live, not just via logic-test regexes.

## v0.6.0.0 — Architecture Baseline & Operation Platform

- Adds a real Operation/Provider/Health/Transaction contract set in `MystTiq.Core/Operations/`: `OperationId`/`OperationRecord`/`OperationPhase`/`OperationTransactionState`, `ServerProfileId`, `ServerHealthState`/`ServerHealthSnapshot`, `ICapabilityProvider`, and `IOperationCoordinator`/`OperationCoordinator` — first milestone of the `v0.6.x.0` family, laying the foundation the rest of it builds on.
- `OperationCoordinator` rejects any operation whose resource keys overlap one already in flight (reject-if-conflicting, not queue-and-wait), naming the blocking operation in the message, and persists one JSON journal per operation.
- Migrates the three existing destructive-mutation services — World Transactions, Guild Ownership, Base Ownership/Recovery — onto the coordinator by "wrapping, not replacing" their proven per-feature journals, layering cross-feature resource-locking on top rather than ripping out already-shipped, verified logic. All four share resource key `world-mutation` (they all touch the same `Level.sav`), so the coordinator now correctly rejects any of them running concurrently with another — a real safety property that did not exist before.
- Adds `GET /api/v1/operations` and `GET /api/v1/operations/{id}`; the Desktop World Transactions page gains a second "Operation Platform" card listing Kind/Source/Phase/TransactionState across all three migrated features with its own Refresh command.
- Scoped-down pass by design: priority-ordered queueing, dependency-graph blocking beyond a single resource key, restart-safe reclassification of interrupted operations, SteamCMD phase parsing, and a full log-tail UI are explicitly deferred, not silently dropped — see `docs/architecture/v0.6.0.0-operation-platform.md`.
- Verified end-to-end on Windows: a real Base Recovery Apply and a concurrent World Transaction Apply were fired simultaneously against an isolated copy of a real guild/base-populated save; the loser was rejected with the coordinator's own message naming the held resource key, proving the new cross-feature lock rather than any pre-existing per-service guard. The new `/api/v1/operations` routes were also confirmed working end-to-end against the live Linux VM.

## v0.5.5.0 — Desktop Shell UI/UX Consolidation

- Replaces the sidebar `ToggleButton.nav` template with a bare `ContentPresenter`, fixing a bug where FluentTheme's default checked/pointerover background bled a solid `#0078D4` default-blue frame around the custom rounded glass pill (a plain `Background` setter cannot override a template's own internal state-specific paint).
- Nav items are taller (58px) and the sidebar is wider (260px); the pill now stretches to fill its full row height instead of centering at its natural text height and leaving dead space.
- Hovering an already-selected nav item now visibly brightens instead of showing zero change (checked background was silently winning over hover with equal cascade specificity).
- Selected/hover glow switched from `BoxShadow` to `DropShadowEffect`, since `BoxShadow` was painting a hard rectangular corner past the rounded `CornerRadius` silhouette.
- Dashboard SERVER card is now a clean three-state traffic light (red/amber/green) instead of a fourth neutral-grey state that made a stopped server look calm rather than stopped; OVERALL HEALTH label color now follows its card's state instead of staying hardcoded green.
- Every page's duplicated title banner (18 of them) removed from the page body now that the title/subtitle renders once in a shared card in the ribbon row, which also now carries Auto refresh/Connected — frees vertical space on every page.
- Desktop-only change; no backend/API/platform-specific code touched.

## v0.5.4.0 — Base Recovery

- Adds Base Recovery ("Recover Base"), porting the legacy app's `DeleteBaseAndOwnedObjects` operation onto `HeadlessBaseOwnershipService`: removes the base's `base_ids` entry from its owning guild and deletes every `base_camp_id_belong_to`-tagged structure/container/worker record outright, rather than reassigning them like Transfer Ownership.
- Uses the same Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI implementation as Base Ownership Transfer. Preview decodes the save up front and reports the exact record count that will be permanently removed before Apply.
- Removal only ever discards whole array elements, never a keyed object property — some record shapes (`MapObjectSaveData`) carry the ownership tag nested under `Model.value.RawData.value`, several levels below the record's own array element; deleting that inner key directly corrupted the structure and crashed the save encoder during initial testing, fixed by scanning each array element's full subtree for the tag before removing the whole element.
- The Bases page's "Recover Base — BACKEND REQUIRED" stub is replaced with a real Preview/Apply card carrying an explicit destructive-action confirmation checkbox.
- Verified end-to-end on both Windows and Linux (Ubuntu 24.04.4 LTS) against a real guild/base-populated save: 132/132 tagged records and the `base_ids` entry correctly removed, confirmed via independent re-decode with zero remaining references, identical outcome on both platforms.

## v0.5.3.0 — Base Ownership Transfer

- Adds Base Ownership Transfer, porting the Guild Ownership pattern onto Base: moves a base's `base_ids` entry between guild records and retags every structure/container/worker object placed at that base (132 tagged records confirmed for one real base) to the new owning guild.
- Uses the same Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI implementation and journals into the same store as Guild Ownership and World Transactions.
- Scoped to Transfer Ownership only; Base Recovery (legacy `DeleteBaseAndOwnedObjects`) is not ported and remains an honest disabled stub.
- Verified end-to-end on both Windows and Linux (Ubuntu 24.04.4 LTS) against a real guild/base-populated save: 113/113 owning-guild tags correctly retagged and both guilds' `base_ids` arrays confirmed correct via independent re-decode, identical outcome on both platforms.

## v0.5.2.0 — Guild Ownership Platform

- Adds real Claim Orphaned Guild, Transfer Leadership, and Add Player to Guild operations, replacing three disabled "BACKEND REQUIRED" stub buttons on the Guilds page.
- All three share one Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI implementation and journal into the same store as World Transactions.
- Adds a server-side save codec that detects each save's real container format (plain PlZ vs. PlM/Oodle) per file instead of assuming one converter.
- Fixes `PalworldSettingsConfigurationService.Load()` crashing the entire `/api/v1/status/poll` endpoint when `PalWorldSettings.ini` exists but hasn't been fully written yet.
- Fixes two version-string literals that had silently drifted from the real build version (headless `--help`, Desktop `Version` property); both now derive from the assembly.
- Fixes a dead "Open Server Log" button in Diagnostics Center.
- Base ownership/Palbox repair remains BACKEND REQUIRED — the pattern now exists and is proven, it just hasn't been ported onto Base's data shape yet.
- Verified end-to-end against a real Linux deployment (Ubuntu 24.04.4 LTS), not just compiled for Linux.

## v0.5.1.5 — First-Run Setup Wiring and Update Center Correction

- Restores the Server Setup first-run defaults form for identity, optional passwords, player capacity, and game/REST ports.
- Adds a confirmation-gated, authenticated headless creation route that validates inputs, refuses overwrite, preserves official REST/RCON enablement defaults, and omits credentials from audit details.
- Restores the Update Center platform, SteamCMD, and PalServer summary cards to the Update Center page.
- Preserves the v0.5.1.4 server-tab, Configuration, Backup Center, Console, Workspace, containment, polling, security, and Windows/Linux parity corrections.

## v0.5.1.4 — Legacy Page Detail and Server Tab Correction

- Corrects the selected server tab’s contained glass styling and places the add-server control directly beside the profile tabs.
- Expands Server Setup with component, ready, and attention summaries above the authoritative environment checklist.
- Restores Configuration server identity, separate Simple slider/selection controls, and the Advanced default/active settings table while removing lifecycle/status duplication.
- Restores all primary Backup Center commands to the top row with archive verification summaries.
- Removes CPU, RAM, thread, and recent-metric cards from Console; console rows remain green and timestamps are normalized to the first field.
- Restores Workspace deployment mode, health, server discovery, and executable/application/server/download/export/log locations with profile-safe actions.
- Fixes the desktop publisher so the scoped artifact process is closed before output files are overwritten.

## v0.5.1.3 — Navigation and UE4SS Parity Correction

- Moves Notifications into System and keeps the notification bell routed to the same persistent API-backed page.
- Adds the top add-server control and opens a blank, unsaved connection profile in Settings until explicitly saved.
- Replaces redundant UE4SS Installed MODs content with installed runtime version, health/evidence, and fork selection.
- Adds server-side UE4SS version detection across the existing API DTO boundary without desktop filesystem access.
- Standardizes single-line TextBox and ComboBox dimensions against the legacy GUI and records the ordered v0.2.16.4 screen comparison.
- Preserves the headless-first wiring, single aggregate poll, secured remote/LAN boundary, card containment and shared Windows/Linux Avalonia implementation.

## v0.5.1.2 — v0.5 Shell Functional Integration

- Promoted the approved v0.5.0.18 prototype visual system from reference-only code into the real desktop shell.
- Replaced dummy prototype server tabs with the authoritative connection-profile collection.
- Connected ribbon lifecycle, backup, console and Doctor actions to the existing ViewModel/API/headless paths.
- Retained every restored functional page inside the glass workspace, with Home, World and System navigation matching the approved information architecture.
- Added custom title-bar behavior while preserving tray-aware close semantics.
- Added versioned logic/runtime/Linux gates and parity evidence for the corrected shell foundation.

## v0.5.1.1 — Home Navigation Refinement

- Creates a Home section containing Dashboard and Notifications.
- Removes Notifications from System; Settings and Activity & Audit remain there.
- Restyles the selected Home destination with the compact blue glass treatment from the approved prototype reference.
- Keeps the header notification bell routed to the persistent Notifications page.
- Preserves the v0.5.1.0 functional wiring, single status poll, secured remote/LAN boundary, card containment and Windows/Linux parity.

## v0.5.1.0 — Functional Visual Shell Integration

- Starts the post-restoration integration route from the clean v0.4.18.2 functional baseline.
- Uses the supplied compact MystTiq wordmark beside the Palworld server icon with the version directly beneath it.
- Keeps World navigation to Inspector, Players, Bases and Guilds; the transactional recovery center remains available inside Inspector.
- Keeps Settings, Activity & Audit and Notifications under System and adds direct header bell/gear routing.
- Adds a glass-highlight navigation hover while preserving global card clipping and centered metallic buttons.
- Preserves the headless-first API boundary, single aggregate poll, remote/LAN security, and Windows/Linux Avalonia parity.

## v0.4.18.2 — Closeout Gate Contract Correction

- Corrects inherited logic assertions that still expected v0.4.17.4 metadata and the former Workspace button label.
- Carries the complete v0.4.18.1 Workspace, Diagnostics, Settings and 287-event coverage implementation unchanged.

## v0.4.18.1 — Workspace + Diagnostics + Settings Closeout

- Corrects the initial v0.4.18.0 path-validator compile mismatch.
- Restores Workspace refresh, syntax validation, local browse/open and rollback-backed API save; local shell operations are disabled for remote profiles.
- Adds diagnostic report export and a local redacted ZIP support package while retaining network/firewall/listener routes.
- Retains connection profiles, LAN discovery, process-memory-only bearer tokens, TLS certificate pins and explicit local bootstrap.
- Classifies all 287 legacy GUI event rows with current mapping and evidence.
- Closes the planned restoration sequence and stops before v0.5.0.0.

## v0.4.9.1 — RCON Boundary Gate Correction

- Corrects the RCON ownership assertion so descriptive GUI text containing the word “socket” is not mistaken for socket implementation code.
- Continues to require all `TcpClient`/socket transport construction to remain in the shared headless Core service.
- Preserves the v0.4.9.0 Console + RCON implementation, single status poll, global card containment, remote/LAN security, and Windows/Linux Avalonia parity unchanged.

## v0.4.9.0 — Console + RCON Parity & Global Card Containment

- Accepts v0.4.8.0 as the source baseline and corrects its stale Testing UX and Palworld Configuration logic assertions.
- Restores legacy console severity/category/search filters, Hide routine REST, Refresh, Pause/Resume, Clear View and local Export.
- Adds Core-owned Source RCON support with headless `/api/v1/rcon/status`, `/api/v1/rcon/doctor`, and `/api/v1/rcon/command` routes; AdminPassword never crosses the API boundary.
- Adds legacy RCON Doctor/Connect/Disconnect/preset/send/history UI while clearly labelling RCON as deprecated/compatibility functionality.
- Adds a global card-containment style: card/status-card clipping, card text wrapping, and nested layout clipping to prevent content overflow.
- Adds v0.4.9.0 logic/runtime coverage for console filters, RCON wiring/security, global containment, version consistency, and prior regression contracts.

## v0.4.8.0 — Configuration Parity

- Promotes v0.4.7.1 as the accepted source baseline and fixes its carried-forward version/documentation metadata mismatches.
- Restores Simple/Advanced Palworld configuration views, search/category filtering, historical QoL presets, dirty-state and validation.
- Adds cross-platform local JSON import/export through Avalonia StorageProvider; imports remain local until Save Changes routes through the management API.
- Preserves unknown/custom OptionSettings and rollback-backed server-side saves.
- Adds v0.4.8.0 logic coverage for configuration parity, file-boundary safety, presets, filtering, validation, and version consistency.

## v0.4.8.0 — Dashboard + Server Setup Parity Closeout

- Correct the Testing UX logic assertion so README validation targets `Test-v0.4.8.0-Logic.ps1` rather than the prior v0.4.6.8 harness.
- Promote v0.4.6.8 as the official baseline after its complete gate passed.
- Restore legacy-style Dashboard OPEN/BACKUP/DOCTOR card workflows and the operational health strip without adding a second poller.
- Make RCON/Cleanup capability gaps visible as BACKEND REQUIRED instead of fake controls.
- Restore Server Setup banner/environment-health/checklist/operations/monitor hierarchy.
- Extend `/api/v1/server/environment` rows with `actionSupported` and `unavailableReason`; unsupported mutations are disabled and guarded in the ViewModel.
- Add v0.4.8.0 logic/runtime/Linux acceptance coverage for the reconstruction contract.

## v0.4.6.8 — Dashboard Meter Containment Fix

- Constrained the compact CPU/Memory utilization meters to the dashboard card using clipping and bounded meter layout.
- Clipped the custom Resource History chart to its own drawing bounds and inset plotted strokes so antialiasing cannot paint outside the card.
- Added logic-test coverage for dashboard meter/chart containment.

## v0.4.6.7 — Tray Gate Contract Fix & GUI Restoration Roadmap

- Corrected the stale Tray Lifecycle logic assertion to recognize the implemented Safe Exit, Force Exit and Exit GUI Only tray controls.
- Packaged the supplied GUI Reconstruction Guide, 287-event v0.2.16.4 event inventory and v0.2.16.4-to-current functional crosswalk as reconstruction references.
- Added `docs/GUI_RESTORATION_ROADMAP_v0.4.7.0_PLUS.md`, defining exact page/section restoration builds, current ViewModel/API mappings, BACKEND REQUIRED boundaries and per-version logic tests.
- Made the dangerous world/save mutation contract explicit: Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI.
- Preserved the v0.4.6.6 runtime functionality; this build is a release-gate/reference correction, not a backend architecture rewrite.

## v0.4.6.6 — Resource History Graph & Runtime Metrics Parity

- Restored the v0.2.16.4-style Resource History card with a live CPU/RAM line graph, 1 Hour / 6 Hours / 24 Hours / 7 Days / 30 Days range selector, average/peak summaries, sample count and live indicator.
- Added persisted headless historical telemetry under the manager runtime root with 30-day retention and throttled recording from the existing aggregate status poll; no second periodic poller was introduced.
- Added `/api/v1/history` for on-demand range changes while current live samples continue to arrive through the single `/api/v1/status/poll` path.
- Changed runtime metrics sampling to aggregate all managed PalServer processes so CPU, memory and thread totals do not go idle when the bootstrap/wrapper PID differs from the Shipping process.
- Restored compact CPU and RAM meters in the top dashboard summary and kept the full dual-series history graph in Resource History.
- Corrected the v0.4.6.4 Page Parity assertion so the intentionally restored `SERVER ENVIRONMENT` heading satisfies Server Setup parity.
- Added Windows runtime-smoke coverage for the historical-metrics endpoint and logic coverage for graph rendering, range selection, persistence and multi-process metrics aggregation.

## v0.4.6.3 — Deployment Bootstrap & Dashboard Telemetry Parity

- Added safe latest-FullSource deployment from Downloads with clean/stop, staged validation, target replacement, script unblocking and post-install validation.
- Replaced dashboard CPU bar with a live history graph sourced from the existing aggregate status poll.
- Added active-world nickname + full copyable World ID and restored authoritative World Pulse day/time, save-age and backup-age presentation.
- Combined redirected PalServer process output with Pal.log and restored green in-app console presentation.
- Restored dashboard server name/description, removed duplicate Dashboard heading, and repaired the persistent sidebar status layout.
- Expanded aggregate `/api/v1/status/poll` with dashboard world, backup and Palworld settings support data without adding another client polling loop.


## v0.4.6.2 — PalServer Window Suppression & Live Dashboard Parity

- Reapplies the proven v0.2 Windows post-launch window policy with Win32 `ShowWindow(SW_HIDE)` during PalServer startup so wrapper/child consoles that allocate after `Process.Start` are hidden while stdout/stderr remain redirected into MystTiq.
- Rebuilds the Avalonia Dashboard toward v0.2.16.4 information density with live health, world pulse, session/player state, resource history, live activity, console tail, online players and compact operational summaries.
- Preserves the single aggregate status polling path; dashboard animation/data surfaces consume the same coherent sample rather than creating extra pollers.
- Adds regression coverage for post-launch child-window hiding and dense live-dashboard parity.

## v0.4.6.1 — Hidden PalServer Console & In-App Output Redirection

- Promotes the successful v0.4.6.0 management-session/server-start behavior into the next tracked fix build.
- Windows PalServer launch no longer uses Unreal `-log`, which explicitly opens a separate log window.
- Windows default launch now uses `-stdout -FullStdOutLogOutput -logformat=text` with `UseShellExecute=false`, redirected stdout/stderr, and `CreateNoWindow=true`.
- MystTiq prioritizes `MystTiq-PalServer-Console.log` as the Live Console source, falling back to `Pal.log` only when redirected capture is unavailable.
- Adds regression coverage for hidden-window launch and Live Console redirection.

- Promoted v0.4.5.1 as the official baseline after its compile/logic/runtime gate passed.
- Versioned `/healthz` with API contract, backend version, platform, authentication and TLS metadata.
- Changed desktop connection establishment to use health verification followed by the single aggregate status poll instead of separate status/service calls.
- Added `--desktop-sidecar`, which forces a GUI-owned sidecar to loopback-only unauthenticated/non-TLS mode without weakening remote/LAN security rules.
- Added compatibility-aware loopback bootstrap: an occupied or incompatible configured endpoint causes the packaged sidecar to launch on a private free loopback port.
- Improved lifecycle connection failure text to report the exact endpoint and retained backend detail.
- Restored the established v0.2 default PalServer launch arguments and explicit text logging flags when no custom headless configuration exists.
- Carried the v0.2.16.4 GUI parity audit forward as `docs/GUI_PARITY_v0.4.6.1.md`.

# Changelog

## v0.4.11.0 — Guilds + Bases Parity

- Restored read-only guild and base directory/detail workflows with search/status filters, CSV export, ID copy, and guild-leader player navigation.
- Carried authoritative decoded base IDs through the headless explorer contract without adding GUI filesystem access.
- Kept all guild/base repairs and recovery visibly unavailable until the complete transactional safety backend exists.

## v0.4.10.0 — Players Parity

- Unified live REST and saved-player evidence into a server-owned stable-ID player directory with search, view/admin filters, details, save discovery, and CSV export.
- Added headless-persisted player notes and warnings with bounded input, atomic writes, and privacy-conscious audit entries.
- Made Kick/Ban availability follow online state and kept unsupported player operations visibly disabled.
- Centered global button content, added the metallic gradient interaction treatment, and guarded build relaunch by exact artifact-root process ownership.
- Preserved single aggregate periodic polling, remote/LAN security, card containment, and Windows/Linux Avalonia/headless parity.

## v0.4.5.1 — runtime parity continuation
- Fixed Avalonia desktop compilation for the Palworld configuration list by replacing unsupported `ListBox.HorizontalContentAlignment` with supported `ListBoxItem`/template stretching; added regression coverage.

- Restored a full active Palworld `OptionSettings` configuration editor in the Avalonia Configuration page through a new headless `/api/v1/palworld/config` contract. Saves create timestamped rollback copies.
- Server startup readiness now follows the configured `PublicPort` from `PalWorldSettings.ini` instead of assuming UDP 8211.
- Removed generic/self-evident tooltips while preserving explanatory, safety, capability, path and destructive-action help.
- Added `docs/GUI_PARITY_v0.4.5.1.md`, a page-by-page comparison against the v0.2.16.4 WPF GUI; partial and not-yet-migrated functions are explicitly tracked instead of being represented as complete.

## v0.4.5.1 — Tray Lifecycle, Page Parity & Server Start Reliability (Release Candidate)

### Runtime acceptance continuation
- Fixed Avalonia AXAML property-element syntax introduced by the tooltip pass; tooltips remain on owning/interactive controls and templates/context-menu property elements no longer carry invalid attributes.
- Added an Avalonia XAML safety regression check to block the malformed property-element attribute pattern from returning.
- Added hover information tooltips across navigation, buttons and selection controls.
- Added right-click live-player administration: native Palworld REST Kick/Ban plus capability-gated Whisper/Promote/Give Item entries.
- Restored persistent MystTiq activity/audit logging and a dedicated Activity & Audit view.
- Restored Windows PalServer stdout/stderr capture to `MystTiq-PalServer-Console.log` when MystTiq launches the server.
- Restricted world discovery to canonical SaveGames directories so backup copies of `Level.sav` are not detected as live worlds.
- World Inspector now preserves the full World ID and supplies a compact nickname for readability.
- Corrected stale v0.4.5.0 version assertions/manifest/banner references and archived-test validation noise.


- Added an Avalonia system tray using the established MystTiq icon and NativeMenu. Closing the main window hides it to the tray; it no longer leaves the local sidecar running with no user-facing control surface.
- Added tray actions to show the GUI and route Start/Restart/Stop through the existing ViewModel/API lifecycle commands.
- Added explicit `Stop Local Management Backend & Exit` and `Exit GUI (keep backend running)` choices. The stop action is guarded to the exact sidecar PID/executable started by the current GUI session and never targets PalServer or a separately installed MystTiq service.
- Decoupled management-API connectivity from the human-readable connection/lifecycle status so a failed Start operation does not disable all future lifecycle retries.
- Start Server now attempts local backend recovery when needed, verifies the server distribution/executable before mutation, and surfaces a visible lifecycle result instead of failing silently.
- Split previously shared generic page presentation into page-specific Server Setup, Update Center, Base Manager, Guild Administration, Live Console, Activity & Audit, MOD Dashboard, MOD Library, and UE4SS Runtime views while retaining existing backend data contracts.
- Hardened the Windows runtime smoke test with an isolated missing-server root and a safe `/api/v1/server/start` request that must return HTTP 424; it can never launch the user's real PalServer.
- Added v0.4.5.1 logic/runtime/Linux acceptance coverage for tray lifetime, sidecar ownership, lifecycle retryability, start preflight, visible lifecycle results, and page-specific presentation.

## v0.4.5.0 — GUI Branding & Navigation Polish (Release Candidate)

- Promoted v0.4.4.3 as the official baseline after successful build, logic and runtime acceptance.
- Replaced the temporary Avalonia sidebar `M` placeholder with the established MystTiq Palworld Server Manager logo asset.
- Applied the established MystTiq icon to the Avalonia window/application packaging.
- Added a modest standardized indent to expanded navigation child items while preserving the shared 44-pixel navigation row height.
- Added v0.4.5.0 logic/runtime/Linux acceptance coverage and branding/navigation regression checks.
- Preserved single aggregate status polling, local sidecar auto-connect, LAN/remote authentication/TLS, and Windows/Linux platform behavior.

## v0.4.4.3 — Local Sidecar Auto-Connect & Runtime Acceptance Fix (Promoted Baseline)

- Fixed the acceptance-blocking Windows architecture gap where `api-run` rejected Windows, leaving the Avalonia GUI able to discover PalServer files but unable to perform management operations.
- Added Windows Core lifecycle/session inspection and Windows management API composition.
- Added a self-contained headless sidecar to Windows/Linux Avalonia desktop publishes and local API bootstrap behavior when no persistent API is reachable.
- Standardized sidebar top-level and expanded child navigation rows to the same 44-pixel sizing.
- Added a Windows runtime-smoke gate for health, aggregate status, configuration and distribution endpoints.
- Added platform-aware Windows/Linux headless configuration defaults and path validation.
- Local loopback sidecars now auto-connect without remote credentials when `/healthz` explicitly reports authentication disabled; LAN/remote services retain bearer/TLS requirements.
- Corrected the desktop-sidecar packaging assertion to validate the actual publish contract instead of a brittle regex.
- Corrected stale v0.4.4.2 expectations accidentally carried into the v0.4.4.3 logic harness for version, README, roadmap and test-command checks.
- Hardened `Build.ps1 Clean` so auto-launched development GUI/sidecar processes under `artifacts` are stopped before deletion; Clean now fails loudly if generated artifacts remain instead of reporting a false success.
- Promoted as the official baseline after the complete release gate and runtime acceptance passed.


## v0.4.4.1 — Server / Configuration / Console / Workspace Integration (Release Candidate)


- Fixed release packaging so `scripts/Test-v0.4.4.1-Logic.ps1` is present in both Changed Files and Full Source packages.
- Added bounded cross-platform LAN discovery for MystTiq `/healthz` endpoints instead of assuming only `127.0.0.1`.
- Preserved secure remote management: discovery may identify a service, while authenticated API operations still use normal TLS validation/certificate pinning and bearer-token rules.
- Standardized Avalonia sidebar navigation rows at 36 px with compact indentation/font sizing so expanded items fit consistently across Windows and Linux.
- Added logic contracts for dynamic version wiring, package/test presence, LAN discovery behavior/security, and GUI discovery composition.
- Standardized the documented full test sequence: unblock scripts, Clean, Validate, then the current version logic test with `-RunBuild -ExportJson`.

- Added one aggregate `/api/v1/status/poll` endpoint so periodic GUI status sampling does not fan out across multiple HTTP requests.
- Added a cross-platform bottom status bar with last-sample time and an indeterminate busy animation for active work.
- Promoted Workspace from placeholder navigation to a managed, API-backed view of server, SteamCMD, backup, and runtime paths.
- Preserved lifecycle ownership in the headless/Core path; Avalonia remains an API consumer on Windows and Linux.
- Added `Test-v0.4.4.1-Logic.ps1` covering View → ViewModel → API → endpoint → Core/platform wiring, polling behavior, workspace behavior, release-gate integration, and platform preservation.
- Updated the release workflow so logic tests and Windows/Linux headless + Avalonia builds execute before packaging/checksums.

- Development moved to v0.4.4.1 after promotion of v0.4.3.1.
- Target architecture remains headless-first: View -> ViewModel -> API/service -> endpoint -> Core -> platform implementation.
- GUI close/background behavior must remain independent of explicit server/service shutdown.
- Windows and Linux desktop behavior must share Avalonia/Core contracts wherever platform-specific implementations are not required.
- Versioning now follows MAJOR.MINOR.REVISION.FIX; new revisions start at fix 0.

## v0.4.3.1 — Promoted Baseline / Avalonia StringFormat Parser Hotfix

- Promoted v0.4.3.1 as the official baseline after the complete release gate passed.
- Normalized the former `v0.4.0.3 FIX1` label to the new four-part version convention.
- The final component is now the fix number; separate `FIX1` suffixes are no longer used for new releases.
- Preserved the Dashboard/local backend integration and Avalonia StringFormat correction.

## v0.4.0.2 FIX2 — Console Composition & Warning-Free Build Hotfix

- Corrected Console composition verification to follow its real Players/Logs/Metrics API wiring.
- Fixed CS8601 in diagnostics restart handling.
- Avalonia desktop compiler warnings now block promotion.
- Product version remains v0.4.0.2.


## v0.4.0.2 FIX1 — Logic Harness & Release Packaging Hotfix

- Fixed the v0.4.0.2 PowerShell logic-harness parser error.
- Added required changed-files apply instructions and packaging preflight checks.
- Product/runtime behavior remains v0.4.0.2.


## v0.4.0.2 — Avalonia Navigation & Theme Foundation

- Rebuilt the Avalonia left navigation using the original MystTiq SERVER/WORLD/MODS/TOOLS/SYSTEM hierarchy.
- Added persistent sidebar service/server status and version identity.
- Replaced the purple prototype accent with centralized dark navy/blue theme resources.
- Added shared primary/success/warning/danger/navigation/card/status styles.
- Routed navigation destinations to existing backend-backed screens where available and explicit placeholders where backend integration is deferred.
- Added current-version Linux acceptance/production-readiness packaging scripts.
- Integrated version-specific logic/regression tests into the root Build.ps1 release gate.
- Re-sequenced v0.4.0.3–v0.4.0.9 for GUI functional parity before deferred save/freeze and advanced MOD diagnostic work.

## v0.4.0.1 — Network Diagnostics & Connectivity Recovery

- Added independent Runtime Health and Network Health.
- Added effective Palworld UDP game-port resolution with explicit `-port=` support and safe 8211 fallback.
- Added PID-aware UDP/TCP endpoint inspection and wrong-process/wrong-port detection.
- Treats `0.0.0.0` as a healthy all-IPv4 binding and resolves a human-readable LAN endpoint.
- Added Windows Firewall rule inspection plus idempotent MystTiq-owned rule repair.
- Added startup grace for delayed PalServer socket binding.
- Added optional RCON and REST checks when enabled in PalWorldSettings.ini.
- Added controlled diagnostic restart with post-restart network verification.
- Added management API endpoints and shared Avalonia Diagnostics UI.
- Added redacted support report/copy workflow and structured diagnostic start/end evidence.
- Captured projected v0.4.0.2–v0.4.0.5 save/freeze, auto-save/save-health, incident-bundle and MOD isolation/functional-test work.


## v0.4.0.0 — Windows Persistent Service Foundation

- Added Windows SCM service manager contracts and implementation in shared core.
- Added automatic-start/recovery configuration for the MystTiq Windows Service.
- Added win-x64 headless-host publish build action.
- Preserved Linux systemd behavior and existing WPF runtime while Windows service lifecycle integration proceeds.


## v0.3.1.9 FIX1 — Release Metadata & Deployment Script Cleanup

- Added the required v0.3.1.9 Changed Files apply document.
- Synchronized Linux desktop deployment helper version references to v0.3.1.9.
- No runtime behavior changed.


## v0.3.1.9 — MOD & UE4SS Management

- Added shared MOD/UE4SS API and Avalonia management page.
- Added UE4SS active-root and runtime-evidence parity.
- Added safe stopped-server PAK and mods.txt state changes.
- Preserved neutral Disabled / Active-Unverified health semantics.
- Removed superseded v0.3.1.8 harness warning source.


## v0.3.1.8 — Player & Guild Explorer

- Added validated active-world player-save identity discovery.
- Added authenticated Player & Guild Explorer API.
- Added authoritative decoded GroupSaveDataMap guild semantics.
- Added optional live REST enrichment for online player evidence.
- Added guild leadership/member/base-reference and orphan-review evidence.
- Added shared Avalonia Players & Guilds page.
- Preserved read-only behavior and explicit semantic-unavailable state.


## v0.3.1.7 FIX1 — Validation Warning Cleanup

- Removed the prior-patch literal from the active v0.3.1.7 logic harness.
- Replaced it with a positive assertion that `DoctorReport` receives the assembly-derived `reportVersion`.
- No runtime behavior changed.


## v0.3.1.7 — World Explorer Foundation

- Added read-only authenticated World Explorer API.
- Added server-authoritative world discovery from SaveRoot/Level.sav evidence.
- Added bounded active-world file/player-save metadata inventory.
- Added shared Avalonia World Explorer page.
- Made Doctor version assembly-derived.
- Changed inapplicable secured-listener lifecycle acceptance from Warning to Skip.
- Added skipped-count evidence to Linux acceptance.


## v0.3.1.6 — Server Setup & Update Workflows

- Began v0.3.1.6+ feature-parity expansion.
- Added service-owned SteamCMD/Palworld server distribution status and plan APIs.
- Added API-backed Palworld Dedicated Server update/validation.
- Added stopped-server, serialization, cancellation and post-update verification safeguards.
- Added shared Avalonia Setup & Update page.
- Kept MystTiq application updates explicitly separate from Palworld server updates.


## v0.3.1.5 FIX1 — Desktop Version Metadata Cleanup

- Synchronized PalworldManager manifest release metadata to v0.3.1.5.
- Synchronized Avalonia desktop project metadata to v0.3.1.5.
- Added version-metadata regression checks.
- No runtime behavior changed.


## v0.3.1.5 — Production Doctor & Diagnostics GUI

- Added authenticated `/api/v1/doctor` endpoint and shared production-health evidence model.
- Added functional Avalonia Doctor page with PASS/WARNING/FAIL summary, evidence, recommendations, timestamp, and report export.
- Added Linux acceptance coverage for the Doctor endpoint.


## v0.3.1.4 — Backup & Configuration

- Added authenticated backup inventory/create/delete/restore API.
- Added managed filename/path containment checks and verified `.partial` backup commit.
- Added safe restore requiring PalServer stopped, pre-restore safety backup, and staging/rollback.
- Added restricted headless configuration GET/PUT API.
- Added validation + timestamped rollback copy before configuration writes.
- Preserved authentication/TLS security structures outside the editable DTO.
- Added functional Avalonia Backups page and headless configuration editor.
- Added v0.3.1.4 Linux acceptance coverage for backup/config read contracts.


## v0.3.1.3 FIX6 — Harness Assertion Cleanup

- Replaced one brittle exact-line deployment assertion with behavior-oriented checks.
- Preserved parser-safe version resolution and dynamic archive-selection regression coverage.
- No runtime code changed.


## v0.3.1.3 FIX5 — PowerShell Deploy Parameter Syntax Hotfix

- Replaced invalid command invocation in the `Version` parameter default.
- Current project version is now resolved after parameter binding and project-root resolution.
- Preserved optional explicit `-Version` override.
- Added parser-pattern regression checks.
- No runtime behavior changed.


## v0.3.1.3 FIX4 — Dynamic Deployment Version & Validation Cleanup

- Removed hard-coded v0.3.0.7 deployment version.
- Headless deployment now derives the current project version dynamically.
- Headless archive selection is version-driven.
- Linux acceptance/production gate paths remain dynamically versioned.
- No runtime behavior changed.


## v0.3.1.3 FIX3 — Linux Gate Version-Path Validation Cleanup

- Eliminated six false stale-version warnings caused by literal versioned Linux gate filenames in the logic harness.
- Current Linux acceptance/production script paths are now derived from `Get-ProjectVersion.ps1`.
- Preserved all Linux monitoring gate checks.
- No runtime behavior changed.


## v0.3.1.3 FIX2 — Linux Acceptance & Production Gate Restoration

- Added release-version-matched v0.3.1.3 Linux acceptance and production-readiness runners.
- Added monitoring endpoint/payload checks to Linux acceptance.
- Added explicit evidence that AdminPassword is absent from the player API response.
- Removed legacy v0.3.0.7 Linux gate scripts from the active full-source baseline.
- Updated cleanup automation for ChangedFiles overlays.


## v0.3.1.3 FIX1 — Headless Monitoring HttpVersion Compile Hotfix

- Added the missing `System.Net` namespace required by `HttpVersion.Version11`.
- Preserved explicit HTTP/1.1 Palworld REST requests.
- Added compile-contract regression checks.
- No runtime behavior changed.


## v0.3.1.3 — Logs, Players & Monitoring

- Added authenticated `/api/v1/players`, `/api/v1/logs/tail`, and `/api/v1/metrics` endpoints.
- Added server-side Palworld REST player polling over loopback only.
- Kept Palworld AdminPassword server-side; it is never returned to Avalonia.
- Added bounded PalServer log-tail reads.
- Added PalServer CPU, working-set RAM and thread metrics.
- Added shared Players and Monitoring pages.
- Added Dashboard player-count/CPU/RAM summaries and recent metric history.
- Preserved v0.3.1.2 lifecycle behavior and v0.3.1.1 connection-profile security.


## v0.3.1.2 FIX1 — Version Consistency Cleanup

- Removed obsolete v0.3.1.1 cleanup script from active source.
- Removed literal stale-version text from the current cleanup helper.
- Updated Avalonia desktop project metadata to v0.3.1.2.
- Added regression checks for version-consistency hygiene.
- No runtime behavior changed.


## v0.3.1.2 — Dashboard & Lifecycle

- Added API-backed Start / Stop / Restart to the shared Avalonia desktop.
- Added live PalServer readiness, PID and UDP listener evidence.
- Added MystTiq system-service state.
- Added last-observed, last-transition and uptime presentation.
- Added optional five-second auto refresh.
- Lifecycle mutations reacquire authoritative status/service evidence.
- Desktop remains an API client and never directly owns PalServer.


## v0.3.1.1 FIX1 — Deployment Version & Warning Cleanup

- Removed stale v0.3.1.0 references from the Linux desktop deployment helper.
- Made Linux desktop archive naming version-driven.
- Removed the unused generic `CanExecuteChanged` backing-field compiler warning.
- Added regression checks for both cleanup items.
- No runtime behavior changed.


## v0.3.1.1 — Shared Shell, Navigation & Connection Foundation

- Added working Avalonia navigation across Dashboard, Server, Players, Backups, Mods, Doctor and Settings.
- Added persistent local/remote connection profiles.
- Bearer tokens remain process-memory-only and are never saved with profiles.
- Added optional SHA-256 TLS certificate pinning while preserving normal OS trust validation by default.
- Added automated Linux Avalonia desktop build/deploy/hash/smoke-launch helper.
- GUI remains an API client and does not own PalServer lifetime.


## v0.3.1.0 FIX6 — Actual RunBuild Block Locator Hotfix

- Replaced first-string `IndexOf()` RunBuild detection with a line-level regex locator.
- The harness now selects the final real `if($RunBuild){` block instead of its own string literal.
- Independently validated the extracted build block during package generation.
- No application/runtime behavior changed.


## v0.3.1.0 FIX5 — RunBuild Boundary Marker Hotfix

- Corrected the logic harness RunBuild end marker from nonexistent `$report=` to the actual `$passed=` summary boundary.
- Added regression coverage for the real RunBuild boundary marker.
- No application/runtime behavior changed.


## v0.3.1.0 FIX4 — RunBuild Block Boundary Hotfix

- Fixed the final false-negative build-gate regression check.
- RunBuild source inspection now stops at the report-generation boundary instead of scanning the remainder of the harness.
- Prevents the regression assertion from detecting its own `$LASTEXITCODE` text.
- No application/runtime behavior changed.


## v0.3.1.0 FIX3 — Harness Self-Reference Regression Hotfix

- Fixed two false-negative FIX2 logic checks caused by self-referential source scanning and PowerShell string interpolation.
- Build-gate regression checks now inspect only the actual RunBuild block.
- Validate, Windows desktop and Linux desktop non-throwing-completion semantics are all checked structurally.
- No application/runtime behavior changed.


## v0.3.1.0 FIX2 — PowerShell Build-Gate Exit Semantics Hotfix

- Fixed false harness failure caused by checking stale `$LASTEXITCODE` after successful PowerShell build-script execution.
- PowerShell build actions now PASS on non-throwing completion and FAIL on exceptions.
- Added regression coverage preventing native exit-code semantics from being reused for PowerShell build actions.
- No runtime application behavior changed.


## v0.3.1.0 FIX1 — Avalonia Font Dependency & Desktop Compile-Gate Hotfix

- Added the missing `Avalonia.Fonts.Inter` dependency required by `.WithInterFont()`.
- Updated the public docs index to the v0.3.1.0 desktop foundation candidate.
- Strengthened the v0.3.1.0 `-RunBuild` harness to compile/publish Windows and Linux Avalonia targets.
- Added regression checks tying the Inter bootstrap to its package dependency.
- No accepted headless/WPF/server runtime behavior changed.


## v0.3.1.0 — Avalonia Desktop Foundation

- Added the first shared Windows/Linux Avalonia desktop project.
- Added MVVM shell, MystTiq theme foundation and API client boundary.
- Added local connection profile and live `/api/v1/status` connection.
- Added Windows/Linux desktop publish build actions.
- Preserved Windows WPF during cross-platform parity development.


## v0.3.0.7 FIX4 — Headless Help, Linux Documentation & GUI Roadmap Completion

- Added `production-doctor` to built-in headless help.
- Added complete Linux command/path/security documentation.
- Added Linux production workflow to the main README.
- Selected Avalonia for v0.3.1.x shared Windows/Linux GUI.
- Defined v0.3.1.0 as Avalonia Desktop Foundation.
- No accepted Linux runtime behavior changed.


## v0.3.0.7 FIX3 — Production Readiness Result Accounting Hotfix

- Fixed Bash post-increment exit-status behavior causing PASS checks to also execute FAIL branches.
- `record` now uses assignment-based counter increments and explicitly returns success.
- Executable and disk-reserve checks now use explicit `if / elif / else` control flow.
- Added regression coverage for production-readiness result accounting.
- The underlying Production Doctor remains unchanged and had already passed 9/9 checks.


## v0.3.0.7 FIX2 — Extended Production-Readiness Integration Hotfix

- Fixed extended Linux acceptance not invoking the packaged v0.3.0.7 production-readiness runner.
- Extended acceptance now passes the active executable and config paths into the production-readiness test.
- Production-readiness output is captured in the timestamped Linux acceptance report.
- A readiness failure now increments the Linux acceptance failure count and blocks promotion.
- No runtime/API/lifecycle/systemd/TLS/PalServer/Windows WPF behavior changed.


## v0.3.0.7 FIX1 — Production Doctor Compile & Release-Gate Hotfix

- Fixed Production Doctor references to the existing `IServerPathProfile` contract.
- Routed PalServer readiness through the established `LinuxServerLifecycleService`.
- Fixed the v0.3.0.7 logic harness architecture path.
- Removed active stale v0.3.0.6 HeadlessHost wording.
- Prevented the cleanup helper from creating stale-version warnings.
- Corrected apply order: Unblock PowerShell scripts before running cleanup.


## v0.3.0.7 — Linux Integration & Production Readiness

- Added production-doctor with evidence/recommendation output.
- Added first-run and safe-upgrade Linux automation.
- Added one-command production-readiness reporting.
- Integrated production readiness into extended Linux acceptance.
- Reserved v0.3.0.8+ for stabilization only.


## v0.3.0.6 FIX5 — Acceptance Harness Consistency Hotfix

- Fixed two false-negative logic-harness checks after successful real-world remote API acceptance.
- Enrollment-version validation now matches the stable v0.3.0.6 base contract instead of exact decorated FIX text.
- Token-persistence validation now checks stable semantic markers rather than exact spacing-sensitive output.
- Synchronized Linux enrollment and Windows remote-acceptance display labels to FIX5.
- No runtime/API/TLS/auth/systemd/PalServer/Windows WPF behavior changed.


## v0.3.0.6 FIX4 — Running-Binary Service Install Hotfix

- Fixed Linux `service-install` failing with `Text file busy` while `/opt/mysttiq/bin/mysttiq-server` was running.
- Replaced in-place executable overwrite with same-directory staged copy plus atomic rename.
- Existing systemd restart behavior now launches the replaced binary while the old process can finish on its previous inode.
- Added regression coverage for staged executable replacement.
- No API, TLS, authentication, configuration schema, PalServer lifecycle policy, or Windows WPF behavior changed.


## v0.3.0.6 FIX3 — Protected Secrets Directory Ownership Hotfix

- Fixed `/etc/mysttiq/secrets` being created as root-only while MystTiq secrets were owned by the non-root service user.
- Secrets directory is now owned by the selected service account with mode 0700.
- Enrollment explicitly verifies secrets-directory owner/mode.
- Token/certificate existence checks now use privilege-safe `sudo test -s` checks.
- Preserved per-file mode 0600 and pre-commit rollback behavior.
- No runtime/API/security-policy/lifecycle/PalServer/Windows WPF behavior changed.


## v0.3.0.6 FIX2 — Linux Remote-Tool Packaging Hotfix

- Fixed Linux headless archives omitting the v0.3.0.6 remote-enrollment and rollback scripts.
- `Build-LinuxHeadless.ps1` now packages the acceptance runner, remote enrollment script, and remote disable script.
- Missing required Linux-side scripts now fail the package build instead of producing an incomplete archive.
- Added regression coverage for all required Linux operational scripts.
- No runtime, API, security, lifecycle, systemd, PalServer, or Windows WPF behavior changed.


## v0.3.0.6 FIX1 — Remote Enrollment Reliability Hotfix

- Fixed remote enrollment leaving MystTiq on the previous loopback/schema-v1 configuration.
- Reworked Linux enrollment into a staged fail-fast workflow with prerequisite, file, ownership, configuration, service, listener, HTTPS and authentication verification.
- Added automatic pre-enrollment configuration backup and pre-commit rollback.
- Enrollment now verifies token/PFX/password creation instead of assuming commands succeeded.
- Enrollment now reads the effective config back and requires the requested bind plus auth/TLS before restarting.
- Enrollment now verifies the exact LAN listener before reporting success.
- Windows remote acceptance now validates effective config, token and listener prerequisites before attempting HTTPS.
- Missing token/listener/connectivity now produces explicit `[FAIL]` output instead of null-reference PowerShell exceptions.
- No core API security policy, configuration schema, lifecycle, systemd, PalServer, or Windows WPF behavior changed.


## v0.3.0.6 — Secure Remote API Enrollment & TLS Provisioning

- Promoted v0.3.0.5 as the official Linux/headless baseline.
- Added `HeadlessCertificateService` for self-signed TLS server certificate provisioning.
- Generated certificates use RSA 3072, SHA-256, server-auth EKU, selected IP SAN, localhost SAN, optional DNS SAN, and a maximum 825-day validity.
- PFX passwords are generated separately using the existing protected-secret service.
- Added `HeadlessRemoteApiEnrollmentService` for typed remote enable/disable configuration updates.
- Added `api-tls-create`, `api-remote-enable`, and `api-remote-disable`.
- Remote enable requires an explicit non-loopback literal IP and produces an auth+TLS configuration; remote disable returns to loopback defaults.
- Added `scripts/Configure-MystTiqRemoteApi.sh` for one-command Linux enrollment with confirmation, one sudo authorization, secret/certificate provisioning, service-user ownership hardening, systemd restart and HTTPS/auth validation.
- Added `scripts/Disable-MystTiqRemoteApi.sh` for one-command return to local-only management.
- Added `scripts/Test-MystTiqRemoteApi.ps1` for Windows-over-LAN HTTPS/401/bearer-auth acceptance using the dedicated SSH trust channel.
- Linux enrollment intentionally does not alter firewall rules.
- Extended the Linux acceptance runner with temporary TLS certificate creation and explicit secured remote-config/rollback tests.
- Windows WPF behavior remains unchanged.


## v0.3.0.5 — Passwordless Linux Deployment & SSH Trust Foundation

- Promoted v0.3.0.4 FIX1 as the official Linux/headless baseline.
- Added `scripts/Initialize-MystTiqLinuxSSH.ps1` for one-time dedicated Ed25519 deployment-key setup.
- The SSH bootstrap installs only the public key into `~/.ssh/authorized_keys`; the private key remains on Windows.
- Added passwordless-key verification using OpenSSH batch/public-key-only authentication.
- Reworked `scripts/Deploy-Test-MystTiqLinux.ps1` to prefer the dedicated MystTiq SSH key automatically.
- Deployment now uses the same dedicated identity for SSH and SCP.
- Interactive password deployment is disabled by default and available only through explicit `-AllowPasswordFallback`.
- Removed normal dependence on Posh-SSH/password caching from the deployment workflow.
- Preserved SHA-256 transfer validation, versioned extraction, and automated Linux acceptance invocation.
- Carried the version-matched Linux acceptance runner forward as `scripts/Test-v0.3.0.5-LinuxAcceptance.sh`.
- No management API, configuration schema, TLS/authentication, PalServer lifecycle, systemd, or Windows WPF behavior changed.


## v0.3.0.4 FIX1 — HeadlessHost Compile & Automation Harness Hotfix

- Fixed CS1929 by invoking `WaitForShutdownAsync(CancellationToken)` through the `IHost` generic-host interface.
- Corrected the deployment harness literal for the actual `& .\Build.ps1 LinuxHeadless` invocation.
- Prevented the release validator from treating the versioned Linux acceptance-script filename as a stale-version suffix.
- Added regression coverage for the `IHost` shutdown bridge.
- No security model, configuration schema, API behavior, lifecycle policy, Linux acceptance behavior, or Windows WPF behavior changed.


## v0.3.0.4 — Secure Management API & Automated Linux Acceptance Foundation

- Promoted v0.3.0.3 FIX1 as the official Linux/headless baseline.
- Advanced headless configuration to schema version 2 with API authentication and TLS settings.
- Added supported in-memory migration from schema v1 plus `config-migrate` persistence and automatic migration during `service-install`.
- Added protected secret-file service with cryptographically random bearer-token generation and Linux owner-only token permissions.
- Added `api-token-create` command.
- Added bearer-token authentication for `/api/v1/*` while keeping `/healthz` intentionally minimal and unauthenticated.
- Added fixed-time token comparison.
- Added TLS certificate/password-file support for Kestrel.
- Non-loopback API configuration now fails closed unless authentication and TLS are both enabled; the runtime API host independently enforces the same condition.
- Default configuration remains loopback-only and does not require authentication/TLS.
- Added version-matched `scripts/Test-v0.3.0.4-LinuxAcceptance.sh` with consolidated PASS/FAIL/WARN output plus timestamped raw evidence.
- Added `scripts/Deploy-Test-MystTiqLinux.ps1` to build, verify, copy, extract and invoke Linux acceptance in one workflow; default Linux test host is `192.168.1.248`.
- Linux publish archives now include their version-matched Linux acceptance runner.
- Added the detailed product roadmap through v0.8, including v0.4 character migration/Doctor/UI consolidation, v0.5 advanced administration, v0.6 multi-server, v0.7 themes/icon set and v0.8 adaptive efficiency.
- Windows WPF behavior remains unchanged.


## v0.3.0.3 FIX1 — Configuration Compile & systemd Argument-Quoting Hotfix

- Corrected Linux absolute-path validation to use `StartsWith("/", StringComparison.Ordinal)` instead of the invalid char/StringComparison overload.
- Added the missing `QuoteSystemdArgument(string value)` helper used by systemd `ExecStart` configuration-path generation.
- Added newline-injection rejection to the systemd argument-quoting helper.
- Strengthened the v0.3.0.3 harness to cover both compile contracts.
- No configuration schema, API, lifecycle, recovery, or Windows WPF behavior changed.


## v0.3.0.3 — Headless Configuration & Local Management API Foundation

- Promoted v0.3.0.2 FIX2 as the official Linux/headless baseline.
- Added schema-versioned persistent headless configuration with Linux defaults under `/etc/mysttiq/mysttiq.json`.
- Added configuration validation for paths, lifecycle/recovery values, API port, and API bind scope.
- v0.3.0.3 rejects non-loopback API binding by design.
- Added `config-show`, `config-validate`, and `config-write-default`.
- Added `config/mysttiq.linux.example.json`.
- Lifecycle CLI and service supervisor now consume configured timeouts, paths, recovery settings, and PalServer launch arguments.
- Added ASP.NET Core/Kestrel local management API support to the headless host.
- Added loopback-only health, lifecycle status, systemd status, configuration, and start/stop/restart endpoints.
- Lifecycle-changing API calls are serialized to prevent competing operations.
- `service-run` starts/stops the local API with the supervisor when API support is enabled.
- Added standalone `api-run` for local API testing without systemd.
- `service-install` creates a default configuration when one does not exist and preserves a custom `--config` path in systemd `ExecStart`.
- No remote/LAN API exposure, authentication secrets, or Windows WPF changes are introduced in this phase.
- Added Windows v0.4 backport candidates for shared configuration, local service IPC/API, and UI-to-background-service control.


## v0.3.0.2 FIX2 — systemd Start-Limit Section Hotfix

- Moved `StartLimitIntervalSec=300` and `StartLimitBurst=5` from `[Service]` to `[Unit]`.
- Preserved `Restart=on-failure` and `RestartSec=10` under `[Service]`.
- Added regression coverage for correct directive ordering.
- No lifecycle, recovery algorithm, PalServer launch, or Windows WPF behavior changed.


## v0.3.0.2 FIX1 — systemd Unit Raw-String Compile Hotfix

- Replaced the malformed raw-string `BuildUnit()` implementation with compile-safe string construction.
- Preserved the exact v0.3.0.2 systemd unit policy and lifecycle behavior.
- Strengthened the v0.3.0.2 harness to detect raw-string regression and verify generated unit sections.
- No Windows WPF behavior changed.


## v0.3.0.2 — Linux Service & Automatic Recovery Foundation

- Added systemd service models and `LinuxSystemdServiceManager`.
- Added `service-status`, `service-install`, `service-uninstall`, and internal `service-run` headless commands.
- Service installation copies the current self-contained headless host to `/opt/mysttiq/bin/mysttiq-server`, writes `/etc/systemd/system/mysttiq-palworld.service`, reloads systemd, and enables boot startup.
- Service start remains explicit unless `--start-now` is supplied.
- Added a long-running `LinuxHeadlessSupervisor` that starts/adopts PalServer, monitors lifecycle state, and performs bounded automatic crash recovery with configurable backoff/restart windows.
- Added SIGTERM/SIGINT handling so systemd shutdown asks MystTiq to perform the existing graceful PalServer shutdown policy.
- The service runs as the selected non-root service account, with `NoNewPrivileges=true`, `Restart=on-failure`, restart delay, and systemd start-rate limits.
- systemd journal output now captures MystTiq service/supervisor logs while PalServer console output remains under `/opt/mysttiq/runtime`.
- Promoted v0.3.0.1 FIX1 as the official Linux/headless baseline.
- Added v0.4 backport candidates for Windows Service supervision, automatic recovery throttling, background journal/event logging, and UI-independent startup recovery.


## v0.3.0.1 FIX1 — Linux Lifecycle Compile Contract Hotfix

- Added `FindProcessesByName(...)` to the shared `IServerSessionInspector` contract; the Linux implementation already provided the method and the lifecycle layer consumes it.
- Marked `LinuxServerLifecycleService` with `[SupportedOSPlatform("linux")]` so .NET platform analysis recognizes intentional Unix-only API use such as `File.SetUnixFileMode`.
- Strengthened the v0.3.0.1 harness to validate the shared process-discovery contract and Linux platform annotation.
- No lifecycle semantics or Windows behavior changed.


## v0.3.0.1 — Linux Server Lifecycle Control

- Added shared lifecycle phase, operation-result, persisted-state, and stable headless exit-code models.
- Added `LinuxServerLifecycleService` with `start`, `stop`, `restart`, and lifecycle-aware `status`.
- Linux start blocks duplicate PalServer instances, launches the server detached from the SSH terminal, observes the native `PalServer-Linux-Shipping` process, and verifies UDP 8211 before reporting ready.
- Detached PalServer stdout/stderr is written to `/opt/mysttiq/runtime/palserver-console.log`.
- Added persisted lifecycle state under `/opt/mysttiq/runtime/lifecycle-state.json` for transition/crash evidence across short-lived CLI invocations.
- Linux stop sends SIGTERM first and escalates to SIGKILL only after the graceful timeout; force escalation is surfaced in the operation result.
- Restart safely stops an active server before starting it; restart from stopped/crashed state proceeds as a start.
- Added Ctrl+C cancellation handling and configurable startup/stop timeouts to the headless CLI.
- Corrected Linux kernel reporting to read `/proc/sys/kernel/osrelease`; the validated host previously exposed distro text through `RuntimeInformation.OSDescription`.
- Preserved `probe` and informational `install-plan`, including the validated SteamCMD `+@sSteamCmdForcePlatformType linux` requirement.
- Windows WPF behavior remains unchanged; lifecycle/service backport opportunities are recorded for v0.4.

## v0.3.0.0 FIX1 — Regression Harness Reserved Variable Hotfix

- Corrected the v0.3.0.0 PowerShell harness to avoid the read-only built-in `$Host` variable by using `$headlessHostSource`.
- Runtime validation then passed 55/55 and the self-contained Linux host successfully detected Ubuntu, SteamCMD, `PalServer-Linux-Shipping`, and UDP 8211.


## v0.3.0.0 — Headless Core Foundation & Linux Platform Base

- Added `MystTiq.Core` as a new `net10.0` cross-platform project with no WPF dependency.
- Added `MystTiq.HeadlessHost` as a no-GUI command-line host for Linux/headless development.
- Added platform-neutral runtime configuration separate from the Windows WPF `AppSettings` model.
- Added Linux distribution detection through `/etc/os-release` plus kernel/architecture reporting.
- Added Linux server path profile using `/opt/mysttiq/palserver`, `/opt/mysttiq/steamcmd/steamcmd.sh`, LinuxServer config paths, logs, saves, and backup roots.
- Added Linux process/executable profile for `PalServer.sh` and `Pal/Binaries/Linux/PalServer-Linux-Shipping`.
- Added `LinuxServerDistributionPlatformService` with Valve Linux SteamCMD package handling and Palworld App `2394010`.
- Baked the validated `+@sSteamCmdForcePlatformType linux` SteamCMD override into Linux install/update argument generation after the reference environment returned `Missing configuration` without it.
- Added read-only Linux procfs session inspection for process trees, mapped files, and guarded TCP/UDP port observation.
- Added non-destructive headless commands: `probe`, `status`, and `install-plan`; v0.3.0.0 does not yet grant the headless host start/stop/install/update authority.
- Added a dedicated Linux self-contained publish workflow through `Build.ps1 LinuxHeadless`.
- Added Linux tested-environment documentation for Ubuntu Server 24.04.4 LTS x86_64, kernel `6.8.0-137-generic`, Valve Linux SteamCMD, and native PalServer validation.
- Added the formal SHARED / LINUX / WINDOWS-BACKPORT registry; Windows headless/service/minimized-resource improvements are reserved for v0.4 unless shared-core correctness requires earlier work.
- Preserved v0.2.16.4 as the frozen validated Windows baseline; no intentional WPF behavior changes are included in this phase.


## v0.2.16.4 — SteamCMD Distribution Abstraction & Final Windows Platform Audit

- Added `IServerDistributionPlatformService` as the platform boundary for SteamCMD package source, extraction, command arguments, process startup policy, and default Palworld install recovery.
- Added `WindowsServerDistributionPlatformService`, preserving the validated Windows SteamCMD behavior and Palworld Dedicated Server App ID 2394010.
- Added `ServerDistributionPlatformService.ForCurrentPlatform()` so higher-level installer/update services no longer construct the Windows implementation directly.
- Migrated `InstallerService` SteamCMD install/self-update and Palworld server install/retry/recovery logic to the shared distribution service.
- Migrated `SteamServerUpdateService` command construction and SteamCMD process startup to the shared distribution service.
- Application composition now creates one shared distribution service for server update and installer workflows.
- Centralized remaining core-service SteamCMD executable-name usage through `ServerPlatformProfile`.
- `ApplicationPathService` workspace/default SteamCMD discovery now uses the platform executable name instead of embedding `steamcmd.exe`.
- Server diagnostics now consume platform process names instead of hard-coded PalServer process names.
- Completed a final Windows platform audit and documented remaining WPF/UI-specific assumptions for the v0.3 Linux foundation.
- No intended changes to current Windows install/update semantics, server lifecycle, MOD evidence, Operational Health, World Inspector safety, or WORLD PULSE.
- Completed a documentation closeout after successful v0.2.16.4 build/runtime validation: removed historical implementation notes from the repository root, archived them under `docs/history/`, moved the current platform audit under `docs/architecture/`, moved publication-process material under `docs/release/`, and rebuilt README/RELEASE_CHECKLIST as current-state documents.
- Updated the public `docs/index.html` baseline/RC wording and Linux-support statement.


## v0.2.16.3 FIX2 — Legacy MystTiq Release-Source State Removal

- Removed the obsolete MystTiq release-source fallback from `FinalizeUpdateCheckResults()`.
- Removed the obsolete status from the generic Update Center action mapper.
- GitHub comparison failures now use the retryable `UNABLE TO CHECK` state.
- Strengthened regression coverage so the legacy state cannot silently return.
- No unrelated runtime behavior changed.


## v0.2.16.3 FIX1 — Update Center Regression Harness False-Positive Hotfix

- Corrected the v0.2.16.3 logic harness so the obsolete manager release-source placeholder check is scoped specifically to the `MystTiq Server Manager` component branch.
- Generic Update Center fallback compatibility text no longer causes a false failure.
- No application source, UI, update behavior, or runtime logic changed.


## v0.2.16.3 — Application Update Awareness & Server Setup Polish

- Added `MystTiqReleaseService` backed by the public GitHub `releases/latest` endpoint for `Wad3M/MystTiq-Palworld-Server-Manager`.
- Added numeric MystTiq version parsing/comparison with explicit `UPDATE AVAILABLE`, `UP TO DATE`, and `DEVELOPMENT BUILD` states.
- Server Setup `CHECK FOR UPDATES` now includes the installed MystTiq version and latest public GitHub release alongside SteamCMD, Palworld, UE4SS, and Workshop information.
- Update Center now performs the same MystTiq GitHub release check and opens the official Releases page when a newer manager release is available.
- GitHub/network failures are fail-soft and do not prevent the remaining component update checks.
- Reduced the Server Environment status badge column, padding, and font size; READY/MISSING/DISABLED/OPTIONAL badges now share compact centered geometry.
- Preserved existing button/theme/tooltip semantics, server lifecycle logic, live-save safety, MOD runtime evidence, Operational Health, and WORLD PULSE behavior.


## v0.2.16.2 — Live Save Read Safety & UI Button Standardization

- Added `SafeWorldSaveSnapshotService` to create stable temporary snapshots of live Palworld save files using `FileShare.ReadWrite | FileShare.Delete`.
- Snapshot creation verifies source length and last-write stability and retries around PalServer save/write windows.
- World Inspector header inspection now reads the stable snapshot instead of opening active `Level.sav` directly.
- Recoverable live-save contention is reported in the Inspector status area without a blocking modal error.
- Hardened `Plm1SaveDecoder` header reads with shared file access for other read-only callers.
- Reduced the shared MystTiq button footprint by approximately 10% while preserving semantic colors, common template behavior, tooltip standards, borders, rounded corners, hover/pressed behavior, and typography hierarchy.
- Normalized Workspace, MOD, Player, Config, icon, and DataGrid button variants to the shared density standard.
- Converted 42 button-only horizontal action clusters to equal-cell `UniformGrid` layouts for consistent grid alignment.
- Removed common per-button size overrides where the semantic/shared style should be authoritative.
- No intended changes to server lifecycle, MOD runtime evidence, Operational Health, World Pulse semantics, save contents, or destructive world-repair behavior.


## v0.2.16.1 — Runtime Path & Deployment Abstraction

- Introduced `IServerPathProfile` and `WindowsServerPathProfile`.
- Centralized deployment/runtime path construction and shared it through the composition root.
- Migrated core UE4SS/MOD/environment/installer/doctor services from direct Win64 path construction.
- Preserved validated Windows behavior; Linux implementation remains deferred.


## v0.2.15.17 — Live World Telemetry & Dashboard Pulse

- Added `WorldTelemetryService` with server-session-scoped player metrics: current online, peak online, joins, leaves, unique players, and last player transition.
- Added `WorldClockProvider` to read the authoritative saved Palworld clock from decoded `Level.sav` `GameTimeSaveData.GameDateTimeTicks`.
- Added `WorldTelemetrySnapshot` / `WorldClockSnapshot` models and centralized telemetry composition.
- Added `ServerService.ActiveSessionId` and `ActiveSessionStartedAt` read-only session metadata for true PalServer uptime.
- Added a Dashboard `WORLD PULSE` strip showing saved world day/time, session uptime, session player metrics, save freshness, latest backup age, and last player event.
- Player join/leave and saved-world day changes can flow into the existing Activity/Audit feed.
- The existing Dashboard UPTIME indicator now reflects the active PalServer session rather than MystTiq process uptime.
- World-clock values are never estimated from uptime; if authoritative save evidence is unavailable, MystTiq displays the clock as unavailable.
- No intended changes to MOD health/runtime evidence, server lifecycle semantics, dark theme, button standards, or tooltip standards.


## v0.2.15.16 — Platform Profile Abstraction & Documentation Consistency

- Added `ServerPlatformProfile` as the centralized source for PalServer process names, executable-relative paths, SteamCMD executable naming, and guarded ports.
- Removed hard-coded Windows PalServer process names and guarded-port constants from `ServerService`.
- `ServerProcessDiscoveryService`, `ServerResourceMonitor`, `ServerSessionInspector`, and `WindowsServerPlatformOperations` now receive their conventions through the selected platform profile.
- Preserved the Windows profile and all current Windows lifecycle behavior as the default.
- Performed a full README consistency rewrite: removed duplicate Feature Matrix/release-diary content, removed stale baseline/RC claims, consolidated MOD runtime-health semantics, corrected Linux wording, and made CHANGELOG/release-notes authoritative for historical version detail.
- No intended UI, dark-theme, button, tooltip, MOD evidence, operational-health, or lifecycle behavior changes.


## v0.2.15.15 — Server Platform Services Abstraction

- Added `IServerPlatformOperations` as the platform boundary for executable resolution, process launch policy, post-launch window handling, forced process-tree termination, and server-process fallback cleanup.
- Added `WindowsServerPlatformOperations` containing the existing Windows-specific implementation.
- `ServerService` now depends on `IServerPlatformOperations` and retains the Windows implementation as its compatibility default.
- Removed Windows `user32.dll` window-hiding P/Invoke and executable-path selection logic from `ServerService`.
- Removed direct `Process.Kill(entireProcessTree: true)` fallback logic from `ServerService`; forced termination now delegates through the platform service.
- Reduced `ServerService` further while preserving the public facade, current session semantics, stream readers, startup/restart behavior, native MOD evidence, and operational-health logic.
- No intended UI, dark-theme, button, tooltip, server configuration, or runtime behavior changes.


## v0.2.15.14 FIX1 — Composition Root Namespace Compile Hotfix

- Added the missing `using PalworldManager.Models;` import to `ApplicationServiceComposition.cs`.
- Restored visibility of `AppSettings` in the new application composition root.
- Added regression-harness coverage for the composition-root namespace dependency.
- No service graph, platform abstraction, UI, MOD health, or runtime behavior changed.


## v0.2.15.14 — Application Composition & Platform Abstraction Preparation

- Added `ApplicationServiceComposition` as an explicit composition root for MystTiq's core server/MOD/diagnostics service graph.
- Removed direct construction of core server/MOD services from `MainWindow`; the window now consumes the composed graph and remains responsible for UI event wiring.
- Added `IServerSessionInspector` as the platform boundary for server-session process tree, loaded-module, descendant-process, and guarded-port inspection.
- `ServerSessionInspector` remains the Windows implementation and preserves v0.2.15.12 session/module behavior.
- `ServerService` now depends on `IServerSessionInspector` while retaining a Windows default implementation for compatibility.
- Established a clean seam for a future Linux session/process inspector without adding OS conditionals throughout server/runtime-evidence logic.
- No intended UI, theme, button, tooltip, health, MOD evidence, start/stop/restart, or server configuration behavior changes.


## v0.2.15.13 FIX2 — Regression Harness Quoting Hotfix

- Fixed PowerShell parsing in `Test-v0.2.15.13-Logic.ps1`.
- Replaced nested double-quoted text in the conflict-regression failure message with PowerShell-safe single-quoted message strings.
- No C# health logic, MOD conflict semantics, UI behavior, or server health scoring changed from FIX1.


## v0.2.15.13 FIX1 — MOD Conflict Health False-Positive Hotfix

- Fixed a server-health false positive where `No known conflict` matched a broad `Contains("Conflict")` check.
- MOD conflict health now requires either `Compatibility == Conflict` or the explicit status `Confirmed conflict`.
- Healthy MODs with `No known conflict` no longer count as confirmed server-health issues.
- Added regression-harness coverage to prevent broad conflict-string matching from returning.
- No change to genuine conflict, missing dependency, runtime error, failed, missing, or misconfigured MOD penalties.


## v0.2.15.13 — Operational Health Model & Application Composition Refinement

- Added `ModPlatformHealthService` as the single source of truth for MOD contribution to server-level health.
- Disabled, Active / Unverified, Active, Installed, and Unknown MOD states are informational/neutral and no longer reduce Overall Health.
- Enabled MOD failures, runtime errors, missing deployment, misconfiguration, state/duplicate attention, confirmed conflicts, and missing dependencies are explicit health issues.
- `DashboardIntelligenceService` now consumes `ModPlatformHealthSnapshot` instead of deriving `installed - healthy`.
- Dashboard MOD card, compact health strip, Overall Health detail, and tooltip now share the centralized MOD health interpretation.
- Preserved v0.2.15.12 FIX1 server lifecycle/process modularization and v0.2.15.10+ runtime evidence behavior.


## v0.2.15.12 FIX1 — Server Inspector Delegation Compile Hotfix

- Repaired three stale `ServerService` references left after process/session inspection was extracted to `ServerSessionInspector`.
- Cleanup now obtains descendant process IDs and guarded listening ports through the extracted inspector.
- Added a public read-only descendant-process query to `ServerSessionInspector`.
- Updated the stale v0.2.15.11 MOD-center source comment to v0.2.15.12.
- Tightened the v0.2.15.12 regression harness to detect stale extracted-helper references before release build.
- No lifecycle behavior, cleanup safety policy, runtime evidence semantics, UI styling, buttons, or tooltips changed.


## v0.2.15.12 — Server Lifecycle & Process Modularization

- Extracted PalServer process-tree, loaded-module, and guarded-port inspection into `ServerSessionInspector`.
- Extracted lifecycle-state interpretation into the pure `ServerLifecycleEvaluator`.
- Reduced `ServerService` while retaining it as the compatibility facade used by the rest of MystTiq.
- Preserved current-session/PID snapshot semantics required by native UE4SS runtime evidence.
- Preserved start, stop, restart, adoption, cleanup, update, resource-monitoring, and I/O behavior.
- Established explicit process/session boundaries that can later be placed behind Windows/Linux platform interfaces.
- No intended UI, theme, button, tooltip, configuration, or runtime-evidence semantic changes.


## v0.2.15.11 FIX1 — MOD Center Extraction Compile Hotfix

- Repaired four interpolated dialog strings in `MainWindow.ModCenter.cs` that were accidentally split across physical source lines during the modularization extraction.
- Restored the original escaped newline behavior (`\n\n`) without changing dialog text or runtime logic.
- Added regression harness checks for the extracted MOD-center dialog strings.
- No MOD architecture, runtime evidence, UI styling, or workflow behavior changed.


## v0.2.15.11 — MOD Architecture Modularization

- Added `ModCoordinator` as the application-facing orchestration boundary for inventory, verification, compatibility, recommendations, and verification export.
- Added `ModDashboardStateService` to own Dashboard projection, merge, health, and summary logic without WPF dependencies.
- Added MOD workflow/result models.
- Extracted the MOD Library/Dashboard/UI workflow from the monolithic `MainWindow.xaml.cs` into `MainWindow.ModCenter.cs`.
- MainWindow now delegates MOD workflow orchestration instead of coordinating backend services directly.
- Preserved runtime evidence, native module verification, UI behavior, button/tooltip standards, and dark theme.
- No intentional user-facing feature or file-format changes.


## v0.2.15.10 FIX1 — Release Synchronization & Harness Hotfix

- Fixed the v0.2.15.10 PowerShell regression harness by replacing the `R` helper name that collided with PowerShell's `r` / `Invoke-History` alias.
- Removed obsolete v0.2.15.9 harnesses from the active `scripts` folder.
- Synchronized `docs/index.html` and `src/PalworldManager/app.manifest` to v0.2.15.10.
- Refreshed release checklist, release notes, build/test plan, apply instructions, and source manifest.
- No native runtime module evidence or MOD verification behavior changed.


## v0.2.15.10 — Native Runtime Module Evidence

- Integrated read-only PalServer process-module evidence for native/hybrid UE4SS mods.
- Exact canonical path matching prevents collisions between mods that both use `main.dll`.
- Native module mapping provides 100% Confirmed Loaded evidence while Confirmed Running still requires functional activity.
- Runtime inspection refreshes the current ServerService session snapshot and includes the PalServer process tree.
- Enumeration failures fail open to Active / Unverified, never a false load failure.


## v0.2.15.9 FIX2 — Native UE4SS Detection & Verification

- Corrected UE4SS inventory classification using actual payload: Lua, Native, Hybrid, or generic UE4SS.
- Added non-executing PE validation and bounded printable-string analysis for native DLL capability hints.
- Native static signatures are diagnostic capability evidence only; they never count as proof that a DLL executed.
- Separated `Active / Unverified` from actual MOD attention/failure counts.
- Dashboard now reports quiet valid MODs as `awaiting runtime confirmation` instead of `need attention`.
- Preserved current-session runtime evidence requirements and observational safety.


## v0.2.15.9 FIX1 — Compile Hotfix

- Added the missing `ConfirmedRunning` member to `RuntimeEvidenceState`.
- Updated the v0.2.15.9 regression harness so PowerShell build steps are judged by exceptions rather than stale `$LASTEXITCODE` values.
- Updated the stale v0.2.15.8 source comment that caused release validation to warn.
- No runtime-verification architecture or functional-detection behavior changed.


## v0.2.15.9 — MOD Functional Verification & Capability Analysis

- Added non-destructive UE4SS capability/source analysis.
- Added Confirmed Running state based on observed current-session functional activity.
- Verification now reports MOD kind, detected runtime APIs, and expected functional proof.
- Preserved v0.2.15.8 FIX1 Active / Unverified semantics for quiet/event-driven mods.
- MystTiq remains observational and does not inject or modify third-party MOD code.


## v0.2.15.8 FIX1 — Runtime Evidence Model & Confidence Engine

- Added Confirmed Loaded, Active / Unverified, Not Loaded, Error, Disabled, and N/A runtime semantics.
- Added evidence confidence, source, matched alias, and explanation.
- Added UE4SS `enabled.txt, starting mod` loader acknowledgement detection.
- Absence of a positive signature no longer falsely means Not Loaded for a correctly deployed/enabled UE4SS mod.
- Preserved current-session evidence boundaries.


All notable public changes will be documented here.

## [0.2.15.8] - 2026-08-09

### Added
- Added `ModRuntimeEvidenceEngine` as the centralized interpreter for UE4SS/Lua positive runtime evidence.
- Added structured evidence explanations with source and matched runtime alias for verification/report diagnostics.
- Added conservative positive UE4SS signatures for Starting/Loading Lua mods and explicit loaded/initialized/registered mod messages.
- Added event-driven runtime-state synchronization so authoritative evidence updates existing MOD Library and Dashboard rows without waiting for a manual Verify All.

### Changed
- RuntimeStateService now delegates positive load-signature extraction to the shared runtime evidence engine instead of owning a single `Starting Lua mod` regex.
- UE4SS verification now consumes the same authoritative runtime state as the MOD Library and treats `LoadedByUe4ss` as positive evidence when the inventory has already resolved a valid runtime identity.
- Runtime verification details now explain whether evidence came from the unified runtime session or unified inventory state.

### Fixed
- Prevented MOD Dashboard rows such as AntiDupe and PalImportFilter from remaining **Runtime Unverified** while the MOD Library already reports them **Loaded**.
- Removed the verification timing window where runtime evidence could arrive after a Dashboard scan but before a later manual refresh.

## [0.2.15.7] - 2026-08-09

### Added
- Added `RuntimeStateService` as the authoritative, session-aware owner for MOD runtime-loaded evidence.
- Added immutable `RuntimeStateSnapshot` records with session ID, revision, timestamps, runtime log identity, loaded aliases, runtime errors, and health metadata.
- Added session-bound UE4SS log offsets so new sessions consume only newly written runtime evidence instead of inheriting historical `Starting Lua mod` lines.
- Added runtime-state diagnostics to the MOD Runtime view, including session ID, revision, loaded alias count, and last observation time.

### Changed
- MOD inventory scans now observe runtime evidence through `RuntimeStateService` and apply the shared snapshot to MOD rows.
- MOD Library refreshes no longer own a private runtime-loaded latch.
- Server session preparation starts a new runtime-state session; server exit clears the authoritative runtime state.
- Runtime verification/export continues to use the same inventory rows, which are now populated from the centralized runtime source of truth.

### Fixed
- Prevented periodic MOD Library refreshes and UE4SS log changes from erasing valid current-session loaded state.
- Prevented runtime-loaded evidence from a previous PalServer session from being carried into a new session.

### v0.2.15.7 FIX1 — Compile hotfix
- Fixed the two-argument `ModService` constructor to chain through its declared `ue4ssResolver` parameter instead of the stale pre-refactor identifier.
- Removed an unused `RuntimeStateService` field from `GenericModVerifier`, clearing the migration warnings without changing verification behavior.
- Added a regression check for the constructor-chain compile failure.

### v0.2.15.6 FIX2 — Runtime-loaded session persistence
- Fixed MOD Library runtime-loaded state reverting to **Not loaded** after UE4SS log rotation/refresh.
- Positive UE4SS load evidence is now latched for the current PalServer session and is not erased by later logs that omit startup lines.
- Session-loaded evidence resets on server exit and before a new server session, preventing stale cross-session status.
- Added regression checks for session-latch application and reset boundaries.

## [0.2.15.6 FIX1] - 2026-08-08

### Fixed
- MOD Library UE4SS/Lua `LOADED` state now refreshes dynamic runtime evidence instead of reusing a pre-start cached resolver snapshot.
- Workshop and managed packages now retain UE4SS runtime-folder aliases so `Starting Lua mod` evidence can match the actual runtime identity even when package/friendly names differ.
- The MOD Library automatically synchronizes after the 45-second startup evidence window.
- Logic regression harness now recognizes the `report.CanStart` gate implementation and no longer treats stale native `$LASTEXITCODE` values as failed PowerShell build steps.

## [0.2.14.9 FIX3] - 2026-08-06

### Changed
- Update Center row actions now use compact sizing that fits the action column.
- Update Center actions now use semantic colors for update, verify, source, install, retry/check, enable/disable, manage, and create operations.

### Fixed
- Admin Commands runtime status now recognizes common package-name and successful-load message variants.
- Admin Commands status now updates from both process output and tailed `Pal.log` content, including when MystTiq adopts an already-running server.

### v0.2.14.9 FIX1
- Added required Windows installer generation and Inno Setup bootstrap tooling.
- Installer assets now publish to `artifacts` with SHA-256 checksums.
- UE4SS runtime compatibility now refreshes from the live MOD inventory after every MOD state change.
- Removed stale hard-coded MOD compatibility guidance.

## [0.2.14.9 FIX2] - 2026-08-06

### Fixed
- Mouse-wheel routing across nested grids, lists, logs, and page-level scroll viewers.
- Diagnostics Center now scrolls correctly when the pointer is over the results grid.
- Parent pages automatically receive wheel input when a nested control reaches its scroll boundary.

## [0.2.14.9] - 2026-08-06

### Added
- Release-candidate validation for repository hygiene, XAML resources, event handlers, version consistency, dialog usage, and release documentation.
- Unified `Build.ps1 Validate` action.

### Changed
- Full release preparation now validates the source tree before build and packaging.

## [0.2.14.8] - 2026-08-06

### Added
- Central application constants for non-user-configurable timing and network defaults.
- Dedicated MainWindow lifecycle partial for startup, timer ownership, cancellation, and shutdown cleanup.
- Window-lifetime cancellation token to stop startup work cleanly when the application closes.

### Changed
- MainWindow constructor no longer contains the complete loaded/closed orchestration logic.
- Monitor, automation, and heartbeat timer subscriptions now have named handlers and are explicitly detached at shutdown.
- Startup and monitor exceptions are isolated, logged, and surfaced through the notification infrastructure without crashing the UI thread.
- Long-lived cancellation sources and disposable services are released through one predictable shutdown path.
- Repeated operational timing values now consume shared constants to reduce configuration drift.

### Safety
- No world, player, guild, base, backup, MOD, or configuration-editing behavior was intentionally changed.
- This release is focused on lifecycle reliability, maintainability, and shutdown safety.

## [0.2.14.5] - 2026-08-06

### Added
- Guided Repair Center status banner, world scan action, repair candidate summary cards, and selection controls.
- Read-only repair-plan workflow with candidate, selected, and high-risk counts.

### Changed
- World Inspector is now the single navigation entry point for world validation and repair workflows.
- Removed the redundant World Validator item from the left navigation.
- Repair Center uses shared MystTiq dark-theme cards, semantic buttons, tooltips, and compact spacing.

## [0.2.14.3] - 2026-08-06

### Added
- Central application dialog facade and expanded dialog service.
- Shared MystTiq button density variants for standard, compact, toolbar, wide, icon, and DataGrid actions.
- Formal UI standards documentation under `release-notes`.

### Changed
- Existing application dialogs now route through one central integration point while preserving current behavior.
- Historical apply notes, build/test plans, and compile hotfix notes now live under `release-notes`.
- Environment DataGrid actions now use the shared DataGrid button standard.

## [0.2.14.2] - 2026-08-05

### Added
- Central player-save discovery and validation service.
- Guarded filesystem enumeration service.
- Ordered startup coordinator with independent stage results.

### Changed
- Players, Guilds, World discovery, World Tools, and Player Recovery now share the same player-save rules.
- Volatile Palworld filesystem scans now tolerate files and folders changing during enumeration.


## [0.2.14.7] - 2026-08-06

### Added
- Diagnostics Center under Tools with read-only application, workspace, server, world, backup, MOD, transaction, and notification checks.
- Provider-based `DiagnosticsService` architecture for registering subsystem health checks.
- Weighted health score with passed, warning, and failed totals.
- JSON and text diagnostics report export.
- Redacted support-package ZIP containing diagnostics, selected configuration metadata, and recent size-limited logs.

### Changed
- Diagnostics and support artifacts are stored in the centralized diagnostics directory for installed and portable modes.
- Transaction Center read-only wording no longer embeds an obsolete version number.


## [0.2.14.6] - 2026-08-06

### Added
- Read-only Transaction Center inside World Inspector.
- Durable transaction and world-import journal discovery.
- Search, filtering, stage details, backup/report links, and rollback-availability reporting.

### Changed
- Transaction History foundation is now an active audit center.

### Safety
- Rollback execution remains disabled until the rollback framework is validated.
- Malformed transaction records are skipped and logged without interrupting history loading.

## [0.2.14.4] - 2026-08-06

### Added
- Dark-themed World Management migration wizard inside World Inspector.
- Progressive seven-step migration workflow with locked, active, and completed states.
- World Inspector tabs for Players, Guilds, Bases, World Validator, World Management, Repair Center, and Transaction History.
- Guided status banner explaining the current migration state and next required action.
- Sidebar World Validator shortcut now opens the validator inside World Inspector.

### Changed
- World Validator is now organized as a World Inspector tab rather than a separate working page.
- Existing repair preview is presented as Repair Center.
- Player, Guild, and Base tabs link to their full management pages while preserving one World Inspector workspace.
- Locked migration stages explain what must be completed before they become available.

## [0.2.14.1] - 2026-08-05

### Added
- Dedicated Workspace Manager page.
- Portable/installed mode visibility and path inspection.
- Workspace validation, folder-opening, browsing, and synchronized path saving.


## [0.2.13.2] - 2026-08-05

### Added
- Portable workspace and application path foundation.
- Automatic PalServer and SteamCMD discovery inside the portable workspace.
- Portable-local settings, logs, cache, notifications, diagnostics, and window state.

### Changed
- Portable packages now include a working directory layout and marker file.
- Installer builds now use non-portable published files.

## [0.2.13.1] - 2026-08-04

### Added
- Central version definition in `Directory.Build.props`.
- Runtime application-version service.
- Shared version reader for build and packaging scripts.
- Release-tag version validation.

### Changed
- Window title, sidebar, user agents, exports, packaging, and installer builds now consume the central version.

## [0.2.12] - 2026-08-04

### Added
- First public open-source baseline.
- Professional one-page operations dashboard.
- Compact CPU and RAM history graphs.
- Live activity ticker and notification center.
- Separate operational-state and overall-health reporting.
- Health breakdown tooltip.
- Standardized MystTiq dark-theme buttons and tooltips.
- Rounded transparent application icon.

### Fixed
- Intentionally stopped servers no longer report poor health solely because they are stopped.
- Live-save temporary files no longer interrupt player and world inspection.
- Notification bell now toggles correctly.
- Clearing the final notification closes the flyout and hides the bell immediately.

## [0.2.14.1 FIX1]
### Fixed
- Backup Center now handles Palworld-exclusive live save locks with an optional coordinated stop-backup-start workflow instead of failing after repeated copy attempts.

## v0.2.14.1 FIX2

### Fixed
- Dashboard guild/base totals now initialize on first launch.
- Players, Guilds, Bases, and guild/base recovery snapshots are preloaded automatically after startup.
- Startup world-data stages fail independently so one unavailable subsystem does not prevent the rest of the application from loading.


## v0.2.14.11
- Finalized Update Center polish
- Admin Commands refresh reliability improvements
- Scroll routing audit
- Documentation and release wrap-up completed

## v0.2.15.1

### Added
- Central `Ue4ssRuntimeResolver` as the authoritative source for UE4SS runtime and MOD-root paths.
- `Ue4ssRuntimeInfo` snapshot model for modern, legacy, active, and runtime-reported MOD roots.
- UE4SS.log parsing for `Loading mods from:` runtime verification.
- Session-cached resolution with explicit refresh and invalidation support.
- Startup diagnostics that log the selected active root, detection method, runtime-reported root, and health state.

### Changed
- v0.2.15.x development now begins from the validated v0.2.14.11 baseline.
- The resolver prefers an existing modern `Win64\ue4ss\Mods` layout, can use UE4SS runtime-log evidence, retains legacy `Win64\Mods` compatibility, and never selects the legacy root merely because it contains more mods.

### Safety
- Phase 1 performs detection and diagnostics only. It does not migrate, move, delete, enable, disable, or rewrite existing MOD files.
## v0.2.15.2

### Changed
- Migrated `ModService` and `ModScannerService` UE4SS runtime operations to `Ue4ssRuntimeResolver.GetActiveModsRoot()`.
- MOD inventory now enumerates the active UE4SS Mods Root rather than assuming `Win64\Mods`.
- ZIP install planning for recognized UE4SS Lua/DLL packages now targets the active runtime root.
- Enable/disable, `mods.txt`, `enabled.txt`, state repair, managed-path detection, and delete operations use the same resolved active root.
- MOD Dashboard folder actions use the resolver-selected active root.
- Server Doctor and UE4SS dependency checks no longer treat a legacy Mods directory alone as proof of the active runtime.

### Safety
- No automatic legacy-to-modern migration or deletion is performed in this phase. Legacy content remains untouched for Phase 3 migration support.
- Existing managed manifests can still recognize both modern and legacy UE4SS paths for identification/migration purposes, while live operations target only the active runtime root.


## v0.2.15.6

### Pre-start MOD Reconciliation & Runtime Health Hardening
- Added `ModLifecycleCoordinator` as the authoritative boundary for normal modded PalServer startup.
- Pre-start reconciliation now repairs UE4SS `enabled.txt` overrides, preserves/creates canonical `mods.txt` entries, and immediately rescans effective MOD state.
- Added a startup health gate that blocks normal modded startup when enabled files are missing, an enabled UE4SS MOD is outside the resolver-selected Active Mods Root, a state mismatch remains, duplicate logical installs are detected, or reconciliation returns filesystem/state warnings.
- Preserved **Start Without MODs** as an intentional health-gate bypass for recovery and isolation testing.
- Added `ModRepairRecommendationEngine` for deterministic operator-facing repair guidance.
- Added TXT + JSON MOD verification report export from the MOD Dashboard using existing MystTiq button/tooltip/dark-theme standards.
- Updated versioning, README roadmap, release checklist, release notes, build/test plan, apply instructions, and source manifest for v0.2.15.6.

## v0.2.15.5

### Centralized MOD Health & Identity
- Added one authoritative `ModHealthEvaluationService` used by verification and UI health presentation.
- UE4SS/Lua mods are no longer reported Healthy without matching runtime load evidence while the server is running.
- Added explicit Runtime Unverified and Misconfigured health states.
- PAK/Workshop mods use installation/enabled verification and do not require UE4SS Lua load evidence.
- Main Dashboard healthy counts now include only genuinely Healthy MODs.
- Workshop display names are resolved from MystTiq's metadata cache during inventory scans so rescans preserve names such as `PalSchema (3625280368)`.
- MOD verification summaries now distinguish healthy, runtime-unverified, misconfigured/attention, disabled, failed/missing, and unknown states.

## v0.2.15.4

### Runtime-Loaded Status & Expanded UE4SS Diagnostics
- Added active-runtime presence and UE4SS-loaded state to managed MOD inventory rows.
- Parse the latest UE4SS runtime log for `Starting Lua mod` evidence.
- UE4SS/Lua mods that are not present under the resolver-selected Active Mods Root now report `Misconfigured` rather than healthy/installed.
- Expanded MOD Runtime diagnostics with UE4SS root, Active Mods Root, Legacy Mods Root, runtime-reported root, path health, active/legacy directory counts, and loaded Lua-mod count.
- Preserved v0.2.15.3 migration semantics; no automatic migration or destructive legacy cleanup was added.

## v0.2.15.3

### UE4SS Legacy Migration & ZIP Normalization — Phase 3
- Added safe copy-first migration from the legacy `Win64\Mods` directory into the resolver-selected Active Mods Root.
- Legacy copies are retained; migration never performs destructive moves or deletes.
- Existing active-root files are preserved when their content differs, and conflicts are reported instead of overwritten.
- Known UE4SS runtime-component folders are skipped so legacy runtime components cannot replace the active UE4SS installation.
- Added a MOD Dashboard migration action using existing MystTiq warning-button and tooltip standards.
- Normalized `ue4ss\Mods\<Mod>`, `Mods\<Mod>`, and wrapped `<Mod>\Scripts\...` archive layouts into `<ActiveModsRoot>\<Mod>\...`.
- Removed the remaining generic ZIP deployment path that could recreate `Win64\Mods` for UE4SS package content.
### v0.2.15.3 FIX1
- Changed the Windows installer default directory for fresh installs to `C:\GameServers\MystTiqPalworldServer`.
- Installer and post-install launch now run elevated; the application itself continues to request administrator privileges on subsequent launches.
- Updated README installer guidance and executable naming.


- Corrected the v0.4.4.1 platform-preservation test so cross-platform `System.Windows.Input.ICommand` usage is not misidentified as WPF.
- Added the missing v0.4.4.1 Linux acceptance and production-readiness scripts required by Linux packaging.
- Windows Avalonia desktop publish now launches the GUI automatically after a successful publish; `-NoGuiLaunch` suppresses launch for CI/release automation.

- v0.4.6.1 connection contract fix: desktop lifecycle DTO now accepts nullable `lastTransitionAt`, matching the headless/Core wire model so a stopped server can establish the management session before its first lifecycle transition.
## v0.4.12.0 — Backup Center Parity

- Adds deep persisted/audited backup verification, explicit restore confirmation, immutable server-side retention preview/apply, and profile-aware backup-root behavior.
- Preserves safety-backup rollback, remote/LAN security, one aggregate status poll, Windows/Linux Avalonia parity, card containment, and centered metallic buttons.
## v0.4.13.0 — MOD Dashboard / MOD Library / UE4SS Parity

- Adds staged, bounded, traversal-safe MOD ZIP installation plus audited selected delete, bulk state, and repair routes.
- Preserves server-side evidence, neutral Disabled/Active-Unverified health, and explicit capability truth.
## v0.4.14.0 — World Inspector Read-only Parity

- Restores ten read-only world evidence sections with headless statistics/integrity metadata, canonical discovery, and remote-safe presentation.
## v0.4.15.0 — World Validator, Recovery & Transaction Center

- Added structural world validation and local report export through the headless API.
- Added bounded, traversal-safe archive Analyze and expiring single-use review plans.
- Added stopped-server, confirmation-gated full-world import and canonical player-save recovery.
- Enforced fresh safety backup, isolated staging, atomic swap, post-validation, rollback, durable journal/audit and GUI refresh.
- Added test-only gated failure injection for every transaction stage; production hosts ignore the test header.
- Kept guild/base binary mutations visibly BACKEND REQUIRED pending a safe Palworld save codec.
## v0.4.16.0 — Activity & Audit + Notifications Parity

- Added Activity/Audit search, severity/category filters and visible-view export.
- Kept persistent audit append-only from the GUI with no silent clear endpoint.
- Added bounded atomic server-side notification persistence and audited read/pin/dismiss/mark-all/self-test routes.
- Added notification badge/page, filters, selection actions and local export without another periodic poll.

## v0.4.17.0 — Crash Analyzer + Palworld Save Tools Parity

- Added bounded evidence-backed crash signature analysis with durable server-side history.
- Added non-destructive isolation guidance that does not claim unproven causes.
- Added on-demand Python, legacy/PlM converter, Oodle and active-save diagnostics.
- Added bounded remote-safe save inventory and read-only signature inspection.
- Preserved single polling, authenticated remote/LAN access, card containment and the protected World Transaction mutation boundary.

## v0.4.17.1 — Documentation Gate Correction

- Synchronized `docs/index.html` and current-version release references after strict validation rejected the v0.4.17.0 candidate.
- No runtime behavior changed from the v0.4.17.0 feature candidate.

## v0.4.17.2 — Promotion-State Gate Correction

- Keeps the roadmap status as Current Release Candidate until the installed-tree gate completes.
- Carries the v0.4.17.1 documentation-index correction and all v0.4.17.0 feature behavior unchanged.

## v0.4.17.3 — Clean/Updater DLL-Release Retry

- Waits for the verified artifact-hosted desktop and sidecar to exit, then retries deletion of only resolved `artifacts`, `bin`, and `obj` directories when Windows briefly retains a DLL mapping.
- Refuses retry deletion for paths outside the project root or for unrecognized directory names.

## v0.4.17.4 — Installed-Workspace Shutdown Guard

- Updater closes MystTiq desktop/sidecar processes from the exact installation target, including development `bin` launches, before invoking clean and replacement.
- Process ownership is constrained by both executable name and a resolved target-root path prefix, with repeated sweeps and exit waits.
