<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.1.6.0 Build and Test Plan

1. Static gate: `pwsh ./scripts/Test-v1.1.6.0-Logic.ps1` (the N-1 contracts: no `apikey` header, PKCE, the redirect address,
   the token signature check, the old key removed, the card without a key box, the strings in 12 languages).
2. Full gate: `pwsh ./scripts/Test-v1.1.6.0-Logic.ps1 -RunBuild -RunLong`.
3. Desktop harness, N-1: the RFC 7636 challenge; the authorize address; the callback accepts only this sign-in's state
   and tells a refusal from a stray request; tokens signed with the key are read, tampered, `alg: none`, expired and
   other-key tokens are refused; a full sign-in against a scripted token endpoint through the real loopback listener
   (the verifier checked against the challenge); the tokens kept and reloaded; a refresh inside the one-minute margin,
   then the cached token; signed out when the refresh is refused; an old personal key deleted; Bearer, never `apikey`;
   the card with Sign in and Sign out and no key box.
4. With Nexus's Client ID in `Directory.Build.props`: sign in with a real account (the owner), load Trending, look up a
   mod, sign out.
5. Packages as for v1.1.5.1.
