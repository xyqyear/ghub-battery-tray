# G HUB protocol notes

The application uses the internal WebSocket exposed by `lghub_agent.exe`:

```text
ws://127.0.0.1:9010
```

The connection uses the `json` WebSocket subprotocol and an `Origin` header of `file://`.

This is not a documented Logitech SDK. All messages are treated as untrusted input, requests have finite timeouts, and disconnects are expected.

## Discovery

Request:

```json
{
  "msgId": "generated-guid",
  "verb": "GET",
  "path": "/devices/list"
}
```

Battery-capable devices have `payload.deviceInfos[].capabilities.hasBatteryStatus` set to `true`.

The application prefers `deviceSignature` as the persistent key, then falls back to `deviceModel` plus `deviceUnitId`. The `id` field, such as `dev00000000`, is transient.

## Battery state

Request:

```json
{
  "msgId": "generated-guid",
  "verb": "GET",
  "path": "/battery/dev00000000/state"
}
```

Relevant response fields are:

- `percentage`
- `charging`
- `fullyCharged`
- `mileage`

Optional fields are never required for a successful percentage update.

When a wireless device sleeps, G HUB can retain the device in `/devices/list` with state `NOT_CONNECTED` while returning `NO_SUCH_PATH` for its battery endpoint. That response marks an existing value stale; it does not erase the cached percentage.

## Change subscription

Subscription request:

```json
{
  "msgId": "",
  "verb": "SUBSCRIBE",
  "path": "/battery/state/changed"
}
```

Battery changes arrive with verb `BROADCAST`. The subscription is renewed after each broadcast because G HUB versions have exhibited one-shot subscription behavior.

Device connection changes use a second subscription:

```json
{
  "msgId": "",
  "verb": "SUBSCRIBE",
  "path": "/devices/state/changed"
}
```

A device-state broadcast triggers immediate discovery and battery refresh. This subscription is also renewed after each broadcast. Independent 45-second polling remains the fallback when a G HUB version omits or loses a notification.

## Unsolicited messages

G HUB sends an `OPTIONS /` message when a client connects and may send unrelated broadcasts. The client correlates request IDs and paths rather than assuming that the next incoming frame is the response to the most recent request.

## Recovery

The monitor reconnects after `1`, `2`, `5`, `10`, and `30` seconds, with subsequent attempts capped at 30 seconds. Every new connection performs device discovery before issuing battery requests.
