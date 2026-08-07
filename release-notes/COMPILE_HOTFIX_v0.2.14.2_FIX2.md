# v0.2.14.2 FIX2 — Player Identity Deduplication

## Fixed
- Player History now collapses historical records that share the same User ID, Steam ID, or Palworld Player ID.
- A REST-discovered player and an imported-save placeholder for the same person no longer appear as separate rows.
- Duplicate cleanup runs when player history loads, after save discovery, and after live REST player merges.
- Canonical records preserve the strongest available name, identifiers, online state, ban state, notes, timestamps, platform details, and source.
- Dashboard known-player totals now use the deduplicated player history.

## Root cause
Previous validation deduplicated save filenames, but the persisted player-history file could still contain multiple records with different history keys that resolved to the same actual Palworld Player ID.
