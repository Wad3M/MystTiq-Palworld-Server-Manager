<!-- MystTiq v1.0.1.0: file reviewed for this release (2026-10-05). -->
# Translation review

MystTiq ships in 12 languages. English is the source. The other 11 are complete drafts, **awaiting native review**:

| Language | Code | Status |
| --- | --- | --- |
| 简体中文 | zh-Hans | awaiting native review |
| Español | es | awaiting native review |
| Português (Brasil) | pt-BR | awaiting native review |
| Русский | ru | awaiting native review |
| Deutsch | de | awaiting native review |
| Français | fr | awaiting native review |
| 日本語 | ja | awaiting native review |
| 한국어 | ko | awaiting native review |
| Italiano | it | awaiting native review |
| Polski | pl | awaiting native review |
| Türkçe | tr | awaiting native review |

## Getting a review sheet

```powershell
pwsh scripts/Export-MystTiqTranslationReview.ps1 -Language ja
```

This writes `MystTiq-review-ja.csv` (to the temp folder unless `-OutputDirectory` is given): every text with its key,
kind, English, current translation and where it is used, plus empty **Correction** and **Notes** columns. Open it in
any spreadsheet, fill in Correction only where the translation should change, and send the sheet back.

## Checklist

Read each text as a user of the app would meet it, then check:

- **Meaning:** the translation says what the English says, no more and no less. Messages marked `{0}`, `{1}` get a
  value (a name, number, path or another message) in that place; keep every placeholder, in whatever order reads
  naturally.
- **Terminology:** the same English term is translated the same way everywhere (server, world, save, backup, Pal,
  guild, base, port). Page and button names in a message match the page or button itself.
- **Game words:** item and Pal names come from the game's own tables and are not in these sheets. Palworld terms that
  the game translates (Pal, base, guild) should follow the game's translation.
- **Plurals:** English uses "{0} player(s)"; write the form your language uses for any count, or a form that reads
  correctly for 1 and for many.
- **Dates and numbers:** they are currently shown in one fixed format for every language; note in Notes where that
  reads wrongly (a later version formats them per language).
- **Length:** very long texts may be cut on small windows; note texts that should be shorter (tabs, Ribbon buttons,
  column headers).
- **Accessibility:** texts read by screen readers (button names, tips) should make sense on their own.
- **Leave as they are:** commands sent to the game (KickPlayer, TeleportToMe…), setting names (AdminPassword,
  RCONEnabled), file names, and product names (Palworld, Steam, UE4SS, RCON).

## Coverage

| Area | Where it comes from | Keys |
| --- | --- | --- |
| Window texts: labels, buttons, tips, menus, accessible names | `ui.*`, `nav.*`, `page.*`, `ribbon.*`, `category.*` | about 1,000 |
| The Desktop's own messages: status, progress, results, errors, dialogs | `msg.*` (v0.9.1.0, v0.9.2.0) | 1,153 |
| The service's messages: Doctor, operations, crash explanations, network checks | `msg.*` (v0.9.3.0) | 1,339 |
| Item and Pal names | the installed game's own tables (v0.9.2.0) | read from the game |

Every key exists in every language (the release gate fails otherwise), and the ArtworkHarness visits every page in
every language looking for English left on screen.

## Applying corrections

Corrections go into `src/MystTiq.Desktop/Assets/i18n/<code>.json` under the same key. Keep placeholders and leading or
trailing spaces as they are in English. Then run the ArtworkHarness (it round-trips every message template) and the
release gate.
