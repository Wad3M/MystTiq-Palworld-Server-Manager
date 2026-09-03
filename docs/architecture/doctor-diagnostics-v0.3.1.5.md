# v0.3.1.5 Production Doctor & Diagnostics Architecture

The headless host owns production-health semantics. `HeadlessDoctorService` evaluates the installed configuration and runtime and returns a structured report. `/api/v1/doctor` exposes that report through the same bearer/TLS middleware as other management endpoints. The Avalonia client only renders the returned state, evidence, and recommendations; it does not create a second platform-specific health engine.

The report intentionally excludes API bearer tokens, certificate passwords, and Palworld administrative credentials. Export is performed by the desktop client to the current user's `MystTiqReports` directory.
