import type {
  CollectionDevice,
  CollectionPoint,
  CollectionProtocolDescriptor,
  CollectionTask,
} from '../types'
import type { PointRow, TopologyRow } from '../uiTypes'
import {
  defaultSettingValue,
  defaultTimeout,
  defaultPointAddress,
  normalizeProtocolKey,
  normalizeStructuralKey,
  normalizeTargetName,
  protocolDefaultPort,
  pointSourceOptions,
  pruneEmpty,
  sameProtocol,
  toNumber,
} from './common'

export function findTopologyTask(configuration: { tasks?: CollectionTask[] }, protocol: CollectionProtocolDescriptor) {
  const tasks = Array.isArray(configuration.tasks) ? configuration.tasks : []
  return tasks.find((task) => sameProtocol(task.protocol, protocol.contractProtocol))
}

export function buildTopologyRows(configuration: { tasks?: CollectionTask[] }, protocol: CollectionProtocolDescriptor): TopologyRow[] {
  const rows: TopologyRow[] = []
  const tasks = Array.isArray(configuration.tasks) ? configuration.tasks : []

  for (const task of tasks) {
    if (!sameProtocol(task.protocol, protocol.contractProtocol)) continue
    for (const device of Array.isArray(task.devices) ? task.devices : []) {
      for (const point of Array.isArray(device.points) ? device.points : []) {
        rows.push({
          taskKey: task.taskKey ?? '--',
          deviceName: device.deviceName || device.deviceKey || '--',
          pointName: point.pointName || point.pointKey || '--',
          address: point.address ?? '--',
          target: String(point.mapping?.targetName ?? '--'),
          protocol: task.protocol ?? protocol.contractProtocol,
        })
      }
    }
  }

  return rows
}

export function countTasksForProtocol(configuration: { tasks?: CollectionTask[] }, protocol: CollectionProtocolDescriptor) {
  return (configuration.tasks ?? []).filter((task) => sameProtocol(task.protocol, protocol.contractProtocol)).length
}

export function toPointRow(point: CollectionPoint, protocol: CollectionProtocolDescriptor): PointRow {
  const polling = asRecord(point.polling)
  const mapping = asRecord(point.mapping)

  return {
    pointKey: point.pointKey ?? '',
    pointName: point.pointName ?? '',
    sourceType: point.sourceType ?? pointSourceOptions(protocol)[0],
    address: point.address ?? '',
    rawValueType: point.rawValueType ?? 'Float',
    length: String(point.length ?? 1),
    targetName: String(mapping.targetName ?? point.pointKey ?? ''),
    targetType: String(mapping.targetType ?? 'Telemetry'),
    valueType: String(mapping.valueType ?? 'Double'),
    unit: String(mapping.unit ?? ''),
    readPeriodMs: String(polling.readPeriodMs ?? 5000),
  }
}

export function toPoint(row: PointRow, protocol: CollectionProtocolDescriptor, existing?: CollectionPoint): CollectionPoint {
  const rawValueType = row.rawValueType || 'Float'
  const pointKey = row.pointKey.trim() || 'point'
  const pointName = row.pointName.trim() || pointKey
  const targetName = row.targetName.trim() || pointKey

  return {
    ...(existing ?? {}),
    pointKey,
    pointName,
    sourceType: row.sourceType || pointSourceOptions(protocol)[0],
    address: row.address.trim() || defaultPointAddress(protocol, 0),
    rawValueType,
    length: Math.max(1, toNumber(row.length, 1)),
    polling: {
      ...(existing?.polling ?? {}),
      readPeriodMs: Math.max(1000, toNumber(row.readPeriodMs, 5000)),
      group: asRecord(existing?.polling).group ?? 'default',
    },
    mapping: {
      ...(existing?.mapping ?? {}),
      targetType: row.targetType || 'Telemetry',
      targetName,
      valueType: row.valueType || resolveMappingValueType(rawValueType),
      displayName: pointName,
      unit: row.unit.trim() || undefined,
    },
  }
}

export function defaultPoint(index: number, protocol: CollectionProtocolDescriptor): CollectionPoint {
  const number = index + 1
  return {
    pointKey: `point-${number}`,
    pointName: `点位 ${number}`,
    sourceType: pointSourceOptions(protocol)[0],
    address: defaultPointAddress(protocol, index),
    rawValueType: 'Float',
    length: 2,
    polling: {
      readPeriodMs: 2000,
      group: 'default',
    },
    mapping: {
      targetType: 'Telemetry',
      targetName: `point_${number}`,
      valueType: 'Double',
      displayName: `点位 ${number}`,
    },
  }
}

export function createPoint(protocol: CollectionProtocolDescriptor, index: number, points: CollectionPoint[] = []) {
  const point = defaultPoint(index, protocol)
  point.pointKey = uniquePointKey(points, point.pointKey ?? 'point')
  point.pointName = point.pointName?.trim() || point.pointKey
  if (point.mapping) {
    point.mapping.targetName = uniquePointTargetName(points, point.pointKey ?? 'point')
    point.mapping.displayName = point.pointName
  }

  return point
}

