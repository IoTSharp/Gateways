namespace IoTEdge.Application;

/// <summary>
/// 网关任务执行报告。
/// 记录任务名称和成功、失败计数。
/// </summary>
public sealed record GatewayExecutionReport(Guid TaskId, string TaskName, int SuccessCount, int FailureCount);
