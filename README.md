# IoTSharp.Gateways

统一工业采集网关宿主，当前已经收敛为**单进程**运行时：

- 一个 `IoTSharp.Gateways` 程序同时负责本地配置接口、采集轮询、向 IoTSharp 注册/心跳/能力上报、拉取平台任务并执行
- 不再拆分 `Api`、`Worker`、`gateway-web` 三套宿主
- 本地只保留一个内置 bootstrap 和诊断页，用于填写平台下发的对接 JSON，并查看运行状态

## 目录结构

- `src/IoTSharp.Gateways.Domain`：领域模型与统一驱动接口
- `src/IoTSharp.Gateways.Application`：配置服务、运行编排、转换服务
- `src/IoTSharp.Gateways.Infrastructure`：SQLite 持久化、驱动适配器、HTTP/MQTT 上传
- `src/IoTSharp.Gateways`：单宿主程序，包含后台采集服务、平台对接、静态 bootstrap/诊断页

## 当前驱动

已接入：

- Modbus
- Siemens S7
- Mitsubishi
- Omron FINS
- Allen-Bradley

已预留统一契约，暂未启用：

- OPC UA
- OPC DA
- MT CNC
- Fanuc CNC

## 本地页面

程序启动后直接打开根地址 `/` 即可访问内置页面：

- Bootstrap 配置：读取/保存本地 `bootstrap.json`
- 运行诊断：查看当前进程、bootstrap 文件状态、边缘上报参数、本地采集配置统计

## 本地运行

```bash
cd /home/runner/work/Gateways/Gateways
dotnet run --project /home/runner/work/Gateways/Gateways/src/IoTSharp.Gateways/IoTSharp.Gateways.csproj
```

## 构建验证

```bash
cd /home/runner/work/Gateways/Gateways
dotnet build IoTSharp.Gateways.sln
```
