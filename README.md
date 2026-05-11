# IoTSharp.Edge

统一的 Gateway 单宿主程序。

当前模式已经收口为一个可执行程序 `IoTSharp.Edge`，Vue SPA 前端源码位于 `src/VueClient`，开发时由宿主通过 SpaProxy 拉起，发布时构建产物拷贝到 `wwwroot`。Gateway 的职责为：

- 读取本地 `bootstrap.json`
- 向 IoTSharp 平台注册、心跳、上报能力
- 从 IoTSharp 平台拉取采集配置
- 维护本地离线采集配置 `local-collection.json`
- 将平台侧采集模型映射为本地执行缓存并执行轮询
- 将采集结果上传到 IoTSharp 或 SonnetDB

## 职责边界

IoTSharp 平台侧可负责：

- 采集模型设计
- 边缘节点管理
- 配置版本管理
- 任务下发与运行观察

Gateway 侧负责：

- 南向协议执行
- 本地执行缓存与离线配置
- 采集轮询与数据上传
- BASIC 采集脚本执行
- Bootstrap、诊断、本地配置 API

当上游平台不可用或未启用时，Edge 可以通过本地配置继续工作；当上游平台成功下发配置时，Edge 会缓存最新配置到本地，离线后仍可沿用。

当 `EdgeEnableBasicRuntimeExtensions=true` 时，宿主会在启动时注入 `IoTSharp.Edge.RuntimeExtensions`，把驱动读取、驱动写入、变换应用和上传桥接能力暴露给 BASIC 脚本；AOT 发布会自动跳过这一层扩展。基础运行时里的 MQTT、串口、Modbus、PLC 能力始终可用。

## 本地管理界面

本地管理界面随 `IoTSharp.Edge` 一起启动，源码位于 `src/VueClient`，通过 HTTP 调用同宿主暴露的本地 API。

当前界面提供：

- 运行态与配置统计
- 按协议目录驱动的采集拓扑结构化编辑
- 按上传协议分组的多目标上传编辑
- IoTSharp、ThingsBoard、SonnetDB、InfluxDB 上传协议页
- 本地 JSON 高级编辑
- BASIC 采集脚本查看
- 运行日志查看

- `GET /api/bootstrap/config`
- `POST /api/bootstrap/config`
- `GET /api/local/configuration`
- `PUT /api/local/configuration`
- `POST /api/local/configuration/apply`
- `POST /api/local/configuration/reset`
- `GET /api/scripts/polling`
- `GET /api/diagnostics/logs`
- `GET /api/diagnostics/summary`
- `GET /api/health`

诊断摘要里会额外显示当前 BasicRuntime 已注入的扩展名称。

## 配置同步

Gateway 会周期性调用：

- `GET /api/Edge/{access_token}/CollectionConfig`

拉取 IoTSharp 平台维护的采集配置，然后将其转换为本地运行时缓存：

- `GatewayChannels`
- `Devices`
- `Points`
- `PollingTasks`
- `TransformRules`
- `UploadChannels`
- `UploadRoutes`

本地 SQLite 现在只是执行缓存，不再是配置主数据源。

如果本次拉取或映射失败，Gateway 会保留上一版本地缓存继续运行，不会清空现有执行配置。

## 上传链路

平台或本地配置映射后，Gateway 会自动派生上传通道：

- 遥测上报到 `POST /api/Devices/{access_token}/Telemetry`
- 属性上报到 `POST /api/Devices/{access_token}/Attributes`
- IoTSharp、ThingsBoard 走 HTTP 上传
- SonnetDB 写入使用 NuGet 包 `SonnetDB` 提供的 ADO.NET provider
- InfluxDB 写入支持 v2 bucket 和 v1 database

本地闭环示例默认将 Modbus 模拟设备数据写入 SonnetDB 的 `metrics.edge_modbus`。

## 目录结构

- `src/IoTSharp.Edge.Domain`
  - 领域模型与统一驱动接口
- `src/IoTSharp.Edge.Application`
  - 运行服务、配置快照、转换服务
- `src/IoTSharp.Edge.Infrastructure`
  - SQLite 持久化、驱动适配、上传通道
- `src/IoTSharp.Edge`
  - 单宿主程序、bootstrap/诊断/本地配置 API、平台同步 worker、Vue SPA 托管入口
- `src/VueClient`
  - 本地管理前端
- `src/IoTSharp.Edge.DeviceSimulator`
  - 基于 IoTServer 的设备模拟程序

## 当前驱动

已接入：

- Modbus TCP / RTU / 串口 ASCII
- Siemens S7
- Mitsubishi
- Omron FINS
- Allen-Bradley
- OPC UA
- MTConnect

已保留统一契约但暂未启用完整实现：

- OPC DA
- Fanuc CNC
- BACnet
- IEC 60870-5-104
- MQTT

## 本地运行

```powershell
docker compose up -d --build
```

默认地址：

- Edge UI / API: http://127.0.0.1:18180/
- Device Simulator: http://127.0.0.1:18181/api/values
- SonnetDB: http://127.0.0.1:15080/

## 构建验证

```powershell
pwsh ./build.ps1 -Action Build
pwsh ./build.ps1 -Action Publish -MustAot
```
