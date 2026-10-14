<!-- MystTiq v1.0.8.7: file reviewed for this release (2026-10-12). -->
# Review of "MystTiq Palworld Server analyses" (2026-10-07)

The owner supplied an outside static review (`MystTiq Palworld Server analyses.docx`, 12 findings A–L, a proposed
v1.0.6.2–v1.0.6.7 split). Each finding was checked against the v1.0.6.1 source. This file records what holds, what is
already handled, what is new, and a plan. Nothing here is scheduled until the owner approves it; approved items move
into `PRODUCT_ROADMAP.md` under new `Q-` (quality) IDs and follow its evidence rule.

## 1. Verdicts

| Report item | Verdict | What the source shows |
|---|---|---|
| A. Fire-and-forget background tasks | **Partly holds** | 14 `_ = Task.Run(...)` sites in HeadlessHost. The automation ones (`HeadlessAutomationService.cs:195/219/519`) are **not** unsupervised: `ExecuteRuleAsync` catches everything, tracks the run in `activeRuns`, records Completed/Failed/Cancelled with the operation id, and the next due time is advanced before dispatch. The others are weaker: `HeadlessWorldClockService.cs:71` and `HeadlessBackupService.cs:378` swallow all errors (`catch { }`), the Discord connect/live loops and notification dispatch are not awaited on shutdown. One real gap: a rule whose run outlasts its interval can be started again while still running (no "already running" check). |
| B. `Dispatcher.UIThread.Post(async …)` | **Holds, and worse than stated** | 19 such posts in the Desktop. Most target methods catch internally, but not all (e.g. `RefreshAsync` calls `RefreshLocalInstallationAsync` before its first `try`). There is **no last-chance exception handler anywhere** — no `Dispatcher.UIThread.UnhandledException`, `AppDomain.UnhandledException` or `TaskScheduler.UnobservedTaskException` in Desktop or HeadlessHost — so an exception escaping any of the 19 posts or 42 `async void` handlers ends the app with no record. Duplicate runs are already limited by `IsBusy`; stale tab results by the `requestTab` check (v0.7.6.0). |
| C. Silent exception handling | **Holds; one case is a data-loss bug** | ~290 bare/empty catches. The four cited are mostly fine (`ServerDistribution:68`, `EnvironmentChecklist:181`, `ConsoleLogWriter:37` are best-effort reads/writes). But `HeadlessUserAccountService.Load` (`:318-322`) returns an empty list when `users.json` cannot be read, and the next account change **writes that empty-plus-one list over the file — every account is lost**. The same load-empty-then-overwrite pattern exists for sessions, automation rules (`:545/559`), kits (`:410/416`), anti-cheat, alert center and identity-guard stores. |
| D. Operation conflicts | **Mostly already handled; three gaps** | `OperationCoordinator` (Core) holds per-server resource locks (`lifecycle`, `world-mutation`) and rejects conflicts with a reason. Start/stop/restart from the API, Discord and automation, restore, Safe Start, and every save edit use it; backup create and restore share `backupGate`. Gaps: (1) a scheduled **backup create takes no `world-mutation` lock**, so it can zip a world mid-edit (a multi-file edit could be captured half-done); (2) **SteamCMD update** (`HeadlessServerDistributionService`) and **MOD changes** (`HeadlessModManagementService`) check "server may be running" but hold no `lifecycle` lock, so a start between the check and the work is possible; (3) the compatibility matrix exists only in code — not written down or tested as a whole. |
| E. 600 KB main ViewModel | **Holds** | `MainWindowViewModel.cs` is 599 KB / 10,704 lines plus 19 partial files. Maintainability, not speed. A big-bang split is high-risk (the gates pin text in these files); extract incrementally when an area is touched. |
| F. `FileRetry` blocks and retries any IOException | **Holds** | Its own comment says "sharing violation only", but it retries every `IOException` (including disk full / path errors). Callers are background service work, so the blocking (≤2.5 s, rare) is acceptable; the filter is the fix. Note: the failure that caused it was `ERROR_UNABLE_TO_REMOVE_REPLACED` (1175), not a plain sharing violation, so the filter must include it. |
| G. HttpClient lifecycle | **Holds, low impact** | 9 per-call `new HttpClient(handler)` sites, all loopback or one-off probes (the Desktop API client is already reused). At the 5–10 s poll rate there is no socket-exhaustion risk. The GUI-state part (unreachable / auth rejected / Palworld stopped / REST unresponsive / reconnecting) is the useful half; how much of it exists needs checking. |
| H. Collection updates | **Holds, with a display bug** | Logs are already bounded (500-line server tail, metric history capped). But every 5-s poll clears and re-adds ~500 lines to `LogLines` and again to `FilteredLogLines`, and runs a regex per line, even when nothing changed. Bug found: `EnsureConsoleTimestampFirst` stamps a line that has no timestamp with **the poll time**, so such lines show a new time on every refresh. Console lists are `ListBox` (virtualised by default). |
| I. Operation lifecycle / progress | **Partly exists** | Operations are already journalled with phases and stages, survive restarts (v0.7.115.0) and are listed (`RecentOperations`). Missing: start-up steps — the server is just `Starting` until `Running`/Ready, with no "launching / waiting for port / checking REST" detail. |
| J. Architecture | **No separate action** | Agreed in principle; the concrete parts are Q-4 and Q-8 below. |
| K. Security audit targets | **One verified safe, rest to audit** | Archive extraction is safe: every ZIP/TAR extraction either uses the framework's extractors (which refuse paths outside the target) or checks the destination explicitly. There is no central redaction helper; a pass over what is logged (launch arguments, webhook URLs, tokens) is worth doing. |
| L. Tests and benchmarks | **Partly exists** | FakePalServer, route smokes, race tests, locked-save and rollback tests exist. Missing: a long idle run, a 10,000-line console test, rapid tab switching, interrupted SteamCMD update. |

