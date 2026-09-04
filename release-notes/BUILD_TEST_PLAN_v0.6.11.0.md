# v0.6.11.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.11.0 logic suite (`scripts/Test-v0.6.11.0-Logic.ps1 -RunBuild`), including the frozen v0.6.10.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Stale-instance fix:
   - Launch an old-version `mysttiq-server.exe` sidecar on the default port; launch the new build's Desktop app; confirm it detects the version mismatch and starts a fresh backend on a different loopback port instead of attaching, with a clear detail message naming both versions and both endpoints.
   - Confirm a same-version instance is still reused normally (no regression to the compatible-reuse path).
5. WAN reachability, against a real network with internet access (read-only checks are safe to run directly; the UPnP repair action mutates the router and should only be exercised with the operator's own explicit click):
   - `GET /diagnostics/network/wan` returns a real detected public IP and a real UPnP mapping state (Mapped/NotMapped/RouterUnreachable/Unsupported as appropriate for the test network).
   - `POST /diagnostics/network/wan/upnp/repair` against a UPnP-capable router actually creates a mapping; re-running the GET confirms it now reports Mapped.
   - On a network without a responding UPnP router, confirm the report degrades to `Unsupported`/`Warning` rather than throwing or hanging.
6. On the Desktop app: confirm the Diagnostics Center page's new WAN / External Reachability card renders, "Run Reachability Check" populates all fields, "Copy IP:Port" copies to the clipboard, and "Open Port Checker" opens the browser.
7. Visual check: Start button renders as a soft green, Stop remains its existing style at matching intensity, the selected nav item is dark (not light lavender), and the header shows the new tagline.
8. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
