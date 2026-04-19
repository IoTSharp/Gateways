# IoTSharp.Gateways

统一的 Gateway 单宿主程序。

当前模式已经收口为一个可执行程序 `IoTSharp.Gateways`，不再拆分 `IoTSharp.Gateways.Api`、`IoTSharp.Gateways.Worker`、`gateway-web`。Gateway 的职责也收口为：

- 读取本地 `bootstrap.json`
- 向 IoTSharp 平台注册、心跳、上报能力
- 从 IoTSharp 平台拉取采集配置
- 将平台侧采集模型映射为本地执行缓存并执行轮询
- 将采集结果直接回传 IoTSharp

## 职责边界

IoTSharp 平台侧负责：

- 采集模型设计
- 边缘节点管理
- 配置版本管理
- 任务下发与运行观察

Gateway 侧负责：

- 南向协议执行
- 本地执行缓存
- 采集轮询与数据回传
- 最小化 bootstrap / 诊断页面

Gateway 不再提供本地 CRUD 采集配置接口。

## 本地页面

启动后访问 `/`，只保留两个本地能力：

- Bootstrap 配置
  - 读取和保存本地 `bootstrap.json`
  - 主要填写 `EdgeReporting.BaseUrl` 和 `EdgeReporting.AccessToken`
- 诊断页
  - 查看进程状态
  - 查看 bootstrap 状态
  - 查看平台配置同步状态
  - 查看本地执行缓存统计

本地接口只保留：

- `GET /api/bootstrap/config`
- `POST /api/bootstrap/config`
- `GET /api/diagnostics/summary`
- `GET /api/health`

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

平台配置映射后，Gateway 会自动派生回传通道：

- 遥测上报到 `POST /api/Devices/{access_token}/Telemetry`
- 属性上报到 `POST /api/Devices/{access_token}/Attributes`

这样采集模型完全归 IoTSharp 平台侧管理，Gateway 只负责执行。

## 目录结构

- `src/IoTSharp.Gateways.Domain`
  - 领域模型与统一驱动接口
- `src/IoTSharp.Gateways.Application`
  - 运行服务、配置快照、转换服务
- `src/IoTSharp.Gateways.Infrastructure`
  - SQLite 持久化、驱动适配、上传通道
- `src/IoTSharp.Gateways`
  - 单宿主程序、bootstrap/诊断页、平台同步 worker

## 当前驱动

已接入：

- Modbus
- Siemens S7
- Mitsubishi
- Omron FINS
- Allen-Bradley

已保留统一契约但暂未启用完整实现：

- OPC UA
- OPC DA
- MT CNC
- Fanuc CNC

## 本地运行

```bash
cd gateways/Gateways
dotnet run --project src/IoTSharp.Gateways/IoTSharp.Gateways.csproj
```

## 构建验证

```bash
cd gateways/Gateways
dotnet build IoTSharp.Gateways.sln
```
