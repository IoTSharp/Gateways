# IoTSharp.Gateways

统一工业采集网关原型，包含：

- 统一驱动契约
- DDD 风格分层后端
- SQLite 可扩展持久化
- 独立 Web API + Worker Host
- Vue 3 前端壳

## 目录结构

- `/home/runner/work/Gateways/Gateways/src/IoTSharp.Gateways.Domain`：领域模型与统一驱动接口
- `/home/runner/work/Gateways/Gateways/src/IoTSharp.Gateways.Application`：配置服务、运行编排、转换服务
- `/home/runner/work/Gateways/Gateways/src/IoTSharp.Gateways.Infrastructure`：SQLite 仓储、驱动适配器、HTTP/MQTT 上传
- `/home/runner/work/Gateways/Gateways/src/IoTSharp.Gateways.Api`：ASP.NET Core Web API
- `/home/runner/work/Gateways/Gateways/src/IoTSharp.Gateways.Worker`：后台采集执行 Worker
- `/home/runner/work/Gateways/Gateways/src/gateway-web`：Vue 3 管理前端

## 当前已落地的统一驱动

- Modbus
- Siemens S7
- Mitsubishi
- Omron FINS
- Allen-Bradley

以下驱动已纳入统一契约，但当前作为隔离占位实现：

- OPC UA
- OPC DA
- MT CNC
- Fanuc CNC

## 上传能力

- HTTP 上传
- MQTT 上传到 IoTSharp

## 本地运行

### 后端 API

```bash
cd /home/runner/work/Gateways/Gateways
 dotnet run --project /home/runner/work/Gateways/Gateways/src/IoTSharp.Gateways.Api/IoTSharp.Gateways.Api.csproj
```

### 采集 Worker

```bash
cd /home/runner/work/Gateways/Gateways
 dotnet run --project /home/runner/work/Gateways/Gateways/src/IoTSharp.Gateways.Worker/IoTSharp.Gateways.Worker.csproj
```

### 前端

```bash
cd /home/runner/work/Gateways/Gateways/src/gateway-web
npm install
npm run dev
```

## 构建验证

```bash
cd /home/runner/work/Gateways/Gateways
 dotnet build IoTSharp.Gateways.sln
cd /home/runner/work/Gateways/Gateways/src/gateway-web
npm run build
```
