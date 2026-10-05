<!-- MystTiq v1.0.1.0: file reviewed for this release (2026-10-05). -->
# Product roadmap to v1.0

Updated 2026-10-04. **Current version: v1.0.1.0. Accepted baseline: v1.0.0.0. Next: the open items below.**

This is the active plan. Version assignments after v1.0.0.0 are proposed milestone buckets, not dated commitments. Older planning and completed work are retained in [the historical roadmap](../history/PRODUCT_ROADMAP_through_v0.8.25.0.md), the [changelog](../../CHANGELOG.md) and [release notes](../../release-notes/). Historical “planned” and “not done” statements may have been superseded.

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
- Still open, and needing people: every language is **awaiting native review** (meaning, terminology, plurals, length, accessibility names). Numbers, dates and times follow the chosen language since v0.9.9.0 (English keeps the system's regional format). The v0.9.10.0 review fixes applied the outside review's label and wording notes; that was not a native review.

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
- Delivered later: accounts in the upgrade test and an alert when a component falls behind (v0.9.8.0); linking a server exit to the player join before it, and UE4SS compared automatically (v0.9.9.0).

**Exit evidence met:** a scripted upgrade from v0.8.25.0 and a fresh setup with every value compared, and a server one build behind reported as out of date. **Still to record:** an update from MystTiq through a refused manifest (a scripted upgrade with accounts: v0.9.8.0).

## v0.9.6.0 — firewall rule for the server's port, and a fast server search (delivered 2026-09-28)

Requested 2026-09-28, after a LAN join to the clone server needed a firewall rule typed by hand.

- **Firewall rule for each server's own port, where the port is set.** Diagnostics, Settings (beside the launch arguments, where `-port=` is set) and the new-server wizard's last step show whether Windows Firewall lets players reach the port the server binds, with **Allow through Firewall**; Fleet's Clone World card reminds you that a clone needs its own rule. The rule is `MystTiq Palworld Server - Game UDP <port>`, tagged `MystTiq server: <id>`; allowing a port removes that server's rules for any other port, so a port change closes the old one (rules from before this version carry no id and are left alone). When Windows refuses the service (not an administrator), the Desktop runs the same script through Windows' administrator prompt for a server on this computer. The Doctor has a `network-firewall` finding with an `allow-firewall` fix. Linux: the card and the finding show the `ufw` and `firewalld` commands.
  - Fixed first: the check started a bare `powershell.exe`, which this machine's PATH could not find, so it never ran. Rules are now read through the firewall's COM API (about 0.2 s against 27 s for `Get-NetFirewallPortFilter`), and changes go through Windows PowerShell by its full path.
  - A rule counts only when it can reach the server: not one limited to another program (the Palworld game client, Steam), a service, or Store apps (an owner or package: Xbox, ChatGPT on this machine), and not one for the `PalServer.exe` launcher, whose child process owns the port. Before, any "Any port" rule read as allowed.
- **A second server gets its own port.** The wizard copied the first server's launch arguments, `-port=` included, so a second server used the first one's port (or 8211 without a `-port=`), whatever port it was given. It now gets `-port=` with its own port, and the Doctor's `configuration-game-port` finding warns when a server binds one port and advertises another.
- **Fast server search.** A TCP connect sweep, 256 addresses at a time with a 600 ms timeout, then the `/healthz` probe only where the port answers: 255 addresses in about 0.6 s in the harness. It shows the ranges it searches and its progress, lists each service as it answers, and has Cancel; the window stays usable. Each subnet is searched once; virtual adapters (WSL, Hyper-V, Docker, VMware, VirtualBox, libvirt) without a default gateway are listed as not searched unless ticked (a Hyper-V external switch carrying the real LAN, as on this machine, is searched); typed ranges (single addresses or `/24` to `/32`) are added.
- **A stop is never taken for a crash.** Found by this version's gate (the v0.9.5.0 fleet smoke, about one run in three): the process inspector dropped a terminating process whose details could not be read, so a stop recorded "Stopped" while the process was still exiting, the next read listed it as "Running", and the supervisor then restarted it as crashed. A process is now listed until Windows reports it has exited and never after, status reads and a stop's state writes are serialised, and a read during a stop keeps the stop request (Windows and Linux); the lifecycle state file retries a replace that something holding it open refused.
- Recorded checks: the logic harness pins rule matching (with the rules found on this machine), the tagged allow script and its cleanup, the rule state, a real read of this computer's firewall, ranges, virtual adapters and typed ranges, a timed search with a stand-in service, Cancel, and a second server's arguments. `scripts/Test-v0.9.6.0-FirewallRoute.ps1`: two servers in one service, one advertising a port it does not bind; the route and the Doctor report the bound port, and only GET routes are called. The ArtworkHarness checks the 44 new texts in all 12 languages.
- Still open: adding a rule live (it changes this computer's security settings, so the owner clicks it and confirms Windows' prompt), then changing the port and allowing again, and a join from another PC through the rule. (The Doctor's fix for a mismatched port: v0.9.9.0.)

**Exit evidence met:** the firewall state for the port each server binds, read in well under a second; a search of 255 addresses in under a second that lists its ranges, its progress and the service it found. **Still to record:** a rule added and then updated after a port change, with elevation, and a join from another PC through it.

## v0.9.7.0 — Avalonia 12 (delivered 2026-09-29)

- **Avalonia 11.3 → 12.1.3**, all four packages together (Avalonia, Avalonia.Desktop, Avalonia.Fonts.Inter, Avalonia.Themes.Fluent) and `Avalonia.Headless` 12.1.3 in the ArtworkHarness and RemoteSignInHarness, pinned to that exact version; Dependabot already groups the Avalonia packages (v0.8.26.0), so its split pull requests (#15–#17) cannot recur.
- What the new version needed: the clipboard's text methods moved to extensions in `Avalonia.Input.Platform` (`GetTextAsync` became `TryGetTextAsync`); `TextBox.Watermark` became `PlaceholderText` (62 text boxes); `Window.SystemDecorations` became `WindowDecorations`; Avalonia 12 has its own `NavigationPage` control, so the harnesses alias the app's.
- Recorded checks: the desktop builds with no new warnings; the ArtworkHarness passes in all 12 languages on Avalonia 12, and its page renders match 11.3's apart from an undersized (950×650) window, which now fits its content (the ribbon folds into "»") where 11.3 ran off the edge.
- The real Linux desktop session on the test VM (window, maximize and restore, clipboard, tray) passed on Avalonia 12 in the v0.9.8.0 gate (2026-09-29), once the test user was logged in to the VM's desktop.

**Exit evidence met:** a clean Windows build, both harnesses building and the ArtworkHarness passing, the full release gate, and every page compared in each appearance mode. **Recorded in the v0.9.8.0 gate:** the Linux desktop session on Avalonia 12.

## v0.9.8.0 — small screens, alerts and Linux install (delivered 2026-09-29)

- **The "+" stays on small windows** (reported 2026-09-29). The title bar's fixed 430 px brand left the tab strip too little room at the minimum width, and the tabs, "»" and "+" were one panel clipped at its right edge. The "»" and "+" now have their own columns after the tabs (capped at the space left), and below 1200 px the brand drops its subtitle. The ArtworkHarness checks the "+" and the active tab at 950, 1100 and 1440 px.
- **Alerts when a component falls behind.** The Alert Center's "Game server or PalDefender out of date" rule (on by default) alerts on a game server behind Steam's public build and on PalDefender's "not updated for this game version" warning, with reminders and a Resolved notice (closes v0.9.5.0's open item).
- **Firewall rules and the network in use.** A rule counts only on the network profile(s) the computer is on; a Private-only rule on a Public network is reported as such (closes v0.9.6.0's open item).
- **Wider server search.** Each adapter's own subnet, capped at /22, instead of always the /24; typed ranges accept /22 (closes v0.9.6.0's open item).
- **Linux install with a desktop shortcut.** `scripts/Install-MystTiqDesktopLinux.ps1` (`.\Build.ps1 DeployDesktopLinux`) installs the Linux download in the user's home over SSH, with a trusted launcher on the desktop and in the applications menu; used on the test VM on 2026-09-29.
- **Accounts across an upgrade.** `scripts/Test-v0.9.8.0-UpgradeAccounts.ps1`: accounts, roles, a changed password and a disabled account from v0.8.25.0 to this version and back (closes v0.9.5.0's open item).

## v0.9.9.0 — one helper, crash causes and local formats (delivered 2026-09-29)

- **The desktop's local helper.** Found on the Linux VM (its port 8213 is held by an older installed service): the desktop started one more helper on every call and app start, all supervising the same servers, and stopping a helper killed its whole process tree, so the new-server wizard's restart ended running game servers. The helper the desktop started is now recorded and reused, and only the helper process is stopped; servers keep running and are adopted.
- **A server that stops when a player joins.** The `exit-after-join` crash signature, from the live case of 2026-09-28 (PalDefender v1.8.3 on game v1.0.5): a PalDefender session log that ends on a player connecting, the session over within 5 minutes (closes v0.9.5.0's open item).
- **UE4SS compared automatically.** Without a recorded install, the installed `UE4SS.dll` is compared by content with the three newest releases' downloads and names the release it is identical to (closes v0.9.5.0's open item). Live on the clone: it matches none of them, so it still reads "check manually", with the reason.
- **Alerts for UE4SS and MOD updates** (requested 2026-09-29). The out-of-date alert also covers a newer UE4SS release (from a cached check) and installed MODs with an update (named), alongside the game server and PalDefender.
- **The Doctor fixes a port mismatch.** Fix sets PublicPort to the port the server binds (closes v0.9.6.0's open item).
- **Numbers, dates and times per language.** Formats follow the language chosen in MystTiq, not the operating system's; text comparison and casing are unchanged (closes the v0.9.0.0 open item on formats; the native review of the texts themselves stays open).
- **Distribution.** `scripts/Test-v0.9.9.0-Distribution.ps1` checks the checksums, what each ZIP holds, the version on the binaries, and that the packaged service starts from a clean folder.
- Still open: a helper left running by an older version is reported, not stopped (MystTiq only stops helpers it started); UE4SS builds older than the three newest releases still read "check manually".

## v0.9.10.0 — review fixes (delivered 2026-09-30)

An outside review of the v0.9.9.0 source reported six findings; each is fixed and has a check. Details: `docs/architecture/v0.9.10.0-review-fixes.md`.

- **F1, helper ownership.** A live but slow helper is waited for and reused; one that cannot be used is replaced only after it has exited; a failed stop is reported. Proved with stand-in helpers in the ArtworkHarness.
- **F2, names.** Names inside translated messages stay as written (quoted, or after server/player/guild and the like); the Dashboard's server name and description are untranslated. Remaining: an unquoted name in another slot that equals a known status.
- **F3, MOD update state.** Unknown is no longer "up to date"; the MOD alert resolves only on checked evidence.
- **F4, prereleases.** The MystTiq update check reads the release list: 0.x counts prereleases, 1.0 and later stable only.
- **F5, crash evidence.** The join-crash finding no longer depends on when it is read, so it is recorded once and stays. This replaces v0.9.9.0's 5-minute window.
- **F6, archive consistency.** The checkpoint notes are stamped, and the source ZIP is checked by its own gate after it is made.
- Also: the Doctor's port finding allows for port forwarding; the review's translation notes (Pal-edit labels, plurals, German action names, "this app").

## v1.0.0.0 — published as the stable release (2026-09-30)

The owner published v0.9.10.0's code as MystTiq 1.0 and moved the open items and verification notes from the public
README, site and release notes to this roadmap and `docs/architecture/v1.0.0.0-stable-release.md`. The v1.0 gate below
was not fully met: native translation review, the remaining live integration checks, a screen-reader pass and Linux
acceptance stay open as post-1.0 work, tracked in the table that follows. The release workflow now makes full releases
from v1 on, which MystTiq's own update check requires.

v1.0.0.0 is the accepted baseline (owner, 2026-09-30), replacing v0.8.25.0. From the next version on, the frozen-checkpoint
regression and the upgrade and accounts smokes start from the v1.0.0.0 checkpoint (`_Backups/MystTiqPalworldServer/v1.0.0.0`)
as well as v0.8.25.0, so an upgrade from 1.0 is always tested.

## v1.0.0.4 — Restore checked by the world's day (2026-10-05)

Restores report the restored world's day, every backup shows its day, the Dashboard's day is re-read when the world has
been saved, and a restore waits for a briefly held file or names the holder. Details:
`docs/architecture/v1.0.0.4-restore-world-day.md`.

- Found: the Dashboard's day was three days old (Day 210 vs Day 248); every 2026-10-01 restore failed (two while the
  server ran, two on a held file); no restore was logged.
- Not covered by a recorded check: the in-game day after the server loads a restored world (the owner's to confirm).

## v1.0.0.6 — Buttons and tags from one look (2026-10-05)

"I would like to have consistency with all the buttons and tags ... governed by the central look and not hardcoded ...
Delete to be red, open to be purple, verify to be green." Every button has one intent from `ButtonIntents` (danger,
open, verify, apply, info, caution, plain) and every status tag one kind from `StatusTags` (ok, warn, fail, off, info);
only the style sheet colours them. Details: `docs/architecture/v1.0.0.6-button-intents-and-status-tags.md`.

- Not covered by a recorded check: buttons inside dialogs other than the confirm dialog are checked in the XAML (gate)
  but not rendered by the harness.

## v1.0.0.5 — Distinct Bases and Guilds pages (2026-10-05)

"The bases and guilds sections look too similar and have redundant data." Bases are now places (location, working Pals,
owner, Show on map) and Guilds are people (roster by name, leader, who is online, bases); the shared count cards and
Evidence Model block are gone. Details: `docs/architecture/v1.0.0.5-bases-and-guilds.md`.

- Not covered by a recorded check: Show on map's zoom on the live Map page (the harness checks the button and the pages,
  not the map viewport).

## v1.0.0.3 — NATIVE MODs and the MOD drop zone (2026-10-05)

PalDefender and the UE4SS loader are NATIVE MODs on the MODs page, with an on/off switch that renames the loader; the MOD
drop zone takes a drop anywhere. Details: `docs/architecture/v1.0.0.3-native-mods.md`.

- Found on the owner's server: both loaders renamed to `*.disabled-test` by hand around 2026-10-01, so PalDefender and every
  UE4SS MOD had not loaded since. Switched back on with the owner's go-ahead.
- Not covered by a recorded check: an Explorer drag onto the live window (the harness drops through Avalonia's headless
  input), and PalDefender loading on the real server after the switch-back.

## v1.0.1.0 — An Update button on every Update Center row (2026-10-05)

"The update page should have a button to update every option. It can be greyed out if it is self updating, but should
always have the option to update." Every row has Update; SteamCMD's is greyed out (it updates itself), as are rows that
do not apply and MystTiq with nothing newer. Details: `docs/architecture/v1.0.1.0-update-every-component.md`.

- Not covered by a recorded check: MystTiq's own download from a real published release (no stable release newer than
  the running build exists yet; the harness covers it against a stand-in), and Save Tools' pip upgrade on a real Python.
- Signing: the release workflow signs once the SignPath variables are set (see Code signing); nothing in the code waits on it.

## v1.0.0.2 — Unique player names (2026-10-04)

Asked the same day: names must be unique and duplicates refused; matching ignores case. Each name belongs to the first
account seen with it or to the account it is reserved for (or nobody); a player using another account's name is turned away
and the owner is notified. Details: `docs/architecture/v1.0.0.2-unique-player-names.md`.

- A turned-away player's character already has the name (Palworld reports names only in the world); the owner deletes it
  on Players. Look-alike characters were offered and not chosen.
- Recorded checks cover the rules (logic harness), the routes on isolated data (seed, save, restart) and the card in 12
  languages. They do not replace a second account joining the real server with a taken name.

## v1.0.0.1 — Launcher, identity guard, tray and stuck starts (2026-10-04)

The owner's Launcher workspace (Server > Launcher with Default / No Mods / Show Window presets, manual-parity launch,
startup-window capture, Steam identity on the Players page) and four changes. Details:
`docs/architecture/v1.0.0.1-launcher-identity-tray.md`.

