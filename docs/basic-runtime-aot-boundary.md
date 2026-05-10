# BasicRuntime AOT Boundary

This repo keeps the BASIC runtime split into two layers:

## Core

- parser
- evaluator
- language objects
- core built-ins from the .NET base class library

## Optional runtime extensions

- MQTT
- serial port
- Modbus
- PLC helpers
- gateway runtime bridges from `IoTSharp.Edge.RuntimeExtensions`

These extensions are enabled by default in the normal build, but they are removed when the build passes `-MustAot`.

The Edge host can inject those optional capabilities into `BasicRuntime` at startup when `EdgeEnableBasicRuntimeExtensions=true`.

## Build flags

- `EdgeAot=true`
- `EdgeEnableBasicRuntimeExtensions=false`

The `build.ps1` script sets both flags for AOT builds.

## Usage

```powershell
pwsh ./build.ps1 -Action Build
pwsh ./build.ps1 -Action Test
pwsh ./build.ps1 -Action Publish -MustAot
```

When `-MustAot` is present, the BASIC runtime will not compile or register the optional extension modules, so they do not enter the AOT dependency graph.
