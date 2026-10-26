# KeyKhan

A fast, offline password manager for Windows. Press **Win + ~** anywhere, type a few letters, hit **Enter** — the password is on your clipboard and the window is gone.

The cryptographic core is written in **C++** on top of Windows CNG; the app is **.NET 10 / WPF** with the Windows 11 Fluent theme.

*KeyKhan* = **Key** + **Khan**, after its author Said Muradkhan.

![Main window](docs/main-window.png)

## Features

- **Quick access from anywhere** — a global shortcut (default `Win + ~`, configurable) opens a Spotlight-style search over your vault.
  - `Enter` copy password · `Shift+Enter` copy username · `Ctrl+Enter` auto-type username, Tab, password, Enter into the app you were using · `Ctrl+T` copy 2FA code · `Ctrl+O` open website · `Esc` close
- **Safe clipboard** — copied secrets are excluded from Windows clipboard history and cloud clipboard, and are cleared after 20 seconds (configurable).
- **Built-in authenticator** — store a 2FA setup key or `otpauth://` link and KeyKhan shows the current one-time code (TOTP, SHA-1/256/512).
- **Password generator** — length, character sets, look-alike exclusion, strength meter.
- **Folders and favorites**, instant search, letter avatars.
- **Auto-lock** after inactivity, when Windows locks and before sleep.
- **Import** CSV exports from Chrome, Edge, Firefox, Bitwarden, LastPass, 1Password and KeePass; **export** to CSV.
- **Tray app** — starts with Windows (optional), lives in the notification area.
- Light, dark or system theme.

<p>
  <img src="docs/quick-access.png" width="520" alt="Quick access">
  <img src="docs/generator.png" width="300" alt="Password generator">
</p>

## Install

1. Open the [Releases](https://github.com/saidmuradkhan/KeyKhan/releases) page and download **`KeyKhan.exe`** and **`install.ps1`** into the same folder.
2. Right-click `install.ps1` → **Run with PowerShell**.

That's it: KeyKhan is installed for your user account (no admin rights), added to the Start menu and launched. On first start it asks you to create a master password.

> Prefer a portable app? Just run `KeyKhan.exe` directly — it is a single self-contained file, no .NET installation required.
>
> Windows SmartScreen may warn about an unsigned app the first time; click **More info → Run anyway**.

To uninstall:

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -Uninstall
```

Your vault is kept unless you delete `%APPDATA%\KeyKhan` yourself.

## Keyboard shortcuts

| Main window | |
| --- | --- |
| `Ctrl+F` | Search |
| `Ctrl+N` | New entry |
| `Ctrl+E` / double-click | Edit entry |
| `Ctrl+C` / `Enter` in search | Copy password |
| `Ctrl+B` | Copy username |
| `Ctrl+T` | Copy 2FA code |
| `Ctrl+U` | Open website |
| `Ctrl+D` | Toggle favorite |
| `Ctrl+G` | Password generator |
| `Ctrl+L` | Lock |
| `Del` | Delete entry |

Change the global shortcut in **Settings → Quick access**: click the box and press the new combination.

## Build from source

Requirements (Windows 10/11 x64):

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — `winget install Microsoft.DotNet.SDK.10`
- Visual Studio 2022/2026 **or** Build Tools with the **Desktop development with C++** workload —
  `winget install Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended --passive"`

```powershell
git clone https://github.com/saidmuradkhan/KeyKhan.git
cd KeyKhan
.\build.ps1      # runs the tests, produces artifacts\publish\KeyKhan.exe
.\install.ps1    # optional: install it for your user
```

For development, open `KeyKhan.sln` in Visual Studio, or:

```powershell
dotnet run --project src/KeyKhan.App
dotnet test --project tests/KeyKhan.Vault.Tests
```

`dotnet build` compiles the C++ core automatically by locating Visual Studio's MSBuild with `vswhere`.

## Security design

| | |
| --- | --- |
| Key derivation | PBKDF2-HMAC-SHA256, 600 000 iterations, 256-bit random salt per vault |
| Encryption | AES-256-GCM; a fresh random nonce on every save |
| Integrity | The file header (format version, KDF parameters, salt) is authenticated as GCM associated data, so it cannot be tampered with |
| Key custody | The derived key never leaves the C++ core. It lives in memory locked against paging (`VirtualLock`) and is wiped with `SecureZeroMemory` on lock |
| Master password | Read from WPF's `PasswordBox` as a `SecureString` and handed to the core through unmanaged memory that is zeroed immediately — it never becomes a .NET string |
| Storage | Writes go to a temporary file and atomically replace the vault, so a crash or power loss cannot corrupt it |
| Clipboard | Marked `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory = 0`, `CanUploadToCloudClipboard = 0`; cleared after a timeout |

All cryptography comes from the operating system (Windows CNG / `bcrypt.dll`); there is no custom cipher code. KeyKhan never talks to the network.

**Limitations.** While the vault is unlocked, entries are held in the app's memory as ordinary .NET strings, which the garbage collector may copy and cannot reliably wipe — locking the vault releases them. Anyone with administrator access to your running, unlocked session can read your vault; no password manager can prevent that. There is no recovery for a forgotten master password.

### Vault file and sync

The vault is a single file: `%APPDATA%\KeyKhan\vault.keykhan`. Back it up like any other file. To keep it in a synced folder (OneDrive, Dropbox, …), close KeyKhan and edit `VaultPath` in `%APPDATA%\KeyKhan\settings.json`, then move the file there.

File format (little-endian):

```
"KKVT" | u16 version | u8 kdf | u32 iterations | u8 salt length | salt | nonce(12) | ciphertext | tag(16)
```

The ciphertext is the UTF-8 JSON list of entries.

## Project structure

```
src/
  KeyKhan.Native/   C++20 core: PBKDF2, AES-GCM, TOTP and key custody over Windows CNG (C ABI)
  KeyKhan.Vault/    .NET library: file format, vault, generator, search, CSV import/export
  KeyKhan.App/      WPF application: MVVM views, global hotkey, tray, clipboard, auto-lock, auto-type
tests/
  KeyKhan.Vault.Tests/   xUnit tests, including RFC 6238 TOTP vectors and tamper detection
```

## License

[MIT](LICENSE)
