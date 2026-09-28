# Versioning Policy

Current MystTiq releases use `MAJOR.MINOR.REVISION.FIX`.

- A new revision starts with fix `0`: `v0.4.4.0`.
- A correction discovered before promotion increments only the fix component: `v0.4.4.1`, then `v0.4.4.2`, then `v0.4.4.3`.
- A failed build, validation, logic, regression, packaging, platform, or runtime gate never advances the revision.
- Only a completely passing release is promotion eligible.
- After promotion, the next planned feature revision advances the revision component and resets fix to `0`.
- Historical versions that used separate `FIX1`/`FIX2` suffixes remain unchanged in historical records.

Example progression: `v0.4.5.1` (promoted baseline) -> `v0.4.6.0` (next development revision) -> `v0.4.6.1` (first fix, if required) -> `v0.4.7.0` (next revision after promotion).
