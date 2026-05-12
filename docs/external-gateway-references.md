# External Gateway Reference Notes

This note captures anonymized findings from two external industrial gateway codebases while evaluating OPC UA, OPC DA, MTConnect / MT CNC, Fanuc CNC, and related industrial protocols for IoTEdge.

The source products are intentionally not named here. Treat this document as a technical dependency and design reference only.

## Reference Shapes

| Reference | Snapshot type | License observed | Best use |
| --- | --- | --- | --- |
| Reference A | Package-oriented gateway runtime | Apache-2.0 | Plugin isolation, channel/device/variable runtime design, task lifecycle |
| Reference B | Source-oriented gateway runtime | MIT | Concrete protocol driver implementation patterns |

Reference A is most useful as an architecture reference. It wires many protocol capabilities as plugin packages and loads them from isolated plugin folders.

Reference B is more useful as a protocol implementation reference. Its driver tree contains concrete source for OPC UA, OPC DA, MTConnect, Fanuc FOCAS, and several PLC protocols.

## Protocol Dependency Matrix

| Area | Reference A | Reference B | Practical take for IoTEdge |
| --- | --- | --- | --- |
| OPC UA | Packaged OPC UA plugin | `OPCFoundation.NetStandard.Opc.Ua.Client` `1.4.370.12` with a local helper wrapper | Stay with OPC Foundation packages. Prefer our current direct adapter over copying an older helper wrapper. |
| OPC DA | Packaged OPC DA plugin | Local COM automation interop: `Interop.OPCAutomation.dll`, no NuGet package | Keep OPC DA Windows-only and isolated. A package such as `TitaniumAS.Opc.Client` is cleaner than hard-coding Automation COM in the cross-platform gateway. |
| MTConnect / MT CNC | No directly visible open-source driver in the inspected snapshot | `opennetcf-mtconnect-client` `1.0.17160`; reads by data item ID using `EntityClient.GetDataItemById(address).Value` | Our HTTP/XML `/current` reader is enough for simple reads. Consider a package only if we need full probe/assets/device traversal or richer typed models. |
| Fanuc CNC | No directly visible Fanuc plugin in the inspected snapshot | Two implementations: direct FOCAS P/Invoke (`fwlib32.cs`, `fwlib64.cs`) and `HslCommunication` `12.3.0` `FanucSeries0i` | Keep Fanuc as a future optional adapter because native FOCAS runtime, bitness, licensing, and machine validation are unavoidable. |
| Allen-Bradley / CIP | Packaged CIP plugin | `IoTClient` `1.0.42` in an Allen-Bradley PLC driver | IoTClient is a reasonable path for basic PLC expansion; a richer CIP boundary may be useful later. |
| Melsec / Omron | Packaged Melsec and Omron plugins | `IoTClient` `1.0.42` in Mitsubishi and Omron drivers | Our IoTClient-based direction is aligned with this reference. |
| Siemens S7 | Packaged Siemens S7 plugin | Vendored S7.Net-style source | Avoid vendoring protocol libraries unless we need exact behavior. |
| Modbus | Packaged Modbus plugin | Vendored NModbus4-style source | Avoid vendoring NModbus4 when our existing Modbus path works. |

## Reference A Findings

Core gateway application dependencies:

| Package | Role |
| --- | --- |
| `Portable.BouncyCastle` | Security / certificate support |
| `Riok.Mapperly` | Source-generated mapping |
| `Rougamo.Fody` | AOP weaving |
| `TouchSocket.Dmtp` | Communication layer |
| `TouchSocket.WebApi` | Web API communication layer |
| CSScript-style package | Runtime script support |
| Foundation/admin/runtime packages | Gateway runtime, UI, and management support |
| `System.Linq.Async` | Async LINQ support on .NET 8 |

Open plugin packages wired by targets include these protocol areas:

| Area | Notes |
| --- | --- |
| Modbus | Packaged plugin |
| Siemens S7 | Packaged plugin |
| DLT645 | Packaged plugin |
| OPC DA | Packaged plugin |
| OPC UA | Packaged plugin |
| DB | Packaged plugin |
| Kafka | Packaged plugin |
| MQTT | Packaged plugin |
| RabbitMQ | Packaged plugin |
| HTTP / webhook | Packaged plugin |

Additional packaged plugin areas include Allen-Bradley CIP, BACnet, DCON, HJ212, IEC104, Inovance, Melsec, Omron, OPC AE, SECS, and other vendor-specific protocols.

Architectural references worth borrowing conceptually:

- Plugin isolation: packages copy their content into a plugin folder and are loaded via collectible `AssemblyLoadContext`.
- Runtime model: channel, device, and variable runtimes are first-class objects.
- Threading model: channel lifecycle and device task lifecycle are separated.
- Driver metadata: driver properties, variable address UI, dynamic method metadata, and plugin property editor items are surfaced through a plugin service.
- Restart/remove paths: channel and device restarts dispose old runtime state before creating new driver instances.

Reference A is not a strong source for direct protocol code in the inspected snapshot because most protocol implementations are packaged assemblies rather than visible source.

## Reference B Findings

Driver plugin projects:

