# BetterTerminal

A Windows terminal that opens on the folder you are working in.

Type `beterm` at any command prompt and BetterTerminal opens there, treats that folder as a project,
and remembers what you set up for it. Command Prompt and Windows PowerShell run in tabs and splits
in one window, with your own commands, your saved remote connections and your layout kept between
runs.

![The application with a project open](docs/images/betterterminal.png)

Built for .NET Framework 4.8 with WPF and direct Win32 interop. No third-party terminal control and
**no NuGet packages at all** - a clean clone builds with MSBuild alone, offline.

## Install

1. Download `BetterTerminal-x64.zip` from the [latest release](../../releases/latest) and unpack it
   anywhere.
2. Run `BetterTerminal.exe` once.

The first run copies itself to `%LOCALAPPDATA%\BetterTerminal\app` and registers the `beterm`
command - no installer, no administrator rights, nothing written outside your user profile. Open a
**new** prompt afterwards, because a prompt only reads the search path when it starts.

To remove it: delete `%LOCALAPPDATA%\BetterTerminal` and `%APPDATA%\BetterTerminal`.

Needs 64-bit Windows and .NET Framework 4.8, which is already on Windows 10 and 11. Windows 10 build
17763 or newer gets the full terminal; older builds fall back to a hosted console window.

## What you get

**Open a folder as a project.** `beterm` in any folder opens the application there. The folder gets
a hidden `.beterm` folder holding its settings: a name, which shell to use, a line to run when it
opens, commands you define yourself, and any values you want to keep with the project. Your own
commands appear in the command palette and run in the focused session.

**A session that tells you where you are.** Each pane opens on a short banner naming the workspace,
the project, the shell and the machine, and the prompt reads `MACHINE /project/folder >>`.

**Saved connections.** The SSH button keeps an address book of `user@address` entries, stored per
user rather than per project. Each row shows a heart: filled and green when the host answers on the
standard port, an outline in red when it does not. Pick one and it opens a session with the
connection line already typed, either in the pane grid or in a window of its own.

**Tabs and splits.** Split any pane right or down, as deep as you like, with draggable dividers.
Every pane has its own shell and its own working directory.

**A command palette** on `Ctrl+Shift+P`, holding every layout command and your project's commands.

**Your layout survives a restart.** Tabs, the split tree, ratios, shell choice, working directories,
theme, colour scheme, font and window position are restored the next time you start.

**A real terminal underneath.** Full colour including truecolour, scrollback with selection, copy
and paste, `Ctrl+wheel` zoom, and the alternate screen buffer - so an editor or a pager draws
correctly and leaves your scrollback intact. When a shell exits, the pane says so and gives you the
exit code instead of going blank.

### Keyboard

| Shortcut | Action |
| --- | --- |
| `Ctrl+Shift+T` | New tab |
| `Alt+Shift+Plus` | Split right |
| `Alt+Shift+Minus` | Split down |
| `Ctrl+Shift+W` | Close the focused pane |
| `Alt+Right` / `Alt+Left` | Focus the next / previous pane |
| `Ctrl+Shift+P` | Command palette |
| `Ctrl+comma` | Settings |
| `Ctrl+Shift+C` / `Ctrl+Shift+V` | Copy / paste |
| `Shift+PageUp` / `Shift+PageDown` | Scroll back / forward |
| `Ctrl+wheel` | Font size |

## Also in the box

Two small console programs ship beside the application:

- **`beterm-banner.exe`** writes the banner a session opens on. The shell runs it as its first
  command; it is installed next to the `beterm` command.
- **`beterm-wrap.exe`** is a text interface for the PowerShell scripts in `tools\`: pick one, fill
  in its parameters, watch its output and see the exit code it really returned.

## Build it yourself

You need Visual Studio 2022 or newer with the .NET desktop workload, or any MSBuild that can build
.NET Framework 4.8 projects. There is no restore step.

```
MSBuild.exe BetterTerminal.sln /t:Rebuild /p:Configuration=Release /p:Platform=x64
```

The application lands in `BetterTerminal.Shell\bin\x64\Release\BetterTerminal.exe`. `x64` is the only
platform the solution defines. A build is expected to finish with **zero errors and zero warnings**;
that is what the [build workflow](.github/workflows/build.yml) enforces on every push.

## How it is put together

| Project | What it is |
| --- | --- |
| `BetterTerminal.Interop` | Every P/Invoke, SafeHandle and Win32 struct. Nothing else in the solution declares one. |
| `BetterTerminal.Terminal` | The session contract, both backends, the escape-sequence parser, the cell grid and the renderer. |
| `BetterTerminal.Shell` | The application: window, tabs, splits, panes, palette, settings, persistence. |
| `BetterTerminal.Wrap` | `beterm-wrap.exe`, the text interface for the verification scripts. |
| `BetterTerminal.Banner` | `beterm-banner.exe`, the session banner. |

Deeper documentation lives at the repository root: [STRUCTURE.md](STRUCTURE.md) for the code map,
[WORKFLOWS.md](WORKFLOWS.md) for runnable procedures, [RULES.md](RULES.md) for the constraints the
code is held to, [TIPS.md](TIPS.md) for the traps that cost time once already, and
[MEMORY.md](MEMORY.md) for why things are the way they are.

## Where your data goes

| Path | What is in it |
| --- | --- |
| `%APPDATA%\BetterTerminal\workspace.json` | Tabs, splits, appearance, window position. |
| `%APPDATA%\BetterTerminal\connections.json` | Saved connections - a user name and an address each, **never a password**. |
| `<project>\.beterm\project.json` | That project's settings, commands and values. Plain text you may commit. |
| `%LOCALAPPDATA%\BetterTerminal\` | The installed copy and the `beterm` command. |

The application makes exactly one kind of network call: a connection to port 22 of a host you saved,
to decide whether its heart is green. It sends nothing, reads nothing and stores no result. There is
no telemetry.

## Honest gaps

- **No test project.** Verification is a warning-clean rebuild plus the scripts in `tools\`.
- **Not code signed**, so Windows will warn the first time you run it.
- Full-screen console applications, a DPI change while running and input latency have not been
  checked by hand.
- The relative `/project/folder` prompt works in Windows PowerShell. Command Prompt shows the full
  path instead: its `PROMPT` understands tokens, not expressions, and a prefix that went stale after
  the first `cd` would be worse.

## Licence

MIT. See [LICENSE](LICENSE).
