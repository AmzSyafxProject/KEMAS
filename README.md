# KEMAS – source (private)

Source code for **KEMAS – Your PC, Kemas.** Public releases live at
[AmzSyafxProject/KEMAS](https://github.com/AmzSyafxProject/KEMAS).

- `src/` – C# (.NET Framework 4.8, WPF) app: engine, UI, updater, installer
- `res/` – icon, admin manifest, version info
- `tests/` – engine tests (Mono) and XAML validator
- `poster/` – release poster source
- `PANDUAN.md` – release / update / anti-virus guide (Bahasa Melayu)

## Build
1. Put the WPF reference DLLs in `ref/` (see `build.sh`).
2. `./build.sh` → `out/KEMAS.exe` + `out/KEMAS.exe.sha256`.
3. Bump `AppInfo.Version` (src/Core.cs), `AssemblyVersion` (src/App.cs) and `res/kemas.rc` before each release.

Created by Amzar Syafiq · Ceritareka Studio.
