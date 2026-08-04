# MystTiq Palworld Server Manager

A free and open-source Windows application for installing, operating, backing up, inspecting, and maintaining a Palworld dedicated server.

> **Current release:** v0.2.12  
> **Platform:** Windows x64  
> **License:** MIT

## Highlights

- One-page server operations dashboard
- Start, stop, restart, update, and backup controls
- CPU and RAM history monitoring
- Operational state and overall health reporting
- Backup center with restore workflows
- Player, guild, base, world, and save inspection tools
- MOD inventory, validation, and UE4SS support
- Activity and notification center
- Dark MystTiq interface with standardized buttons and tooltips

## Download

Download the latest portable ZIP or installer from the repository's **Releases** page. The portable build is self-contained and does not require a separate .NET installation.

## Requirements

- Windows 10 or Windows 11, 64-bit
- A Palworld Dedicated Server installation
- Administrator privileges may be required for server folders, firewall rules, services, or protected installation locations

## Build from source

1. Install the .NET 10 SDK and Visual Studio with the **.NET desktop development** workload.
2. Clone or download this repository.
3. Open `PalworldServerManager.slnx`.
4. Build `Release | x64`.

PowerShell build:

```powershell
./scripts/Build.ps1
```

Create the portable package:

```powershell
./scripts/Package-Portable.ps1
```

## Safety

World and save editing can carry risk. Keep tested backups, stop the server before performing destructive operations, and verify the selected world before applying changes.

Do not publish server passwords, REST credentials, Steam credentials, private player saves, logs, or backup archives in issues.

## Project status

v0.2.12 is the first public open-source baseline. New architecture and repair features will be developed after the repository, release, and packaging process has been validated.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Bug reports and focused pull requests are welcome.

## Security

See [SECURITY.md](SECURITY.md) before reporting a vulnerability or suspected credential exposure.

## License

Released under the [MIT License](LICENSE).

## Disclaimer

MystTiq Palworld Server Manager is an independent community project. It is not affiliated with, endorsed by, or sponsored by Pocketpair, Inc. Palworld and related names are trademarks of their respective owners.
