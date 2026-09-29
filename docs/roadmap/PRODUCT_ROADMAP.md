<!-- MystTiq v0.9.7.0: file reviewed for this release (2026-09-29). -->
# Product roadmap to v1.0

Updated 2026-09-28. **Current version: v0.9.7.0. Accepted baseline: v0.8.25.0. Next: v0.9.x acceptance.**

This is the active plan. Version assignments after v0.9.7.0 are proposed milestone buckets, not dated commitments. Older planning and completed work are retained in [the historical roadmap](../history/PRODUCT_ROADMAP_through_v0.8.25.0.md), the [changelog](../../CHANGELOG.md) and [release notes](../../release-notes/). Historical “planned” and “not done” statements may have been superseded.

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

## v0.9.5.0 — upgrade and recovery (delivered 2026-09-28)

- **Servers no longer start each other.** When any PalServer process started or exited, every other profile on the machine logged "server crashed, restarting" and started its own server, even one stopped on purpose: a process whose path could not be read (starting or exiting) counted as every profile's own. Paths are now read by image name, which works for the whole life of a process, and an unreadable one belongs to a server only when that server started it (Windows and Linux).
- **Readiness waits for the right port.** It waited for PalWorldSettings.ini's PublicPort, which only advertises; the server binds its `-port=` argument (8211 without one). Two profiles launched with `-port=8211` whose ini said 8219 and 8213 could never become ready.
- **The Palworld server's update check tells the truth.** It compares the installed build with Steam's public build from SteamCMD (`+app_info_print 2394010`, cached 15 minutes); the Steam Web API's `UpToDateCheck`, used before, compares against a minimum version and always said "Up to date". The Update Center has a PalDefender row (installed DLL version against its latest GitHub release, and PalDefender's own "not updated for this game version" warning). The Doctor warns when the server is behind Steam and when PalDefender reports that warning, so both reach the Dashboard's health line.
- **A refused manifest no longer stops an update.** When Steam refuses the installed build's manifest ("Access Denied"), MystTiq moves the app manifest aside and checks every file against the new build instead, keeping the old manifest (or putting it back if that fails too), and a failure names SteamCMD's reason instead of "exit code 8".
- **Upgrade and recovery tested.** `scripts/Test-v0.9.5.0-Upgrade.ps1`: the accepted baseline (v0.8.25.0) takes settings and data, this version keeps every value, the baseline's backup verifies and a restore is byte for byte, rolling back to v0.8.25.0 still reads everything, and a fresh setup starts. `scripts/Test-v0.9.5.0-FleetRecovery.ps1`: two servers in one service; restarting or killing one never starts the other.
- Recorded checks: the logic harness pins the unreadable-process rule, the expected port, SteamCMD's app info and failures, and PalDefender's warning; live on this machine, stopping and starting the real clone server under the new build started no other server and logged no crash, the Update Center reported the main server one build behind (25080279 against 25247047) and the clone current, and the Doctor warned on the main server. The fleet smoke also passes on the old build (the stand-in server's path is readable at once), so the logic harness and the live run are the evidence for the fix. Not yet recorded live: an update from MystTiq that meets a refused manifest (the clone was updated by hand before this version).
- Still open: accounts in the upgrade test (it ran without sign-in); a notification (not only a Doctor warning) when a component falls behind; linking a server exit to the player join just before it; UE4SS's installed release is still compared by hand.

**Exit evidence met:** a scripted upgrade from v0.8.25.0 and a fresh setup with every value compared, and a server one build behind reported as out of date. **Still to record:** an update from MystTiq through a refused manifest, and a scripted upgrade with accounts.

## v0.9.6.0 — firewall rule for the server's port, and a fast server search (delivered 2026-09-28)

Requested 2026-09-28, after a LAN join to the clone server needed a firewall rule typed by hand.

- **Firewall rule for each server's own port, where the port is set.** Diagnostics, Settings (beside the launch arguments, where `-port=` is set) and the new-server wizard's last step show whether Windows Firewall lets players reach the port the server binds, with **Allow through Firewall**; Fleet's Clone World card reminds you that a clone needs its own rule. The rule is `MystTiq Palworld Server - Game UDP <port>`, tagged `MystTiq server: <id>`; allowing a port removes that server's rules for any other port, so a port change closes the old one (rules from before this version carry no id and are left alone). When Windows refuses the service (not an administrator), the Desktop runs the same script through Windows' administrator prompt for a server on this computer. The Doctor has a `network-firewall` finding with an `allow-firewall` fix. Linux: the card and the finding show the `ufw` and `firewalld` commands.
  - Fixed first: the check started a bare `powershell.exe`, which this machine's PATH could not find, so it never ran. Rules are now read through the firewall's COM API (about 0.2 s against 27 s for `Get-NetFirewallPortFilter`), and changes go through Windows PowerShell by its full path.
  - A rule counts only when it can reach the server: not one limited to another program (the Palworld game client, Steam), a service, or Store apps (an owner or package: Xbox, ChatGPT on this machine), and not one for the `PalServer.exe` launcher, whose child process owns the port. Before, any "Any port" rule read as allowed.
- **A second server gets its own port.** The wizard copied the first server's launch arguments, `-port=` included, so a second server used the first one's port (or 8211 without a `-port=`), whatever port it was given. It now gets `-port=` with its own port, and the Doctor's `configuration-game-port` finding warns when a server binds one port and advertises another.
- **Fast server search.** A TCP connect sweep, 256 addresses at a time with a 600 ms timeout, then the `/healthz` probe only where the port answers: 255 addresses in about 0.6 s in the harness. It shows the ranges it searches and its progress, lists each service as it answers, and has Cancel; the window stays usable. Each subnet is searched once; virtual adapters (WSL, Hyper-V, Docker, VMware, VirtualBox, libvirt) without a default gateway are listed as not searched unless ticked (a Hyper-V external switch carrying the real LAN, as on this machine, is searched); typed ranges (single addresses or `/24` to `/32`) are added.
- **A stop is never taken for a crash.** Found by this version's gate (the v0.9.5.0 fleet smoke, about one run in three): the process inspector dropped a terminating process whose details could not be read, so a stop recorded "Stopped" while the process was still exiting, the next read listed it as "Running", and the supervisor then restarted it as crashed. A process is now listed until Windows reports it has exited and never after, status reads and a stop's state writes are serialised, and a read during a stop keeps the stop request (Windows and Linux); the lifecycle state file retries a replace that something holding it open refused.
- Recorded checks: the logic harness pins rule matching (with the rules found on this machine), the tagged allow script and its cleanup, the rule state, a real read of this computer's firewall, ranges, virtual adapters and typed ranges, a timed search with a stand-in service, Cancel, and a second server's arguments. `scripts/Test-v0.9.6.0-FirewallRoute.ps1`: two servers in one service, one advertising a port it does not bind; the route and the Doctor report the bound port, and only GET routes are called. The ArtworkHarness checks the 44 new texts in all 12 languages.
- Still open: counting a rule only when it covers the network profile in use (a Private-only rule reads as allowed on a Public network); adding a rule live (it changes this computer's security settings, so the owner clicks it and confirms Windows' prompt), then changing the port and allowing again, and a join from another PC through the rule; searching more than the `/24` around an address on a wider network; changing a mismatched `-port=` from the Doctor (launch arguments apply when MystTiq restarts).

**Exit evidence met:** the firewall state for the port each server binds, read in well under a second; a search of 255 addresses in under a second that lists its ranges, its progress and the service it found. **Still to record:** a rule added and then updated after a port change, with elevation, and a join from another PC through it.

## v0.9.7.0 — Avalonia 12 (delivered 2026-09-29)

- **Avalonia 11.3 → 12.1.3**, all four packages together (Avalonia, Avalonia.Desktop, Avalonia.Fonts.Inter, Avalonia.Themes.Fluent) and `Avalonia.Headless` 12.1.3 in the ArtworkHarness and RemoteSignInHarness, pinned to that exact version; Dependabot already groups the Avalonia packages (v0.8.26.0), so its split pull requests (#15–#17) cannot recur.
- What the new version needed: the clipboard's text methods moved to extensions in `Avalonia.Input.Platform` (`GetTextAsync` became `TryGetTextAsync`); `TextBox.Watermark` became `PlaceholderText` (62 text boxes); `Window.SystemDecorations` became `WindowDecorations`; Avalonia 12 has its own `NavigationPage` control, so the harnesses alias the app's.
- Recorded checks: the desktop builds with no new warnings; the ArtworkHarness passes in all 12 languages on Avalonia 12, and its page renders match 11.3's apart from an undersized (950×650) window, which now fits its content (the ribbon folds into "»") where 11.3 ran off the edge.
- Still open: the real Linux desktop session on the test VM (window, maximize and restore, clipboard, tray), which needs the test user logged in to the VM's desktop.

**Exit evidence met:** a clean Windows build, both harnesses building and the ArtworkHarness passing, the full release gate, and every page compared in each appearance mode. **Still to record:** the Linux desktop session on Avalonia 12.

## v0.9.x — integration and release stabilization

These are remaining checks or targeted fixes, not a request to rebuild shipped features.

| Work | Acceptance evidence / dependency |
| --- | --- |
| Give Item/Pal and starter kits | 2026-09-28, clone server on game v1.0.5 with PalDefender v1.9.2: `scripts/Test-v0.8.26.0-InGame.ps1` passed 8/8 with a real player online; PalDefender gave "1 Wood" over RCON and the player confirmed it arrived. Still to record: a Pal delivery, a starter kit, and a refused give (wrong id, offline player) |
| Live map and teleport points | 2026-09-28: one paired position recorded. REST world X −362179.44, Y 270846.47 and PalDefender map 245.85, −519.15 match map X = (world Y − 158000) / 459 and map Y = (world X + 123888) / 459 to two decimals, and a second pair about 22 map units away (world X −351943.75, Y 270371.38; map 244.82, −496.85) matches too. A live `tp` of 4.36 map units moved the player 2002 world units (about 20 m), as the player confirmed: `tp` takes the same map coordinates `getpos` prints, as MystTiq assumes. Unexplained: the player was logged out about 40 s after the in-game script's own teleport (no PalDefender kick, no server exit). Still to verify: the player marker live on the map, a chat-triggered teleport, and point placement on the map |
| Notifications | Observe real Discord and email delivery, pause/resume, recovery and failure handling. `scripts/Test-v0.8.26.0-Alerts.ps1` sends a real test through every switched-on channel and reports failed sends; confirm arrival in each channel |
| Linux desktop | Window, maximize/restore, clipboard and tray pass on the XFCE test VM (`scripts/Test-v0.8.26.0-LinuxDesktopSession.ps1`, 16/16). Still open: file pickers, scaling and other desktop environments |
| Linux service priority | Install the new unit in a test environment and verify eco-to-normal priority recovery; unit syntax/headless checks alone are insufficient |
| Permissions and accessibility | Delivered in v0.9.4.0 (above); a real screen-reader pass remains |
| Themes | Verify supported modes throughout the app; with a Windows contrast theme on, run `scripts/Test-v0.8.26.0-ContrastTheme.ps1`; document Linux native contrast limitations |
| Upgrade and recovery | Delivered in v0.9.5.0 (above); accounts in the upgrade test and an update through a refused manifest remain |
| Distribution | Build the current desktop with its matching headless sidecar, include the Windows native helper, verify clean-machine launch, source parity, version identity and SHA-256 checksums |
| Documentation | Keep README, site, release notes and supported-platform claims aligned with observed results; publish known limitations |
| Crash analysis | Add signatures only from real anonymized reports; do not invent coverage for unseen crashes |
| Avalonia 12 migration | Delivered in v0.9.7.0 (above); the Linux desktop session on Avalonia 12 remains |

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
