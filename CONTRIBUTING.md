# Contributing

Thank you for helping improve MystTiq Palworld Server Manager.

## Before opening an issue

- Search existing issues.
- Remove passwords, tokens, public IP addresses, private player identifiers, save files, and personal logs.
- Include the application version, Windows version, action performed, expected behavior, actual behavior, and exact error text.

## Development workflow

1. Fork the repository.
2. Create a focused branch, such as `fix/notification-toggle`.
3. Keep changes small and preserve the established dark theme, button standards, tooltip standards, and existing architecture.
4. Build in `Release | x64`.
5. Test the affected workflow with the server stopped and running where applicable.
6. Submit a pull request explaining the change and test results.

## Code expectations

- Use nullable reference types correctly.
- Avoid blocking the WPF UI thread.
- Validate paths and handle files disappearing during live Palworld saves.
- Back up world data before destructive operations.
- Do not introduce new direct world-write paths without a validated transaction and rollback design.