| Driver area | Protocol area | NuGet / code dependency |
| --- | --- | --- |
| OPC UA client | OPC UA | `OPCFoundation.NetStandard.Opc.Ua.Client` `1.4.370.12` plus a local helper wrapper |
| OPC DA client | OPC DA | Local `Interop.OPCAutomation.dll` COM automation wrapper |
| CNC MTConnect | MTConnect | `opennetcf-mtconnect-client` `1.0.17160` |
| CNC Fanuc direct | Fanuc CNC | Direct P/Invoke wrappers in `fwlib32.cs` and `fwlib64.cs` |
| CNC Fanuc high-level | Fanuc CNC | `HslCommunication` `12.3.0`, `Newtonsoft.Json` `13.0.3`, `System.IO.Ports` `8.0.0` |
| Allen-Bradley PLC | Allen-Bradley | `IoTClient` `1.0.42`, `System.IO.Ports` `8.0.0` |
| Mitsubishi Melsec PLC | Mitsubishi Melsec | `IoTClient` `1.0.42`, `System.IO.Ports` `8.0.0` |
| Omron FINS PLC | Omron FINS | `IoTClient` `1.0.42`, `System.IO.Ports` `8.0.0` |
| Modbus master | Modbus | Vendored Modbus source plus `System.IO.Ports` `8.0.0` |
| Siemens S7 | Siemens S7 | Vendored S7.Net-style source |
| TCP / serial devices | TCP / serial devices | `SimpleTCP.Core` `1.0.4`, and `System.IO.Ports` where needed |

Driver interface pattern:

- Driver classes implement a small `IDriver` interface.
- A driver exposes `DeviceId`, `IsConnected`, `Timeout`, `MinPeriod`, `Connect()`, `Close()`, `Read(...)`, and `WriteAsync(...)`.
- Attributes describe metadata:
  - `[DriverSupported(...)]`
  - `[DriverInfo(...)]`
  - `[ConfigParameter(...)]`
  - `[Method(...)]`
- Reads return a model with `Value`, `Message`, `Timestamp`, and a status enum.

OPC UA details:

- Uses `OPCFoundation.NetStandard.Opc.Ua.Client` `1.4.370.12`.
- The driver creates a helper client, connects to `Uri`, and reads `NodeId` values.
- The helper auto-accepts untrusted certificates and implements session keep-alive / reconnect behavior.
- Useful reference: reconnection and certificate handling shape.
- Avoid copying wholesale: package version and helper style are older than our current direct adapter direction.

OPC DA details:

- The project references `Interop.OPCAutomation.dll`.
- The wrapper creates `OPCAutomation.OPCServer`, connects by `serverIP` and `OPCServerName`, creates a group, and can browse branches/leaves and read item values.
- The driver reads string values and parses them according to requested data type.
- Useful reference: Windows COM/DCOM behavior and browse/read shape.
- Avoid direct reuse in the cross-platform gateway package.

MTConnect details:

- Uses `OpenNETCF.MTConnect.EntityClient`.
- It configures `Uri`, `Timeout`, `MinPeriod`, and reads a point with `GetDataItemById(ioArg.Address).Value`.
- Useful reference: exposing MTConnect addresses as data item IDs.
- Our current lightweight HTTP/XML implementation is simpler and dependency-light for `/current` reads.

Fanuc details:

- The direct driver connects with `cnc_allclibhndl3(ip, port, timeout, out handle)` and closes with `cnc_freelibhndl(handle)`.
- It checks connection with `cnc_sysinfo`.
- It exposes many read methods using FOCAS calls, including system info, run/detail status, operation mode, macro variables, PMC registers/strings, spindle speed, feed rate, spindle/feed override, alarms, program number, block counter, tool number, timers, and spindle load.
- The high-level version uses `HslCommunication.CNC.Fanuc.FanucSeries0i` and exposes reads such as status, alarm, coordinates, program list/current program, spindle/feed, axis load, and cutter info.
- Useful reference: the direct FOCAS method surface and the HslCommunication high-level shape.
- Avoid enabling by default until native runtime deployment, licensing, bitness, and real-machine validation are handled.

## Adoption Guidance

Recommended:

- Keep OPC UA on official OPC Foundation packages and evolve the current direct adapter.
- Keep OPC DA as a Windows-only optional adapter. Prefer a clean package boundary or an OPC DA to OPC UA bridge over embedding COM interop in the shared infrastructure assembly.
- Keep MTConnect lightweight for simple `current` reads. Add a richer MTConnect client only when we need probe/assets/devices/history or more complete typed models.
- Use the direct Fanuc FOCAS method surface as the primary reference for a future optional Fanuc adapter.
- Consider borrowing the attribute-driven metadata idea for future optional drivers, but align it with IoTEdge's current driver catalog and configuration contracts.
- Consider borrowing the plugin isolation shape if IoTEdge needs third-party driver drop-ins later.

Not recommended:

- Do not vendor old OPC UA helper code when the official packages already provide the needed primitives.
- Do not hard-reference `Interop.OPCAutomation.dll` from the cross-platform project.
- Do not vendor NModbus4 or S7.Net-style source unless the existing dependencies cannot meet a concrete requirement.
- Do not make Fanuc a built-in dependency in the default runtime.

## Suggested Next Steps

1. Add a design note for optional native / Windows driver assemblies: load boundary, deployment manifest, health check, and failure isolation.
2. For Fanuc, define an address/method map before coding: status, mode, alarm, program, spindle/feed, macro, PMC, timer, and load.
3. For OPC DA, decide between a Windows adapter and recommending an OPC DA to OPC UA bridge.
4. For MTConnect, add optional `probe` support only if users need discovery rather than configured data item IDs.