Not adopted: the report's six-release split (several of its releases are already done or are low value), a general
"supervised operation manager with bounded concurrency" (the coordinator already exists; a small helper suffices), and
`IHttpClientFactory` (no DI container in HeadlessHost; not worth adding for loopback polls).

## 2. Continued review (findings 12–19), checked 2026-10-08

| Report item | Verdict | What the source shows | Roadmap |
|---|---|---|---|
| 12. Notification sends not bounded | **Holds** | `HeadlessNotificationRoutingService.Dispatch` starts one `Task.Run` per notification, with no queue or limit. Each send's failure is caught and recorded (R-2). Alert Center rules have cooldowns, but other notifications (crash, identity guard, automation) go straight through. | Q-11 |
| 13. Backup world-day scan | **Holds** | `QueueWorldDays` passes `CancellationToken.None`, swallows every error, and rewrites the whole world-day file after each backup. | Q-3 (cancel, log), Q-12 (progress, batched writes) |
| 14. Lifecycle state store retries | **Partly holds** | 20 attempts, 25 ms apart. Retrying `UnauthorizedAccessException` is deliberate: Windows reports a file held by a scanner as access denied on a replace (v0.9.6.0, seen live). Missing: a failure that outlasts the retry is not reported with its cause, and there are no tests for concurrent or interrupted writes. | Q-5 |
| 15. Coordinator should be the single authority | **Agreed** | The coordinator already covers lifecycle, restore and every save edit (first review, D). The gaps are backup create and verify, SteamCMD update, MOD changes and component updates. | Q-4 |
| 16. SMTP password in plain text | **Holds (a documented choice)** | `channels.json` keeps the SMTP password in plain text, protected by file permissions, the same as tokens. An earlier plan called for DPAPI storage of tokens. | Q-14 |
| 17. Archive resource limits | **Partly already done** | World import already limits the archive (2 GiB), its entry count (20,000) and its expanded size (8 GiB). MOD installs (download capped at 512 MB but expansion unlimited), MOD snapshot and backup restores, and self-update have no expansion or free-space limit. | Q-15 |
| 18. Navigation polls the busy state | **Holds** | `RefreshPageForNavigationAsync` checks `IsBusy` every 500 ms for up to 60 s for Map and Players, with nothing shown meanwhile. | Q-13 |
| 19. Verify All is sequential | **Holds (fine as is)** | It runs one archive at a time under `backupGate`, and cancellation is passed through. It shows no progress. Parallel verifying is not needed. | Q-12 |

## 3. Where the plan lives

All items are scheduled in [PRODUCT_ROADMAP.md](PRODUCT_ROADMAP.md) (owner decision D-9, 2026-10-08):
- Q-1 to Q-15 are in v1.0.7.0 to v1.0.7.5.
- The ViewModel split (E) is C-0 to C-9, in v1.0.8.0 to v1.0.8.9.

Mapping from the first review:

| Review item | Roadmap |
|---|---|
| A | Q-3 |
| B | Q-2 |
| C | Q-1 and Q-2 |
| D | Q-4 |
| E | C-0 to C-9 |
| F | Q-5 |
| G | Q-7; the shared HTTP client is done when `HeadlessMonitoringService` is next touched |
| H | Q-6 |
| I | Q-8 |
| J | Q-4 and Q-8 |
| K | Q-9 |
| L | Q-10 |