- **Identity guard and argument-free starts.** Players were given new characters in sessions started with MystTiq's
  arguments (also from a script); a double-click, with no arguments, kept them. Servers now start with no arguments by
  default (a Like double-click preset too), and a Steam player not given their own character is caught on join, recorded
  and kicked before a duplicate is made. Which argument was responsible is not known. Real characters were never touched.
- **Close to tray, full Exit.** No `mysttiq-server` is left behind after Exit.
- **Stuck starts.** Detected after two minutes; test without MODs, or find the MOD one at a time, from the Dashboard.
- **Addresses on the Dashboard.** Local and public, with the port.
- Recorded checks cover the stuck-start smoke (a stand-in that hangs on one MOD), the identity rule against the real
  characters' IDs, and both harnesses. They do not replace a player joining after a MystTiq launch, which is what shows
  whether the identity problem is gone.

## v1.1 — MOD browser (planned, requested 2026-09-30)

A MOD browser connected to Nexus Mods and other online repositories: search, read and install MODs from inside MystTiq.
It builds on the Nexus Mods catalog (v0.7.93.0), the website-sourced MOD descriptions (Steam Workshop and GitHub,
v0.7.55.0) and the MOD install, update and rollback paths already in the MOD pages. Repositories to cover and the
details are to be agreed before work starts.

