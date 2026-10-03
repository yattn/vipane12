# vipane12

A small 12-pane file manager for Windows 11.

vipane12 keeps multiple working directories visible at once and provides simple Vim-like keyboard navigation. It deliberately avoids becoming a full replacement for Windows Explorer.

## Features

- 12 directory panes in a fixed 4 x 3 layout
- Vim-like navigation inside and between panes
- Copy, move, recycle-bin delete, and permanent delete
- Multiple selection and simple range selection
- External commands defined as plain `.cmd` files
- Workspace paths restored on startup
- No third-party libraries or package manager

## Requirements

- Windows 11
- .NET Framework C# compiler (`csc.exe`)

Visual Studio, MSBuild, the `dotnet` CLI, NuGet, and third-party DLLs are not required.

## Build

Run:

```bat
build.bat
```

The resulting executable is `ViPane12.exe`.

## Basic keys

| Key | Action |
| --- | --- |
| `j` / `k` | Move in the active pane |
| `h` / `l` | Parent / open |
| `Ctrl+h/j/k/l` | Move between panes |
| `yy` / `Ctrl+C` | Copy selection |
| `dd` / `Ctrl+X` | Move selection |
| `p` / `Ctrl+V` | Paste |
| `x` / `Delete` | Move to Recycle Bin |
| `Shift+Delete` | Delete permanently |
| `F5` | Refresh |
| `e` | Edit current path |
| `Esc Esc` | Clear transient selection/pending state |

## Configuration

Without `portable.flag`, workspace data is stored under:

```text
%APPDATA%\vipane12
```

When `portable.flag` exists next to the executable, workspace data and custom commands are stored beside the executable instead.

Custom commands are regular `.cmd` files placed in the `commands` directory. They receive:

```text
%1 = selected path
%2 = current directory
```

See [docs/spec.md](docs/spec.md) for the detailed design and constraints.

## Scope

vipane12 favors a small implementation and explicit behavior. It intentionally does not provide tabs, a tree view, a preview pane, a plugin API, shell context-menu hosting, background transfer queues, or a full Vim emulation layer.

## License

MIT
