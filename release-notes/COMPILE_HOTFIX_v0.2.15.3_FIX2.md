# Compile Hotfix — v0.2.15.3 FIX2

## Failure

`ModService.cs` failed compilation at line 96 with CS8087 and CS1009.

## Cause

The migration conflict-display string used a single backslash immediately before an interpolated expression:

```csharp
conflicts.Add($"{modName}\{relative}");
```

In a C# interpolated string the path separator must be escaped.

## Fix

```csharp
conflicts.Add($"{modName}\\{relative}");
```

This is a compile-only correction. No runtime logic was otherwise changed.