## Code signing (requested 2026-09-30)

The release workflow signs the Windows download through SignPath once the SignPath Foundation application is approved
and the repository variables are set (`docs/release/CODE_SIGNING.md`). v1.0.0.0 is published unsigned (the owner's
decision, 2026-09-30); the first signed release is the first one tagged after the SignPath variables are set (up to v1.0.1.0 every release is unsigned unless they were set before its tag).

## Open after 1.0 — integration and stabilization (formerly v0.9.x)

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
| Upgrade and recovery | Delivered in v0.9.5.0 and v0.9.8.0 (accounts) (above); an update from MystTiq through a refused manifest remains |
| Distribution | `scripts/Test-v0.9.9.0-Distribution.ps1` checks the ZIPs' contents (desktop, headless service, Windows native helper), version identity, SHA-256 checksums and a start from a clean folder; the Linux download was installed and started on the test VM (v0.9.8.0). Still open: a launch on a machine that never had MystTiq or .NET, and source parity against the tag |
| Documentation | Keep README, site, release notes and supported-platform claims aligned with observed results; publish known limitations |
| Crash analysis | Add signatures only from real anonymized reports; do not invent coverage for unseen crashes |
| Avalonia 12 migration | Delivered in v0.9.7.0 (above); the Linux desktop session passed on Avalonia 12 in the v0.9.8.0 gate |

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

Publish v1.0.0.0 on GitHub as a full release (not a prerelease) with current source, the Windows package, release notes and checksums, following [the publishing guide](../release/README.md). A Linux package can be built locally (`Package-GitHubRelease.ps1 -Runtime linux-x64`) but stays unpublished until Linux desktop acceptance is complete. Keep public-release coordination (including any desired Nexus contact or asset permissions) separate from implementation status. No messages are sent on the user's behalf by this roadmap.
