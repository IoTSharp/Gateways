# IoTEdge Local SPA Prototype

## Goal

Edge must be able to manage its own collection configuration and SonnetDB upload target when no upstream IoTSharp platform is available. If the upstream platform later pushes configuration, Edge should cache the latest configuration locally and continue running from that local copy while offline.

## Reference Direction

The embedded SPA follows the gateway console style used by industrial IoT gateway projects such as:

- https://github.com/iioter/iotgateway
- https://gitee.com/ThingsGateway/ThingsGateway

The useful pattern is not a marketing landing page. It is a dense operator console: left navigation, runtime status strip, compact metric cards, editable tables, connection forms, script viewer, and live logs.

## Information Architecture

### Runtime

Shows whether the edge runtime is running in local mode, upstream-managed mode, or degraded offline mode.

Primary signals:

- runtime health
- collection sync status
- local configuration file path and timestamp
- enabled channel/device/point/task/upload counts
- upstream base URL and token presence

### Collection Topology

Shows the runtime execution graph:

- channels
- devices
- points
- polling tasks
- transform rules
- upload channels
- upload routes

First implementation presents a compact topology summary, a JSON editor, and a structured Modbus connection/point editor. Modbus now covers TCP, RTU over TCP, and serial DTU/RTU style collection. Later iterations should expand the structured editor to every supported protocol and route type.

### Upload Targets

SonnetDB is a first-class local target.

The first screen should surface:

- protocol: `SonnetDb`
- endpoint
- database
- token presence
- measurement
- field
- site tag

The operator should be able to edit the local configuration and immediately apply it to the runtime.

### Script

Shows the BASIC polling script actually used by the runtime. It is read-only in the first prototype.

### Logs

Shows local runtime logs from the Edge API, filtered to operational levels first. It should refresh without navigating away.

## First Prototype Layout

```text
+---------------------------------------------------------------------+
| IoTEdge Console      Local Mode | Running | Last applied ...  |
+----------------------+----------------------------------------------+
| Dashboard            | Runtime cards                                |
| Topology             | - Health                                     |
| Upload Targets       | - Local config                               |
| BASIC Script         | - Collection counts                          |
| Logs                 | - SonnetDB target                            |
| Bootstrap            |                                              |
|                      | Tabs: Overview | Local JSON | Script | Logs  |
|                      |                                              |
|                      | [Topology summary table]                     |
|                      | [SonnetDB settings summary]                  |
|                      | [Local configuration JSON editor]            |
+----------------------+----------------------------------------------+
```

## API Shape

The SPA bundled with `src/IoTEdge` talks to the local APIs through HTTP.

Required first-pass APIs:

- `GET /api/diagnostics/summary`
- `GET /api/local/configuration`
- `PUT /api/local/configuration`
- `POST /api/local/configuration/apply`
- `POST /api/local/configuration/reset`
- `GET /api/scripts/polling`
- `GET /api/diagnostics/logs`

## Offline Behavior

1. On startup, Edge initializes its schema.
2. Edge loads `/data/local-collection.json` if configured, or creates an empty local collection document.
3. Edge applies the local collection document to the execution cache when local configuration is enabled.
4. If upstream configuration is enabled and reachable, upstream data can replace the local execution cache.
5. Every successful upstream configuration pull is cached locally.
6. If the upstream platform is later unreachable, the last local configuration remains active.

## Implementation Phases

### Phase 1

- Add local configuration file store in `src/IoTEdge`.
- Add APIs to read, save, apply, and reset the local configuration.
- Embed the Vue SPA in `src/VueClient`, host it from `src/IoTEdge`, and use SpaProxy in development.
- Remove `samples/IoTEdge.MockPlatform`.
- Update Docker Compose to run Edge, Device Simulator, and SonnetDB only.

### Phase 2

- Expand structured editors for channels, devices, points, tasks, and routes across all supported protocols.
- Add SonnetDB connection test.
- Add import/export for local configuration profiles.
- Add upstream/local conflict visualization.

### Phase 3

- Add role-based access.
- Add version history and rollback.
- Add point-level live values and upload verification views.
