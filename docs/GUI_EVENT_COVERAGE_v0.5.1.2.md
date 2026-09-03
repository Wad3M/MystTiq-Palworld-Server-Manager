# v0.5.1.2 final legacy GUI event coverage

The authoritative row-level report is `docs/reconstruction/MystTiq_v0.2.16.4_GUI_Event_Coverage_v0.5.1.2.csv`. It contains all 287 rows from the final v0.2.16.4 WPF event inventory, in original order, with a classification, current mapping and evidence for every row.

| Classification | Rows | Closure rule |
|---|---:|---|
| Mapped | 255 | Historical intent is represented by an Avalonia command/binding or an adapted local-only action over the current headless architecture. |
| Intentionally obsolete | 19 | WPF presentation plumbing was replaced by Avalonia binding/layout or the single aggregate refresh timer. |
| BACKEND REQUIRED | 13 | Unsafe or codec-dependent mutation remains visibly disabled and is not simulated in the desktop. |
| Visibly unavailable | 0 | No remaining handler required this classification independently of the explicit BACKEND REQUIRED group. |
| **Total** | **287** | Every source inventory row is classified. |

Mapped does not imply a one-to-one copy of the old event handler. It means the user intent is present through the required View → ViewModel → API client → route → headless service chain where server authority is needed, or through a deliberately local-only Avalonia file/shell action where the operation belongs to the GUI computer.

The report preserves these closeout boundaries:

- one aggregate periodic status poll;
- bearer tokens remain process-memory-only;
- TLS and optional SHA-256 certificate pinning remain enforced by the API client;
- remote profiles cannot browse or open server paths through the GUI computer;
- support output redacts secrets, URL credentials, query strings and fragments;
- world/save mutations retain Preview → Safety Backup → Server-side Transaction → Validate → Journal/Audit → Refresh GUI;
- unavailable mutations remain visibly BACKEND REQUIRED.
