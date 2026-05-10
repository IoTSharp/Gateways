# Gateway Driver Components

This note records the driver component choices for the expanded southbound gateway surface.

| Target | Component choice | Status in this repo | Runtime boundary | Notes |
| --- | --- | --- | --- | --- |
| OPC UA | OPC Foundation UA .NET Standard (`OPCFoundation.NetStandard.Opc.Ua.Client` and `OPCFoundation.NetStandard.Opc.Ua.Configuration`) | Implemented as `opc-ua` | Cross-platform .NET 8 | Supports scalar node read/write and batch read/write through the unified driver contract. Addresses are OPC UA NodeIds such as `ns=2;s=Device.Temperature`. |
| OPC DA | Optional Windows adapter, candidate `TitaniumAS.Opc.Client` | Registered as guarded `opc-da` placeholder | Windows COM/DCOM only | Keep out of the cross-platform gateway package unless we split a Windows-only adapter. A safer deployment path is bridging OPC DA to OPC UA. |
| MT CNC | MTConnect HTTP/XML current endpoint | Implemented as `mt-cnc` | Cross-platform HTTP | Treats "MT CNC" as MTConnect. Reads data items from `/current`; writes are rejected because MTConnect current is read-only. |
| Fanuc CNC | Fanuc FOCAS (`fwlib32`/`fwlib64`) native SDK boundary | Registered as guarded `fanuc-cnc` placeholder | Native vendor runtime | Requires Fanuc FOCAS licensing/runtime, bitness handling, and machine validation. It should live in an optional adapter assembly. |

## Connection Shapes

`opc-ua`:

- `endpoint`: `opc.tcp://host:4840`
- `useSecurity`: `true` or `false`
- `securityMode`: `Auto`, `None`, `Sign`, `SignAndEncrypt`
- `securityPolicy`: `Auto`, `None`, `Basic256Sha256`, `Aes128_Sha256_RsaOaep`, `Aes256_Sha256_RsaPss`
- `username` / `password`: optional user identity
- `timeout`, `sessionTimeout`, `autoAcceptUntrustedCertificates`

`mt-cnc`:

- `baseUrl`: MTConnect agent base URL
- `device`: optional device path segment
- `path`: defaults to `current`
- `timeout`: HTTP timeout in milliseconds

`opc-da` and `fanuc-cnc` are intentionally explicit placeholders. Their metadata is visible in the driver catalog so the platform can model them, but runtime access returns a clear error until a Windows/native adapter is added.
