# v0.5.1.0 Build and Test Plan

Promotion order:

1. Close artifact-hosted MystTiq desktop and sidecar processes with the repository Clean workflow.
2. Run strict release validation.
3. Run the complete v0.5.1.0 logic suite and export its JSON evidence.
4. Build shared code plus Windows and Linux headless targets.
5. Publish the Avalonia desktop for Windows and Linux without launching during compilation.
6. Run the isolated Windows management API runtime smoke suite.
7. Launch the Windows desktop only after every automated gate is clean and visually verify branding, World/System navigation, bell/gear routing, containment and button alignment.
8. Create FullSource and Changed Files ZIPs and verify SHA-256 checksums.

Any failure blocks promotion and requires a fix-version increment for a delivered correction.
