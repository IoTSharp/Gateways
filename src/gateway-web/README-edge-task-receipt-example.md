# Edge Task Receipt Example

这是 Gateways 子模块中的最小执行端示例说明。

示例代码位置：

- `src/IoTSharp.Gateways.Worker/EdgeTaskReceiptExample.cs`

用途：

- 演示 Gateway 执行端完成任务后，如何回调 IoTSharp 的 `POST /api/EdgeTask/Receipt`
- 当前已接入 `IoTSharp.Gateways.Worker/EdgeRuntimeReportingWorker`
- Worker 在正常心跳上报后会自动发送一个最小成功回执，用于验证 request/receipt 闭环

最小调用参数：

- `baseUrl`: IoTSharp 平台地址
- `deviceId`: 对应 Edge 节点设备 ID
- `runtimeType`: 建议 `gateway`
- `instanceId`: 运行时实例 ID
- `taskId`: 平台任务 ID

最小回执行为：

- 上报 `contractVersion=edge-task-v1`
- 上报 `targetKey=deviceId:runtimeType:instanceId`
- 上报 `status=Succeeded`
- 上报 `progress=100`

接入说明：

- 当前自动回执使用一个基于 `instanceId` 的稳定 probe task id
- 后续应将该调用迁移到真正的平台任务消费逻辑中
- 若执行中需要进度回报，可改成多次回调 `Running`
- 若失败，请返回 `Failed` 并填充可读 `message`