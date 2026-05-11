export interface FrontendConfig {
  edgeApiBaseUrl: string
  builtAtUtc: string
}

export interface ConnectionSettingDefinition {
  key: string
  label: string
  valueType: string
  required: boolean
  description: string
  options?: string[] | null
}

export interface CollectionProtocolDescriptor {
  code: string
  contractProtocol: string
  driverType: string
  displayName: string
  category: string
  description: string
  lifecycle: string
  supportsRead: boolean
  supportsWrite: boolean
  supportsBatchRead: boolean
  supportsBatchWrite: boolean
  riskLevel: string
  connectionSettings: ConnectionSettingDefinition[]
}

export interface UploadProtocolDescriptor {
  code: string
  displayName: string
  category: string
  description: string
  lifecycle: string
  connectionSettings: ConnectionSettingDefinition[]
}

export interface ProtocolCatalogResponse {
  generatedAtUtc: string
  protocols: CollectionProtocolDescriptor[]
}

export interface UploadProtocolCatalogResponse {
  generatedAtUtc: string
  protocols: UploadProtocolDescriptor[]
}

export interface LocalConfigurationResponse {
  exists?: boolean
  filePath?: string
  lastWriteTimeUtc?: string
  applied?: boolean
  configuration?: EdgeCollectionConfiguration
  [key: string]: unknown
}

export interface EdgeCollectionConfiguration {
  contractVersion?: string
  edgeNodeId?: string
  version?: number
  updatedAt?: string
  updatedBy?: string
  upload?: CollectionUpload
  uploads?: CollectionUpload[]
  tasks?: CollectionTask[]
  [key: string]: unknown
}

export interface CollectionUpload {
  targetKey?: string
  displayName?: string
  protocol?: string
  endpoint?: string
  settings?: Record<string, unknown>
  enabled?: boolean
  batchSize?: number
  bufferingEnabled?: boolean
  [key: string]: unknown
}

export interface CollectionTask {
  id?: string
  taskKey?: string
  protocol?: string
  version?: number
  edgeNodeId?: string
  connection?: CollectionConnection
  devices?: CollectionDevice[]
  reportPolicy?: Record<string, unknown>
  [key: string]: unknown
}

export interface CollectionConnection {
  connectionKey?: string
  connectionName?: string
  protocol?: string
  transport?: string
  host?: string
  port?: number
  serialPort?: string
  timeoutMs?: number
  retryCount?: number
  protocolOptions?: Record<string, unknown>
  [key: string]: unknown
}

export interface CollectionDevice {
  deviceKey?: string
  deviceName?: string
  enabled?: boolean
  externalKey?: string
  protocolOptions?: Record<string, unknown>
  points?: CollectionPoint[]
  [key: string]: unknown
}

export interface CollectionPoint {
  pointKey?: string
  pointName?: string
  sourceType?: string
  address?: string
  rawValueType?: string
  length?: number
  polling?: Record<string, unknown>
  transforms?: unknown[]
  mapping?: Record<string, unknown>
  protocolOptions?: Record<string, unknown>
  [key: string]: unknown
}

export interface ScriptResponse {
  name: string
  language: string
  script: string
}

export interface LogEntry {
  timestampUtc: string
  level: string
  category: string
  message: string
  exception?: string
}

export interface LogsResponse {
  generatedAtUtc: string
  entries: LogEntry[]
}

export interface RuntimeSummary {
  generatedAtUtc?: string
  process?: {
    id: number
    name: string
    threadCount: number
    workingSetBytes?: number
    startTimeUtc?: string
  }
  counts?: {
    enabledDeviceCount?: number
    enabledPointCount?: number
    enabledUploadRouteCount?: number
    pollingTaskCount?: number
  }
  localConfiguration?: Record<string, unknown>
  edgeReporting?: {
    enabled?: boolean
    runtimeType?: string
    runtimeName?: string
    baseUrl?: string
  }
  collectionSync?: {
    status?: string
    [key: string]: unknown
  }
  bootstrap?: Record<string, unknown>
  basicRuntime?: Record<string, unknown>
  [key: string]: unknown
}