export function protocolDefaults(protocol: CollectionProtocolDescriptor) {
  const code = normalizeProtocolKey(protocol.code)
  const device = deviceDefaults(protocol, 0)
  return {
    taskKey: code === 'modbus' ? 'modbus-device-simulator' : `${code || 'protocol'}-collector`,
    connectionName: protocol.displayName ? `${protocol.displayName} 连接` : '采集连接',
    deviceKey: device.deviceKey,
    deviceName: device.deviceName,
    stationNumber: '1',
  }
}

export function deviceDefaults(protocol: CollectionProtocolDescriptor, index: number) {
  const code = normalizeProtocolKey(protocol.code)
  const number = String(index + 1).padStart(2, '0')
  return {
    deviceKey: code === 'modbus' ? `device-simulator-${number}` : `${code || 'protocol'}-${number}`,
    deviceName: protocol.displayName ? `${protocol.displayName} 设备 ${number}` : `设备 ${number}`,
    stationNumber: '1',
  }
}

export function uniqueDeviceKey(devices: CollectionDevice[], suggested: string) {
  const existingKeys = new Set(devices.map((device) => normalizeStructuralKey(device.deviceKey)).filter(Boolean))
  const base = suggested.trim() || 'device'
  let candidate = base
  let counter = 1

  while (existingKeys.has(normalizeStructuralKey(candidate))) {
    counter += 1
    candidate = `${base}-${counter}`
  }

  return candidate
}

export function uniquePointKey(points: CollectionPoint[], suggested: string) {
  const existingKeys = new Set(points.map((point) => normalizeStructuralKey(point.pointKey)).filter(Boolean))
  const base = suggested.trim() || 'point'
  let candidate = base
  let counter = 1

  while (existingKeys.has(normalizeStructuralKey(candidate))) {
    counter += 1
    candidate = `${base}-${counter}`
  }

  return candidate
}

export function uniquePointTargetName(points: CollectionPoint[], suggested: string) {
  const existingTargets = new Set(
    points
      .map((point) => normalizeStructuralKey(point.mapping?.targetName))
      .filter(Boolean),
  )
  const base = normalizeTargetName(suggested) || 'point'
  let candidate = base
  let counter = 1

  while (existingTargets.has(normalizeStructuralKey(candidate))) {
    counter += 1
    candidate = `${base}_${counter}`
  }

  return candidate
}

export function createDevice(protocol: CollectionProtocolDescriptor, index: number, devices: CollectionDevice[] = []): CollectionDevice {
  const defaults = deviceDefaults(protocol, index)
  const deviceKey = uniqueDeviceKey(devices, defaults.deviceKey)
  const protocolOptions: Record<string, unknown> = {}

  if (normalizeProtocolKey(protocol.contractProtocol) === 'modbus') {
    protocolOptions.stationNumber = defaults.stationNumber
  }

  return {
    deviceKey,
    deviceName: defaults.deviceName,
    enabled: true,
    externalKey: deviceKey,
    protocolOptions: pruneEmpty(protocolOptions),
    points: [],
  }
}

export function displayDeviceLabel(device?: CollectionDevice | null, index = 0) {
  if (!device) return '新设备草稿'
  const number = String(index + 1).padStart(2, '0')
  const name = device.deviceName?.trim() || `设备 ${number}`
  const key = device.deviceKey?.trim()
  return key ? `${name} · ${key}` : name
}

export function resolveMappingValueType(rawValueType: string) {
  const normalized = rawValueType.toLowerCase()
  if (normalized.includes('bool')) return 'Boolean'
  if (normalized.includes('int')) return 'Int32'
  if (normalized.includes('string')) return 'String'
  return 'Double'
}

export function ensureLocalTopology(input: { tasks?: CollectionTask[] }, protocol: CollectionProtocolDescriptor, deviceIndex = 0) {
  if (input.tasks && input.tasks.length > 0) {
    return input
  }

  const defaults = protocolDefaults(protocol)
  const task: CollectionTask = {
    id: '00000000-0000-0000-0000-000000000001',
    taskKey: defaults.taskKey,
    protocol: protocol.contractProtocol,
    version: 1,
    edgeNodeId: '00000000-0000-0000-0000-000000000001',
    connection: {
      connectionKey: defaults.taskKey,
      connectionName: defaults.connectionName,
      protocol: protocol.driverType,
      transport: defaultSettingValue(protocol, { key: 'transport', label: '传输方式', valueType: 'select', required: true, description: '', options: [] }),
      host: defaultSettingValue(protocol, { key: 'host', label: '主机', valueType: 'text', required: false, description: '' }),
      port: protocolDefaultPort(protocol),
      timeoutMs: defaultTimeout(protocol),
      retryCount: 3,
      protocolOptions: {},
    },
    devices: [
      createDevice(protocol, deviceIndex),
    ],
    reportPolicy: {
      defaultTrigger: 'OnChange',
      includeQuality: true,
      includeTimestamp: true,
    },
  }

  return {
    ...input,
    tasks: [task],
  }
}

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? { ...(value as Record<string, unknown>) }
    : {}
}
