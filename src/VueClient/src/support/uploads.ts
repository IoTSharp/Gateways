import type {
  CollectionUpload,
  EdgeCollectionConfiguration,
  ConnectionSettingDefinition,
  UploadProtocolDescriptor,
} from '../types'
import { asRecord, normalizeProtocolKey } from './common'

export function getUploadTargets(configuration: EdgeCollectionConfiguration): CollectionUpload[] {
  const uploads = Array.isArray(configuration.uploads) && configuration.uploads.length > 0
    ? configuration.uploads
    : configuration.upload
      ? [configuration.upload]
      : []

  return uploads.map((upload) => ({
    ...upload,
    settings: asRecord(upload.settings),
  }))
}

export function findConfiguredUploadProtocol(configuration: EdgeCollectionConfiguration) {
  const targets = getUploadTargets(configuration)
  return (targets.find((upload) => upload.enabled !== false) ?? targets[0])?.protocol?.trim() ?? ''
}

export function sameUploadProtocol(left: unknown, right: unknown) {
  return normalizeUploadProtocolKey(left) === normalizeUploadProtocolKey(right)
}

export function normalizeUploadProtocolKey(value: unknown) {
  const normalized = normalizeProtocolKey(value)
  if (normalized === 'thingboard') return 'thingsboard'
  if (normalized === 'iotsharpdevicehttp' || normalized === 'iotsharpmqtt') return 'iotsharp'
  if (normalized === 'sonnet') return 'sonnetdb'
  if (normalized === 'influx') return 'influxdb'
  return normalized
}

export function uploadEditableSettings(protocol: UploadProtocolDescriptor) {
  return (protocol.connectionSettings ?? []).filter((setting) => normalizeProtocolKey(setting.key) !== 'endpoint')
}

export function uploadDefaults(protocol: UploadProtocolDescriptor, index: number) {
  const number = String(index + 1).padStart(2, '0')
  const code = normalizeUploadProtocolKey(protocol.code) || 'upload'
  return {
    targetKey: `${code}-${number}`,
    displayName: `${protocol.displayName || '上传目标'} ${number}`,
    endpoint: '',
    batchSize: '1',
    bufferingEnabled: false,
  }
}

export function createUploadTarget(protocol: UploadProtocolDescriptor, index: number): CollectionUpload {
  const defaults = uploadDefaults(protocol, index)
  return {
    targetKey: defaults.targetKey,
    displayName: defaults.displayName,
    protocol: protocol.code,
    endpoint: defaults.endpoint,
    settings: {},
    enabled: true,
    batchSize: Number(defaults.batchSize),
    bufferingEnabled: defaults.bufferingEnabled,
  }
}

export function defaultUploadSettingValue(protocol: UploadProtocolDescriptor, setting: ConnectionSettingDefinition) {
  const code = normalizeUploadProtocolKey(protocol.code)
  const key = normalizeProtocolKey(setting.key)
  const explicit: Record<string, Record<string, string>> = {
    iotsharp: {
      token: '',
      site: '',
    },
    thingsboard: {
      token: '',
      site: '',
    },
    sonnetdb: {
      connectionstring: '',
      database: 'metrics',
      token: '',
      measurement: 'edge_modbus',
      field: 'value',
      site: '',
      includerawvalue: 'false',
      rawfield: 'raw_value',
      flush: 'async',
    },
    influxdb: {
      token: '',
      org: '',
      bucket: 'edge',
      database: '',
      measurement: 'edge',
      field: 'value',
      precision: 'ms',
      site: '',
      includerawvalue: 'false',
      rawfield: 'raw_value',
    },
  }

  if (explicit[code]?.[key] !== undefined) {
    return explicit[code][key]
  }

  if (setting.valueType === 'number') return '1'
  if (setting.valueType === 'boolean') return 'false'
  if (setting.options?.length) return setting.options[0]
  return ''
}

export function displayUploadProtocol(value?: string) {
  if (!value) return '--'
  const normalized = normalizeUploadProtocolKey(value)
  return {
    iotsharp: 'IoTSharp 上传',
    thingsboard: 'ThingsBoard 上传',
    sonnetdb: 'SonnetDB 上传',
    influxdb: 'InfluxDB 上传',
    http: 'HTTP 上传',
    iotsharpmqtt: 'IoTSharp MQTT 上传',
    iotsharpdevicehttp: 'IoTSharp 上传',
  }[normalized] ?? value
}

export function displayUploadSummary(configuration: EdgeCollectionConfiguration) {
  const targets = getUploadTargets(configuration)
  if (!targets.length) return '--'
  if (targets.length === 1) return displayUploadProtocol(targets[0].protocol)

  const protocolLabels = [...new Set(targets.map((upload) => displayUploadProtocol(upload.protocol)))]
  return `${targets.length} 个目标 · ${protocolLabels.slice(0, 2).join(' / ')}`
}
