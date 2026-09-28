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

**Exit evidence:** a scripted run from a clean machine state and from a v0.8.25.0 install, with every setting, account and world compared before and after.

## v0.9.x — integration and release stabilization

These are remaining checks or targeted fixes, not a request to rebuild shipped features.

| Work | Acceptance evidence / dependency |
| --- | --- |
| Give Item/Pal and starter kits | Observe delivery to a real online player with the supported provider; verify refusal and error reporting. Run `scripts/Test-v0.8.26.0-InGame.ps1` on the clone server with a player online (rehearsed against stand-ins by the release gate) |
| Live map and teleport points | Verify player markers live; capture paired REST world and PalDefender positions before choosing a coordinate conversion (the in-game script records the pair); verify chat-triggered teleport and then point placement on the map |
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
