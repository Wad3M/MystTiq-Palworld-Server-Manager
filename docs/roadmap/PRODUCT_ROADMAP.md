<!-- MystTiq v0.8.26.0: file reviewed for this release (2026-09-27). -->
# Product roadmap to v1.0

Updated 2026-09-27. **Current version: v0.8.26.0. Accepted baseline: v0.8.25.0. Next milestone: v0.9.0.0.**

This is the active plan. Version assignments after v0.9.0.0 are proposed milestone buckets, not dated commitments. Older planning and completed work are retained in [the historical roadmap](../history/PRODUCT_ROADMAP_through_v0.8.25.0.md), the [changelog](../../CHANGELOG.md) and [release notes](../../release-notes/). Historical “planned” and “not done” statements may have been superseded.

## Delivered through v0.8.25.0

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

## v0.9.0.0 — complete translation coverage

- Translate remaining page content and status values, dialogs, validation/error messages, notifications, tooltips and accessible names.
- Keep internal command identities stable while translating display text; preserve live language switching and English fallback.
- Review German and Spanish with native speakers, including plurals, formatting, text expansion and keyboard/accessibility labels.
- Localize item/Pal display names where supported by the installed game's tables.
- Audit missing keys and visible hard-coded text. Verify narrow layouts in each supported language.
- Treat additional languages as follow-up scope. Check CJK fonts before advertising CJK support.

**Exit evidence:** a page-by-page coverage checklist, key/fallback checks, reviewed language samples and visual/accessibility checks for each advertised language.

## v0.9.x — integration and release stabilization

These are remaining checks or targeted fixes, not a request to rebuild shipped features.

| Work | Acceptance evidence / dependency |
| --- | --- |
| Give Item/Pal and starter kits | Observe delivery to a real online player with the supported provider; verify refusal and error reporting. Run `scripts/Test-v0.8.26.0-InGame.ps1` on the clone server with a player online (rehearsed against stand-ins by the release gate) |
| Live map and teleport points | Verify player markers live; capture paired REST world and PalDefender positions before choosing a coordinate conversion (the in-game script records the pair); verify chat-triggered teleport and then point placement on the map |
| Notifications | Observe real Discord and email delivery, pause/resume, recovery and failure handling. `scripts/Test-v0.8.26.0-Alerts.ps1` sends a real test through every switched-on channel and reports failed sends; confirm arrival in each channel |
| Linux desktop | Window, maximize/restore, clipboard and tray pass on the XFCE test VM (`scripts/Test-v0.8.26.0-LinuxDesktopSession.ps1`, 16/16). Still open: file pickers, scaling and other desktop environments |
| Linux service priority | Install the new unit in a test environment and verify eco-to-normal priority recovery; unit syntax/headless checks alone are insufficient |
| Permissions and accessibility | Explain required roles on disabled controls; verify focus, keyboard navigation, names, contrast and scaling across pages |
| Themes | Verify supported modes throughout the app; with a Windows contrast theme on, run `scripts/Test-v0.8.26.0-ContrastTheme.ps1`; document Linux native contrast limitations |
| Upgrade and recovery | Test fresh setup, upgrade from the accepted baseline, settings/data preservation, backup/restore and rollback on isolated data |
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

Publish v0.8.26.0 on GitHub with current source, the Windows package, release notes and checksums, following [the publishing guide](../release/README.md). A Linux package can be built locally (`Package-GitHubRelease.ps1 -Runtime linux-x64`) but stays unpublished until Linux desktop acceptance is complete. Keep public-release coordination (including any desired Nexus contact or asset permissions) separate from implementation status. No messages are sent on the user's behalf by this roadmap.
