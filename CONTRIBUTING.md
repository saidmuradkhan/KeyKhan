# Contributing to KeyKhan

Thanks for helping! Bug reports, ideas and pull requests are all welcome.

## Before you start

- **Found a security problem?** Follow [SECURITY.md](SECURITY.md) instead of opening an issue.
- **Bug or idea?** Open an [issue](https://github.com/saidmuradkhan/KeyKhan/issues/new/choose) first, so we can agree
  on the approach before you write code.
- **Looking for something to do?** Issues labelled
  [good first issue](https://github.com/saidmuradkhan/KeyKhan/labels/good%20first%20issue) are a gentle start.

## Building

You need Windows 10 or 11 (x64), the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Visual Studio 2022/2026
or Build Tools with the **Desktop development with C++** workload.

```powershell
git clone https://github.com/saidmuradkhan/KeyKhan.git
cd KeyKhan
dotnet run --project src/KeyKhan.App     # run the app
.\build.ps1                              # all tests + a release build
```

To try changes without touching your real vault, give the app its own folder:

```powershell
$env:KEYKHAN_HOME = "$env:TEMP\keykhan-dev"
dotnet run --project src/KeyKhan.App
```

## Where things live

| Folder | What it is |
| --- | --- |
| `src/KeyKhan.Native` | C++20 core: key derivation, AES-GCM, key wrapping, DPAPI and TOTP over Windows CNG |
| `src/KeyKhan.Vault` | .NET library: vault file format, entries, folders, generator, search, CSV |
| `src/KeyKhan.App` | WPF app (MVVM): windows, hotkey, tray, clipboard, auto-lock, auto-type, updates |
| `tests/` | xUnit tests for the vault library and the app logic |
| `installer/` | Inno Setup script for `KeyKhan-Setup.exe` |

## Pull requests

- Keep each pull request to one change, and match the style of the surrounding code.
- The build treats warnings as errors; `.\build.ps1` must pass.
- Add or update tests for behaviour you change. Changes to the vault format or crypto need tests that cover
  wrong keys and tampered data.
- Write commit messages as `type: what changed`, for example `fix: keep the selection after moving an entry`
  (`feat`, `fix`, `docs`, `build`, `refactor`, `test`).
- Changes to `src/KeyKhan.Native` or the vault format get an extra careful review, so please explain your reasoning
  in the pull request.

## Releases

Maintainers release by pushing a `vX.Y.Z` tag; the release workflow builds, tests and publishes the installer and the
portable executable, and installed copies pick the update up automatically.
