# BasicRuntime AOT Boundary

This repo keeps the BASIC runtime split into two layers:

## Core

- parser
- evaluator
- language objects
- core built-ins from the .NET base class library
- MQTT, serial, Modbus, and PLC built-ins stay compiled into `IoTSharp.Edge.BasicRuntime`

## Optional runtime extensions

- gateway runtime bridges from `IoTSharp.Edge.RuntimeExtensions`

These gateway bridges are enabled by default in the normal build, but they are not referenced when the build passes `-MustAot`.

The Edge host injects those optional gateway bridges into `BasicRuntime` at startup when `EdgeEnableBasicRuntimeExtensions=true`.

## Build flags

- `EdgeAot=true`
- `EdgeEnableBasicRuntimeExtensions=false`

The `build.ps1` script sets both flags for AOT builds. The BASIC runtime itself stays fully compiled either way; only the gateway bridge package is dropped from the AOT graph.

## Usage

```powershell
pwsh ./build.ps1 -Action Build
pwsh ./build.ps1 -Action Test
pwsh ./build.ps1 -Action Publish -MustAot
```

When `-MustAot` is present, the gateway bridge module is not injected, so it does not enter the AOT dependency graph.
