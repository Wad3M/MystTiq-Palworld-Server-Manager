# v0.3.1.8 Player & Guild Explorer Architecture

## Player identity evidence

Player save identities are derived from validated `Players/<32 hex>.sav` filenames in the active world. The client does not scan the filesystem directly.

## Guild semantic evidence

Guilds are accepted only from decoded `GroupSaveDataMap` entries whose `GroupType` is `EPalGroupType::Guild`. The parser uses the decoded Guild `RawData.value` schema for:

- group ID
- guild/group name
- admin player UID
- players / player names
- base IDs

This mirrors the authoritative guild-evidence model already used by the Windows manager.

## Graceful degradation

If decoded `Level.sav.json` evidence is not present, player save identities still load. Guild names, membership, leadership and base references remain unavailable and the API explicitly reports that limitation.

Live REST is optional enrichment for currently-online players. It does not become authoritative historical guild evidence.

## Safety

The v0.3.1.8 explorer contains no save write/delete/encode/repair operation.
