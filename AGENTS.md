# VolumeKeeper Agent Guide

## Working rules

- Make the smallest change that fully addresses the request. Do not refactor or fix unrelated code.
- Inspect the relevant implementation before relying on this guide. Preserve the existing code-behind and service patterns unless the task calls for an architectural change.
- Keep UI work on the WinUI dispatcher and potentially blocking work off the UI thread. Follow the existing disposal and debounce patterns around Core Audio resources.
- Log operational failures with a named logger and enough context to identify the failed operation. Silent catches are reserved for best-effort probing and cleanup where the surrounding code already treats failure as harmless.
- Prefer clear names over comments. Add a short comment only for a non-obvious Windows, WinUI, or Core Audio constraint.

## Project

VolumeKeeper is an unpackaged Windows desktop application that pins volume levels by executable path and enforces them on active Windows Core Audio sessions.

The application targets `net9.0-windows10.0.19041.0` with nullable reference types and WinUI 3. Debug uses the `Exe` output type without a self-contained Windows App SDK; Release uses `WinExe` with a self-contained Windows App SDK. The project declares win-x86, win-x64, and win-arm64 runtime identifiers. GitHub releases publish self-contained x86 and x64 portable archives and Inno Setup installers.

Direct package references are Microsoft Windows App SDK 1.8.251003001, Microsoft Windows SDK BuildTools 10.0.26100.6901, NAudio 2.2.1, H.NotifyIcon.WinUI 2.3.2, NLog 6.0.5, TraceEvent 3.1.28, and System.Management 9.0.10. Package restore is locked through `VolumeKeeper/packages.lock.json`.

## Runtime structure

- `App.xaml.cs` owns startup, single-instance activation, service construction, the tray icon, and shutdown. Services are exposed through static `App` properties rather than a dependency-injection container. A second instance redirects activation to the first; `--minimized` starts without showing the window.
- `MainWindow.xaml.cs` hosts a top `NavigationView` for `HomePage` and `LogsPage`. Closing the window hides it instead of exiting. Its size, position, and maximized state are persisted, and an off-screen window is recentered when shown.
- `HomePage.xaml.cs` is code-behind for the live session list, refresh command, auto-restore toggle, volume slider, mute control, and pin control.
- `LogsPage.xaml.cs` is code-behind for the in-memory activity feed, clear command, and persisted auto-scroll preference.
- `AudioSessionManager` enumerates sessions from every active render endpoint through NAudio. It refreshes on endpoint changes, default multimedia render-device changes, new sessions, manual refreshes, disconnections, and expired sessions.
- Session controls are grouped by process ID across devices. One `ObservableAudioSession` represents a process and writes volume or mute changes to every grouped control for that process. Collection updates find sessions by process ID, while pinned settings are keyed by executable path.
- `AudioSessionService` applies debounced slider changes, immediate mute changes, and bulk restoration when auto-restore is enabled.
- `IconService` extracts and caches application icons. `VolumeSettingsManager` and `WindowSettingsManager` own JSON persistence. `FileLogger` writes through NLog and mirrors entries into the Logs page.

This is a code-behind application with observable models and service classes. It has no ViewModels, repositories, process-launch watcher, or general background-host framework. Application arrival is detected through Core Audio session creation and refreshes.

## Volume behavior

- A pin is identified only by the executable's full path. Equality and hashing are case-insensitive, so separate installations at different paths can have different pins. Process ID, display name, icon, window title, command line, and file contents are not part of the key.
- Clicking Pin saves the current integer volume from 0 through 100. Clicking it again at the same volume removes the pin. If the current volume differs from the saved value, clicking Pin updates the pin.
- Moving VolumeKeeper's slider for a pinned application updates the saved value before applying the session volume. Changes made in Windows Volume Mixer or another program do not update the saved pin.
- With auto-restore enabled, session creation, refresh, or an external volume-change event restores a pinned session when its live volume differs. Re-enabling auto-restore also restores all currently listed pinned sessions. Program-originated changes use a short suppression window to avoid being mistaken for external changes.
- Volume and mute writes resolve the first listed session with the matching executable path. Within that process, the write is applied to all of its session controls. Do not assume one operation updates every running process that shares the same executable path.
- Mute state is not persisted. Setting a nonzero volume through the observable session unmutes it.

## Persistence and logging

User data is under `%APPDATA%\VolumeKeeper`:

- `configs/volume_settings.json` stores `ApplicationVolumes` as objects containing a full-path `Id` and nullable integer `Volume`, plus `AutoRestoreEnabled`, `AutoScrollLogsEnabled`, and `LastUpdated`. The manager writes the live values and preferences with a two-second debounce. Each save emits `LastUpdated` from the model's `DateTime.Now` default rather than retaining the loaded value.
- `configs/window_settings.json` stores settings by `WindowId`; only `Main` exists. Window writes use a two-second debounce.
- `logs/volumekeeper.log` receives Debug and higher entries. NLog archives daily, above 10 MiB, and on startup, retaining at most 30 archives for at most seven days.

The Logs page keeps at most 1,000 newest-first entries in memory and suppresses adjacent duplicate level/message/detail entries within one second. Clearing the page does not delete log files. The installer asks whether to delete `%APPDATA%\VolumeKeeper` after uninstalling.

Use `App.Logger.Named()` for the normal class logger. The available methods are `Debug`, `Info`, `Warn`, and `Error`, each accepting a message and optional exception or source.

## Build and packaging

Run commands from the repository root:

```powershell
dotnet restore --locked-mode
dotnet build
dotnet run --project VolumeKeeper
dotnet publish VolumeKeeper/VolumeKeeper.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishReadyToRun=true
```

There is one application project and no test project. `scripts/rebuild_app.bat` publishes win-x64 locally. `scripts/inno.iss` is the CI installer template; `scripts/inno_dev.iss` is used by `scripts/rebuild_installer.bat`. Installers request administrator privileges and can create desktop and startup shortcuts, with the startup shortcut passing `--minimized`.

## Change checks

- Build the affected configuration and runtime when practical. Use `dotnet build` as the minimum validation for ordinary C# or XAML changes.
- Check Core Audio callbacks, `DispatcherQueue` access, cancellation, and disposal when changing session or device code.
- Keep persisted JSON compatible unless a migration is part of the task.
- Keep `VolumeKeeper.csproj`, `packages.lock.json`, GitHub workflows, and Inno templates aligned when changing versions, dependencies, runtimes, or release output.
