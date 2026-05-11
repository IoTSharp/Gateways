import type {
  CollectionProtocolDescriptor,
  UploadProtocolDescriptor,
} from './types'

export type PanelName = 'dashboard' | 'topology' | 'upload' | 'script' | 'logs' | 'bootstrap'
export type StatusTone = 'info' | 'ok' | 'warn'

export interface ProtocolGroup {
  category: string
  protocols: CollectionProtocolDescriptor[]
}

export interface UploadProtocolGroup {
  category: string
  protocols: UploadProtocolDescriptor[]
}

export interface PointRow {
  pointKey: string
  pointName: string
  sourceType: string
  address: string
  rawValueType: string
  length: string
  targetName: string
  targetType: string
  valueType: string
  unit: string
  readPeriodMs: string
}

export interface DashboardCard {
  label: string
  value: string
  meta: string
}

export interface TopologyRow {
  taskKey: string
  deviceName: string
  pointName: string
  address: string
  target: string
  protocol: string
}
