<!-- MystTiq v1.0.0.4: file reviewed for this release (2026-10-05). -->
# Privacy policy

MystTiq collects no telemetry or analytics and sends nothing to the project's authors. It keeps its settings, accounts,
logs and backups on the computers where you run it.

MystTiq connects to other systems only to do what you run it for:

- **Update checks:** the service asks GitHub (MystTiq, PalDefender and UE4SS releases), PyPI (pip, palworld-save-tools),
  the .NET release index and Steam (SteamCMD and the game's public build) for current versions. Some of these run on a
  schedule while the service runs, for the Alert Center's out-of-date alert. They send no account or server data.
- **Server management:** SteamCMD downloads and updates the game server; the game server itself talks to players and
  Pocketpair's services as it always does.
- **The Dashboard's public address:** while the Dashboard is open, the service asks your router (UPnP, on your network)
  for its internet address and, when the router gives none, api.ipify.org. The answer is kept for an hour.
- **Only when you use or set them up:** Nexus Mods (with your own API key), Steam Workshop details, a public-address
  check (api.ipify.org) and port check links, Discord, email and webhook notifications, and connections to other MystTiq
  services you add.
- **On your own network:** the server search and router port mapping (UPnP) when you ask for them.

Like any internet request, these reveal your IP address to the service contacted, under that service's own privacy
policy.
