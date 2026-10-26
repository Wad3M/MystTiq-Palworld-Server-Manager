<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# MystTiq v1.1.6.0 Checkpoint: Sign In with Nexus Mods

## What changed

- **Nexus Mods sign-in (N-1).** The Nexus Mods card signs in with OAuth 2.0 and PKCE on Nexus's own page instead of a
  personal API key (Nexus Mods support, 2026-10-26). The tokens are kept encrypted on this PC and refreshed; the old key
  box is gone and a key saved before is deleted. API calls send `Authorization: Bearer`.
- Until Nexus issues MystTiq's Client ID (`NexusOAuthClientId` in `Directory.Build.props`), the card says sign-in is not
  available yet.

## Verification

- **Full gate** `scripts\Test-v1.1.6.0-Logic.ps1 -RunBuild -RunLong` (2026-10-26): 465 / 465 passed, 6 skipped (the Linux VM,
  removed from the owed list by the owner). Static gate 404 / 404. Desktop harness 877 checks, including N-1: the RFC 7636
  challenge; the authorize address; only this sign-in's state accepted; tokens signed with the key read, and tampered,
  `alg: none`, expired and other-key tokens refused; a full sign-in through the real loopback listener against a
  scripted token endpoint (the verifier checked against the challenge); the tokens kept and reloaded; one refresh inside
  the minute, then the cached token; signed out on a refused refresh; an old personal key deleted; Bearer, never
  `apikey`; the card with Sign in and Sign out and no key box.
- **Not yet tested live:** a real Nexus Mods sign-in needs the Client ID Nexus issues on registration
  (`NexusOAuthClientId` is empty in this build, so the card says sign-in is not available yet).
- **Packages:** Windows ZIP, portable ZIP (with `portable.txt` and the console proxy), setup `.msi` (administrative
  extract: 577 files, installed.txt, the console proxy, no portable.txt), Linux ZIP and Docker image (`/healthz` answered
  1.1.6.0 over TLS); all in `SHA256SUMS.txt`; the distribution check passed; `Validate-Release -Strict` 0 errors,
  0 warnings.

## Still owed

The Client ID from Nexus Mods (the owner emails support@nexusmods.com), then a real sign-in and the reply to Nexus;
SignPath (O-4: they asked for references); the SecureAge false positive (O-10); the Dependabot pull request (O-11); the
translation review (O-8).
