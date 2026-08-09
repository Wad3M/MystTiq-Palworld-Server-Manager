# v0.2.15.4 FIX1 — MOD Archive Normalization Null Guard

## Problem

Installing or previewing certain valid MOD archives could throw:

`Object reference not set to an instance of an object.`

The failure occurred in `ModService.BuildInstallPlan()` when
`FindCommonWrapperFolder()` returned `null` and the archive normalization
logic called `commonRoot.Equals(...)`.

This affected valid layouts including:

- Root-level Win64 loader archives such as PalDefender (`PalDefender.dll`, `d3d9.dll`)
- UE4SS packages that contain both root documentation files and a `Mods/<ModName>/...` tree

## Fix

The runtime-wrapper check now uses the null-safe static
`string.Equals(commonRoot, ...)` form.

No MOD routing rules, active-root selection, installer paths, migration logic,
or runtime-loaded diagnostics were otherwise changed.

## Validation

Re-test both:

1. PalDefender root DLL/proxy-loader ZIP.
2. MystPalIntelligence `Mods/MystPalIntelligence/Scripts/main.lua` ZIP.

Both packages should now reach their intended package detectors without a
null-reference exception.
