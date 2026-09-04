# Architecture

The application intentionally uses one executable, one G HUB connection, and one settings document. Selecting multiple devices creates multiple notification-area icons inside the same process.

## Components

- `GHubClient` owns the WebSocket, correlates request IDs, parses broadcasts, and ignores unrelated messages.
- `GHubMonitorService` manages discovery, polling, subscriptions, retry delays, and connection state.
- `DeviceStateStore` is the thread-safe source of current device and battery state.
- `SettingsService` performs schema-versioned, atomic JSON persistence.
- `TrayIconService` maps selected devices to independent `NotifyIcon` instances.
- `TrayIconRenderer` creates the cropped-mouse and battery composition and releases native handles.
- `SettingsViewModel` exposes device selection, connection state, refresh, and startup registration.
- `SingleInstanceCoordinator` redirects subsequent launches to the existing settings window.

Background services publish state through `DeviceStateStore`. WPF and notification-area updates are marshalled to the application dispatcher.

## State semantics

A battery reading has two independent dimensions:

- `IsPresent` indicates that the device was returned by the latest discovery call.
- `IsLive` indicates that the battery value was successfully refreshed during the current connection.

When G HUB disconnects or a device sleeps, the last reading remains available with `IsLive` set to `false`. The tray icon remains identical to the last live icon, while the tooltip includes the time of that reading.

G HUB device-state broadcasts request an immediate refresh when hardware arrives, reconnects, wakes, or changes state. A 45-second poll remains active as a fallback for missed or unsupported broadcasts.

## Persistence

Device selection uses G HUB's device signature when available. The transient `dev########` identifier is used only for requests made during the current G HUB session.

Settings writes use a temporary file followed by an atomic replacement. Invalid JSON is copied to a timestamped backup before defaults are loaded.

## Dependency policy

Runtime dependencies are limited to Microsoft-maintained packages:

- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.Hosting`

The tray implementation, WebSocket transport, JSON parser, drawing code, registry integration, and single-instance coordination use APIs from the .NET Windows Desktop runtime.
