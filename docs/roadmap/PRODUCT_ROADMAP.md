<!-- MystTiq v0.9.4.0: file reviewed for this release (2026-09-28). -->
# Product roadmap to v1.0

Updated 2026-09-28. **Current version: v0.9.4.0. Accepted baseline: v0.8.25.0. Next: v0.9.5.0.**

This is the active plan. Version assignments after v0.9.4.0 are proposed milestone buckets, not dated commitments. Older planning and completed work are retained in [the historical roadmap](../history/PRODUCT_ROADMAP_through_v0.8.25.0.md), the [changelog](../../CHANGELOG.md) and [release notes](../../release-notes/). Historical “planned” and “not done” statements may have been superseded.

## Foundation delivered through v0.8.26.0

| Area | Delivered |
| --- | --- |
| Architecture and operations | Shared Core, authoritative headless service, Avalonia desktop, fleet isolation, service management, persisted recovery/history and readiness checks |
| Artwork | Distinct navigation/Ribbon icons, category day/night art, HOST art/icon and corrected header layout |
| Appearance | Dark, Light, Midnight, High contrast and Follow system; density; mode-aware decorative colours and shadows; Windows contrast themes |
| Access | Named accounts, scoped route roles, role-aware cards/Ribbon/page commands; four-role remote sign-in and headless Linux desktop harness verification |
| Host controls | Seven-day CPU/memory/upload history, process priority, eco mode, processor affinity and game-level bandwidth settings |
| Linux service policy | Generated unit uses `LimitNICE=-11`; policy is applied when MystTiq starts the server. The older `CAP_SYS_NICE` / `Nice=` plan is superseded |
| Community and integrations | Item/Pal catalogue and picker, kits, teleport-point commands, delivery-route pause, mods/Nexus and alerts; live verification gaps remain below |
| Repository and verification (v0.8.26.0) | Legacy WPF app, installer and ~1000 obsolete files removed; every file reviewed; Windows and Linux packages; live acceptance scripts for the Linux desktop session, in-game give/teleport, alert delivery and Windows contrast themes |
| Translation foundation | English fallback and German/Spanish coverage for navigation, headers, Ribbon and Dashboard labels; not a completed translation |

## v0.9.0.0 — the window in 12 languages (delivered 2026-09-27)

- Every hard-coded XAML text (labels, buttons, tips, menus, accessible names, the tray menu; 998 texts) is a key in `Assets/i18n/en.json`, made with `scripts/Update-MystTiqUiText.ps1`, which also writes the text list (`docs/i18n/UI_TEXT_INVENTORY.md` and `.csv`) and lets the release gate fail on any new hard-coded text.
- Languages, chosen by use on PCs and among Palworld's players: English, 简体中文, Español, Português (Brasil), Русский, Deutsch, Français, 日本語, 한국어, Italiano, Polski, Türkçe. Catalog coverage is present in all 12 languages; the 11 non-English translations are drafts awaiting native review. Coverage does not establish translation quality.
- Chinese, Japanese and Korean use each language's own system font (Windows, then Noto CJK on Linux), so Japanese is not drawn with Chinese glyph shapes.
- The language can be picked in the title bar (right of Settings) as well as in Settings; the Notifications button is a bell.
- Checked offline in every language: the category tabs fit at 950x650, no Ribbon button is cut off, no Dashboard label splits or is cut off, and no page shows an untranslated English label.

## v0.9.1.0 — status and error messages (delivered 2026-09-28)

- The app's own status values, progress, results, validation and error messages, drop-down choices, dialogs and file-picker titles (1,113 messages, the `msg.*` keys) are translated as they are shown, so the app's logic keeps working with the English values. A message with values ("Restarted. '{0}' is now online.") is matched as a template; the values (names, numbers, times) are kept, and translated too when they are themselves a message.
- 481 bound texts show through the catalog (`{services:TrText}`); editable fields never do. Plain text in drop-downs and lists goes through a String data template.
- Recorded offline checks cover message-template round-trips and translated page text in every language. These checks do not establish coverage of service replies or all composed messages. Data stays English: server names, the in-game restart command, date formats and place names.

## v0.9.2.0 — the game's names in the chosen language (delivered 2026-09-28)

