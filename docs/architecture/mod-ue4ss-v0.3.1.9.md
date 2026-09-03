# v0.3.1.9 MOD & UE4SS Architecture

The Avalonia client does not touch MOD files directly. The persistent service owns PAK `~mods`, UE4SS active-root selection, `mods.txt`, and UE4SS.log evidence. Modern UE4SS layout is preferred when present; runtime-log evidence is independently compared so path drift is visible. Disabled and Active / Unverified are neutral. Mutations require PalServer stopped and are serialized.
