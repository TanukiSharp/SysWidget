# SysWidget

A **single** widget showing CPU, RAM and network — not three separate systray icons.
WPF, .NET 10, MVVM without a framework.

![preview](docs/preview.png)

```
CPU ▮ 24%   │   RAM ▮ 74% · 47G   │   Net ↓61K ↑116K
```

CPU and RAM show a vertical green-to-red bar next to the percentage; network shows the
download/upload rate. The window is borderless, always-on-top, out of Alt-Tab, and draggable
with the left mouse button (its position is remembered).

## Staying on top

The widget re-asserts its topmost position whenever the foreground window changes (via a
`SetWinEventHook` on `EVENT_SYSTEM_FOREGROUND`), so opening the taskbar overflow flyout (the
"^" tray) or another topmost window can't leave it buried. This is event-driven — no polling.

## Grow-only layout

Each component's cell only ever grows, never shrinks, so the widget stabilizes to a steady
width and stops jittering as the numbers change. If a one-off spike (or a bug) leaves it stuck
too wide, use **Reset size** in the menu to let it re-measure from the current content.

## Menu

Right-click the widget (or the tray icon):

- **Components** — enable/disable CPU, RAM, network individually
- **Dark theme** — toggle dark/light
- **Start with Windows** — toggle the `HKCU\...\Run` entry
- **Reset size** — clear the grow-only width ratchet
- **Reset position** — snap the window back to the top-right (recovery for when another app,
  e.g. Windows Magnifier, relocates or buries it)
- **Quit**

## Components

CPU, RAM and network are interchangeable **components**. Each implements `IWidgetComponent`
(see `Components/`) and declares a `ComponentKind` (`Text` or `Gauge`) that selects how it is
rendered.

**Adding a component** (e.g. a virtual-desktop number):

1. Create `Components/MyComponent.cs` deriving from `WidgetComponentBase`
   (poll model via `Sample()`, or push model by calling `SetValue()` from a watcher — e.g.
   `RegNotifyChangeKeyValue`, like the `DesktopWatcher` in the VirtualDesktopNumberIndicator project).
2. Add one line to `Components/ComponentCatalog.cs`.

The UI, the menu and the settings pick it up automatically.

## Architecture (MVVM, no framework)

```
Components/     IWidgetComponent + WidgetComponentBase + Cpu/Ram/Net + ComponentCatalog + ComponentHost
ViewModels/     ViewModelBase (SetValue -> PropertyChanged), WidgetViewModel,
                ComponentViewModel / GaugeComponentViewModel, RelayCommand
Behaviors/      WidgetPlacement (drag + position, data-bound attached properties),
                RatchetWidth (grow-only width + reset)
Converters/     separator visibility, fraction -> gauge color, fraction -> fill height
Interop/        NativeMethods (P/Invoke for the tool-window style)
Services/       TrayIcon (single NotifyIcon), StartupManager (start with Windows)
Settings/       AppSettings + SettingsStore (%AppData%\SysWidget\settings.json)
WidgetWindow    borderless window — no code-behind logic; everything via DataBinding + DataTemplate
```

MVVM rule honored: no window/control is hard-wired to a view model through UI references or
events. Everything goes through DataBinding; components are rendered by DataTemplate (selected on
the `ComponentViewModel` subtype); the placement interop is encapsulated in an attached behavior
driven by bound properties.

## Build & run

```powershell
dotnet build src\SysWidget\SysWidget.csproj -o obj\buildcheck -warnaserror   # clean build, 0 warnings
dotnet run   --project src\SysWidget\SysWidget.csproj
```

Target: `net10.0-windows` (WPF + WinForms for the tray NotifyIcon only).

## Settings (`%AppData%\SysWidget\settings.json`)

`Theme` (Dark/Light), `Opacity`, `ActiveComponents` (ordered list of ids),
`WindowLeft/Top`, `StartWithWindows`.