- Item and Pal names in the Give Item picker, kits and on the map come in the Desktop's display language, from the installed game's own name tables (the game ships all 12: Japanese is its source language, the rest are its localisations). The service reads and caches each language separately and falls back to English, and says so, when a language's table is missing.
- Texts the Desktop composes from several sentences (the map's summary line, Pal tooltips) are translated sentence by sentence, and the host's uptime is translated.
- Recorded checks cover the per-language name cache, the English fallback and composed-text translation offline, and live reads of the installed game's Japanese, Korean and English names on a test server. Names come from the game and are outside the native review.

## v0.9.3.0 — the service's own messages and review aids (delivered 2026-09-28)

- The MystTiq service's own messages (Doctor findings, operation results, crash explanations, network and router checks, recovery steps, validation errors; 1,339 texts), which arrive in English from the server, are translated when shown like the Desktop's own. Commands sent to the game, log patterns, protocol text and the names Windows shows stay English on purpose.
- `scripts/Export-MystTiqTranslationReview.ps1` writes a review sheet per language and [`docs/i18n/TRANSLATION_REVIEW.md`](../i18n/TRANSLATION_REVIEW.md) is the reviewers' checklist and coverage table.
- Recorded offline checks cover the round-trip of all 2,492 message templates in every language; a live check showed service messages in Japanese. Catalog coverage does not establish translation quality, and text the service passes on from Windows, Linux or the game stays as it arrives.
- Still open, and needing people: every language is **awaiting native review** (meaning, terminology, plurals, length, accessibility names), and dates and numbers are still one fixed format for every language.

**Exit evidence for the review:** each language's sheet returned with corrections applied, reviewed language samples, and a look at every page in each advertised language.

## v0.9.4.0 — roles explained, and accessibility (delivered 2026-09-28)

- A control the signed-in role cannot use says which role it needs, in its tooltip (shown while disabled) and to screen readers, on every page; the Ribbon already did.
- Every list, text box, drop-down, number box, slider and check box has an accessible name (about 150 added), and every enabled control can be reached with Tab; the release gate fails on a new unnamed control.
- Recorded automated checks cover the role hints for every role on every page, accessible names and Tab reach. They do not replace a pass with a real screen reader.
- The health states and values inside translated labels follow the language.
- Still open, and needing people: a pass with a real screen reader (Narrator, Orca), and focus order and scaling checked on every page.

## v0.9.5.0 — upgrade and recovery

- Test a fresh setup, an upgrade from the accepted baseline (v0.8.25.0) with its settings, accounts and data kept, backup and restore, and rollback, all on isolated data.

- **Palworld server updates (found 2026-09-28, when a v1.0.5 game could not join a v1.0.4 server):**
  - The Update Center said "Up to date" for build 25080279 while Steam's public build was 25247047. Steam's `ISteamApps/UpToDateCheck` compares against a developer-set minimum version (1000 for this app), not the build, so it can never report an update. Read the public branch's build from SteamCMD (`+app_info_print 2394010`) instead, and show both builds.
  - Update information is fetched live, not assumed: the game server's public build, PalDefender (github.com/Ultimeit/PalDefender releases; v1.8.3 was installed while v1.9.2 was out) and UE4SS are each compared with their latest release in the Update Center, checked on a schedule and after every game update, with a notification when one is behind.
  - After a game update, check the add-ons that hook the game before players join: PalDefender v1.8.3 loaded on game v1.0.5 with "hasn't yet updated to the latest version of the game", and the server then died about 40 seconds after each player joined, with no crash report and nothing in the Windows event log (the Crash Analyzer found nothing new). PalDefender v1.9.2 loaded cleanly. MystTiq should flag that warning, link each exit to the join just before it ("stopped 40 s after a player joined"), and offer to update the add-on.
  - A stopped server was restarted as "crashed" when the running server exited: `frostbound-frontier-8886` (not running earlier that day) logged a crash one second after the clone's first exit and began restart attempts. Its profile expects UDP 8219 while its server listened on 8211, so it never counted as ready and kept retrying. Find why one server's exit read as another's crash, check that recovery and clean-up only ever touch their own server's processes (by path and PID), and report a port mismatch between a profile and its server instead of retrying.
  - The SteamCMD update failed twice with exit code 8: Steam refused the installed build's manifest ("Failed to get manifest request code, 'Access Denied'"). It worked once the app manifest was moved aside, so SteamCMD validated the files against the new build instead. MystTiq should recognise that failure, retry that way on its own (keeping the old manifest), and show SteamCMD's actual reason instead of "exit code 8".

**Exit evidence:** a scripted run from a clean machine state and from a v0.8.25.0 install, with every setting, account and world compared before and after; a server one build behind reported as out of date and updated from MystTiq without manual steps.

## v0.9.6.0 — firewall rule for the server's port, and a fast server search

Requested 2026-09-28, after a LAN join to the clone server needed a firewall rule typed by hand.

- **Firewall rule for the current server's port.** Offer "Allow through Windows Firewall" where the server's game port is set (Configuration, the new-server wizard, Fleet clone), not only in Diagnostics. It adds or repairs one inbound UDP rule for that server's own port (`MystTiq Palworld Server - Game UDP <port>`), asks for administrator rights, and follows a port change: the old rule is removed or updated. The rule is shown per server with its state (present, missing, disabled, wrong profile).
  - Fix first: the firewall check and repair start plain `powershell.exe`, which the service could not find on this machine ("cannot find the file specified"). Resolve it by its full `%SystemRoot%` path, or use the firewall API directly, and report a failure that names the cause.
  - Linux: say which command to run (ufw/firewalld) rather than changing the firewall, as today.
- **Fast server search.** Scan for MystTiq services with many parallel probes (a bounded pool of about 128–256, a short connect timeout before the `/healthz` request) and show each result as it answers, with a Cancel button.
  - Show the IP ranges being scanned (for example "192.168.1.0/24 on Ethernet 2 · 172.20.64.0/24 on vEthernet (WSL)") and the progress (addresses probed / total, found so far).
  - Scan each subnet once when several adapters share it, and skip virtual adapters (WSL, Hyper-V internal) unless chosen. Let the user add or remove a range.

**Exit evidence:** a rule added and then updated after a port change, on Windows with elevation, and a join from another PC through it; a search of two /24 ranges that finishes in a few seconds, listing its ranges, its progress and each service as found.

## v0.9.x — integration and release stabilization

These are remaining checks or targeted fixes, not a request to rebuild shipped features.

| Work | Acceptance evidence / dependency |
| --- | --- |
| Give Item/Pal and starter kits | 2026-09-28, clone server on game v1.0.5 with PalDefender v1.9.2: `scripts/Test-v0.8.26.0-InGame.ps1` passed 8/8 with a real player online; PalDefender accepted "give 1 Wood" over RCON. Still to record: the player seeing the item arrive, a Pal delivery, a starter kit, and a refused give (wrong id, offline player) |
| Live map and teleport points | 2026-09-28: one paired position recorded. REST world X −362179.44, Y 270846.47 and PalDefender map 245.85, −519.15 match map X = (world Y − 158000) / 459 and map Y = (world X + 123888) / 459 to two decimals; confirm with a second, distant position. A teleport command was accepted. Still to verify: the player marker live on the map, a chat-triggered teleport, and point placement on the map |
| Notifications | Observe real Discord and email delivery, pause/resume, recovery and failure handling. `scripts/Test-v0.8.26.0-Alerts.ps1` sends a real test through every switched-on channel and reports failed sends; confirm arrival in each channel |
| Linux desktop | Window, maximize/restore, clipboard and tray pass on the XFCE test VM (`scripts/Test-v0.8.26.0-LinuxDesktopSession.ps1`, 16/16). Still open: file pickers, scaling and other desktop environments |
| Linux service priority | Install the new unit in a test environment and verify eco-to-normal priority recovery; unit syntax/headless checks alone are insufficient |
| Permissions and accessibility | Delivered in v0.9.4.0 (above); a real screen-reader pass remains |
| Themes | Verify supported modes throughout the app; with a Windows contrast theme on, run `scripts/Test-v0.8.26.0-ContrastTheme.ps1`; document Linux native contrast limitations |
| Upgrade and recovery | Planned as v0.9.5.0 (above) |
| Distribution | Build the current desktop with its matching headless sidecar, include the Windows native helper, verify clean-machine launch, source parity, version identity and SHA-256 checksums |
| Documentation | Keep README, site, release notes and supported-platform claims aligned with observed results; publish known limitations |
| Crash analysis | Add signatures only from real anonymized reports; do not invent coverage for unseen crashes |
| Avalonia 12 migration | Move the desktop from Avalonia 11.3 to 12.x as its own version, all four packages together (Avalonia, Avalonia.Desktop, Avalonia.Fonts.Inter, Avalonia.Themes.Fluent) plus `Avalonia.Headless` in the ArtworkHarness and RemoteSignInHarness. Dependabot's split pull requests (#15–#17, 2026-09-27) failed CI because each bumped only half of the set. Evidence: clean Windows and Linux builds, both harnesses and the full release gate passing, the real Linux desktop session, and a look at every page in each appearance mode. Then pin the version and have Dependabot group the Avalonia packages |

Use isolated test roots and disposable server data. Live verification needs the relevant test environment, account/channel or online player; missing evidence must remain explicitly open.

## v1.0 — stable release gate

Release v1.0 when all of the following are true:

- No unresolved blocker involving data loss, unauthorized operations, broken installation/upgrade or unrecoverable service state.
- Lifecycle, backup/restore, update, recovery and multi-server isolation pass on the supported platform matrix.
- Account/role enforcement agrees between client and server, including refusal paths and remote sessions.
- Advertised integrations have real end-to-end evidence; unsupported combinations and remaining limitations are visible.
- Translation and accessibility meet the advertised scope; supported Windows and Linux experiences have recorded acceptance results.
- Tagged source, distributed binaries and release notes agree; CI passes, checksums are available and rollback instructions are usable.

No release date is committed. Items that cannot meet their acceptance gate must be resolved or explicitly removed from the advertised v1.0 scope.

## Optional / later, not v1.0 commitments

Per-accent artwork, a separate night HOST illustration, OS-level traffic shaping, per-process network accounting and a browser administration UI. Additional language packs depend on translation review and font support. These ideas do not make already shipped v0.8 functionality incomplete.

## Current publication work

Publish v0.9.2.0 on GitHub with current source, the Windows package, release notes and checksums, following [the publishing guide](../release/README.md). A Linux package can be built locally (`Package-GitHubRelease.ps1 -Runtime linux-x64`) but stays unpublished until Linux desktop acceptance is complete. Keep public-release coordination (including any desired Nexus contact or asset permissions) separate from implementation status. No messages are sent on the user's behalf by this roadmap.
