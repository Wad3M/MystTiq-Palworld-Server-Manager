<!-- MystTiq v1.0.4.0: file reviewed for this release (2026-10-05). -->
# Pre-publication review

- Confirm the tag, `Directory.Build.props`, release notes and binary version all identify the version being released (v0.9.9.0 for this release).
- Inspect staged Git changes. Keep local settings, saves, logs, server data, credentials, build output, third-party distribution ZIPs and obsolete source manifests out of Git.
- Check screenshots, examples and exported diagnostics for private account/server details. A clean extension scan alone is not proof that no secrets exist.
- Include the matching headless service and native Windows console helper (the legacy WPF application was removed in v0.8.26.0 and is no longer built).
- Verify fresh extraction, startup, SHA-256 hashes and an isolated upgrade/rollback. Record checks that need live players, channels or a real Linux desktop as pending.
- Preserve attribution. The code's MIT license does not relicense Palworld/game artwork; review the maps and supplied artwork before public distribution.
- Review the draft on GitHub before publishing. Any desired Nexus coordination is a separate owner action, not a completed release check.
