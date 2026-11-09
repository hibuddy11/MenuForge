# MenuForge

A lightweight Windows utility for customizing the File Explorer context menu. Add, edit and remove your own menu entries — with full command and argument configuration — without touching the registry by hand.

## Features

- **Add** custom context-menu entries (e.g. "Open with VSCode", "Open Terminal here")
- **Edit / delete** existing user-level entries, including ones created by other applications
- **Argument configuration** with Explorer placeholders (`%1`, `%V`, `%L`, `%W`) and a one-click insert box
- **Scope control** — show an entry on:
  - all files
  - folders
  - folder background (right-click on empty space inside a folder)
  - drives
  - specific file types (e.g. `.txt`, `.md`)
- Optional icon and "Shift-only" mode (entry appears only while Shift is held)
- Writes only to `HKCU\Software\Classes` — **no administrator rights required**
- Changes are applied to Explorer immediately (no reboot, no restart)

## Placeholders

Explorer replaces these in the command line before the program starts:

| Placeholder | Replaced by |
|---|---|
| `%1` | Path of the selected file or folder |
| `%V` | Selected folder — or the current folder when used on the folder background |
| `%L` | Long path version of `%1` |
| `%W` | Working directory |

Example: Command = `C:\Windows\System32\cmd.exe`, Arguments = `/k cd /d "%V"` gives you an "Open Terminal here" entry.

> **Windows 11:** classic registry entries appear in the expanded menu — hold Shift while right-clicking, or use "Show more options".

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```
dotnet build -c Release
```

Single project, no external dependencies. To run:

```
dotnet run
```

## How it works

MenuForge manages standard shell verb registrations under `HKEY_CURRENT_USER\Software\Classes`:

| Location | Registry key |
|---|---|
| All files | `*\shell\<id>\command` |
| Folders | `Directory\shell\<id>\command` |
| Folder background | `Directory\Background\shell\<id>\command` |
| Drives | `Drive\shell\<id>\command` |
| Specific file types | `SystemFileAssociations\<ext>\shell\<id>\command` |

Deleting an entry in MenuForge removes its registry keys. Entries created by other applications are listed too, so they can be removed the same way (only user-level entries are shown; system-wide entries under `HKLM` are never touched).

## License

[MIT](LICENSE)
