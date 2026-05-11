<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import {
  Activity,
  AlertTriangle,
  ChevronRight,
  Cpu,
  Database,
  FileCode2,
  ListTree,
  Network,
  PanelLeft,
  Plus,
  RefreshCw,
  RotateCcw,
  Save,
  Settings2,
  TerminalSquare,
  Trash2,
  Wifi,
} from 'lucide-vue-next'
import { edgeRequest } from './api'
import type {
  CollectionDevice,
  CollectionPoint,
  CollectionProtocolDescriptor,
  CollectionTask,
  ConnectionSettingDefinition,
  EdgeCollectionConfiguration,
  LocalConfigurationResponse,
  LogsResponse,
  ProtocolCatalogResponse,
  RuntimeSummary,
  ScriptResponse,
} from './types'

type PanelName = 'dashboard' | 'topology' | 'upload' | 'script' | 'logs' | 'bootstrap'
type StatusTone = 'info' | 'ok' | 'warn'

interface ProtocolGroup {
  category: string
  protocols: CollectionProtocolDescriptor[]
}

interface PointRow {
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

const activePanel = ref<PanelName>('dashboard')
const selectedProtocolCode = ref('modbus')
const edgeApiBaseUrl = ref(window.location.origin)
const localConfiguration = ref<LocalConfigurationResponse | null>(null)
const baseConfiguration = ref<EdgeCollectionConfiguration>({})
const configurationText = ref('{}')
const jsonParseError = ref('')
const runtimeSummary = ref<RuntimeSummary | null>(null)
const protocolCatalog = ref<CollectionProtocolDescriptor[]>([])
const script = ref<ScriptResponse | null>(null)
const logs = ref<LogsResponse | null>(null)
const statusText = ref('waiting')
const statusTone = ref<StatusTone>('info')
const isLoading = ref(false)
const isSaving = ref(false)
const topologyFormDirty = ref(false)
const uploadFormDirty = ref(false)

const topologyForm = reactive({
  taskKey: '',
  connectionName: '',
  deviceKey: '',
  deviceName: '',
  stationNumber: '1',
})

const connectionValues = reactive<Record<string, string>>({})
const uploadForm = reactive({
  endpoint: '',
  database: '',
  token: '',
  measurement: '',
  field: '',
  site: '',
})
const pointRows = ref<PointRow[]>([])

const fallbackProtocol = computed<CollectionProtocolDescriptor>(() => ({
  code: 'modbus',
  contractProtocol: 'Modbus',
  driverType: 'Modbus',
  displayName: 'Modbus',
  category: 'PLC',
  description: 'Modbus TCP / RTU over TCP 采集。',
  lifecycle: 'ready',
  supportsRead: true,
  supportsWrite: true,
  supportsBatchRead: true,
  supportsBatchWrite: true,
  riskLevel: 'normal',
  connectionSettings: [
    { key: 'transport', label: 'Transport', valueType: 'select', required: true, description: 'tcp or rtuOverTcp.', options: ['tcp', 'rtuOverTcp'] },
    { key: 'host', label: 'Host', valueType: 'text', required: true, description: 'PLC host name or IP.' },
    { key: 'port', label: 'Port', valueType: 'number', required: true, description: 'PLC port.' },
    { key: 'timeout', label: 'Timeout', valueType: 'number', required: false, description: 'Timeout in milliseconds.' },
  ],
}))

const selectedProtocol = computed(() => {
  const normalized = normalizeProtocolKey(selectedProtocolCode.value)
  return protocolCatalog.value.find((item) => normalizeProtocolKey(item.code) === normalized)
    ?? protocolCatalog.value.find((item) => normalizeProtocolKey(item.code) === 'modbus')
    ?? fallbackProtocol.value
})

const protocolGroups = computed<ProtocolGroup[]>(() => {
  const groups = new Map<string, CollectionProtocolDescriptor[]>()
  for (const protocol of protocolCatalog.value) {
    const category = protocol.category || 'Other'
    groups.set(category, [...(groups.get(category) ?? []), protocol])
  }

  return Array.from(groups.entries()).map(([category, protocols]) => ({
    category,
    protocols,
  }))
})

const connectionSettings = computed(() => selectedProtocol.value.connectionSettings ?? [])
const protocolFlags = computed(() => [
  selectedProtocol.value.supportsRead ? 'read' : '',
  selectedProtocol.value.supportsWrite ? 'write' : '',
  selectedProtocol.value.supportsBatchRead ? 'batch-read' : '',
  selectedProtocol.value.supportsBatchWrite ? 'batch-write' : '',
].filter(Boolean))

const isModbusProtocol = computed(() => normalizeProtocolKey(selectedProtocol.value.contractProtocol) === 'modbus')
const protocolTaskCount = computed(() => countTasksForProtocol(baseConfiguration.value, selectedProtocol.value))
const topologyRows = computed(() => buildTopologyRows(baseConfiguration.value, selectedProtocol.value))
const uploadSettings = computed(() => asRecord(baseConfiguration.value.upload?.settings))
const logLines = computed(() => (logs.value?.entries ?? []).map(formatLogEntry))
const scriptText = computed(() => script.value?.script ?? '')
const bootstrapJson = computed(() => JSON.stringify(runtimeSummary.value?.bootstrap ?? {}, null, 2))

const dashboardCards = computed(() => {
  const configuration = baseConfiguration.value
  const summary = runtimeSummary.value
  const upload = configuration.upload
  const settings = uploadSettings.value

  return [
    {
      label: '运行状态',
      value: summary?.process?.name ?? '--',
      meta: summary?.process ? `PID ${summary.process.id} | ${summary.process.threadCount} threads` : '--',
    },
    {
      label: '本地配置',
      value: localConfiguration.value ? `v${configuration.version ?? '--'} ${localConfiguration.value.applied ? 'applied' : 'loaded'}` : '--',
      meta: localConfiguration.value?.filePath ?? '--',
    },
    {
      label: '上传目标',
      value: upload?.protocol ?? '--',
      meta: upload ? `${upload.endpoint || 'no endpoint'} | ${String(settings.database ?? 'no database')}` : '--',
    },
    {
      label: '采集拓扑',
      value: `${summary?.counts?.enabledDeviceCount ?? 0} device(s)`,
      meta: `${summary?.counts?.enabledPointCount ?? 0} point(s) | ${summary?.counts?.enabledUploadRouteCount ?? 0} route(s)`,
    },
  ]
})

const pageTitle = computed(() => {
  if (activePanel.value === 'topology') return `${selectedProtocol.value.displayName} 采集`
  if (activePanel.value === 'upload') return '上传链路'
  if (activePanel.value === 'script') return 'BASIC 脚本'
  if (activePanel.value === 'logs') return '运行日志'
  if (activePanel.value === 'bootstrap') return '本地配置'
  return 'Edge Local Console'
})

let logsTimer: number | undefined

onMounted(async () => {
  await loadAll()
  logsTimer = window.setInterval(refreshLogs, 5000)
})

onBeforeUnmount(() => {
  if (logsTimer) {
    window.clearInterval(logsTimer)
  }
})

async function loadAll() {
  isLoading.value = true
  setStatus('loading')

  try {
    const [config, summary, scriptData, logData, protocols] = await Promise.all([
      edgeRequest<LocalConfigurationResponse>('/api/local/configuration'),
      edgeRequest<RuntimeSummary>('/api/diagnostics/summary'),
      edgeRequest<ScriptResponse>('/api/scripts/polling'),
      edgeRequest<LogsResponse>('/api/diagnostics/logs?count=100&level=Information'),
      edgeRequest<ProtocolCatalogResponse>('/api/collection/protocols'),
    ])

    localConfiguration.value = config
    runtimeSummary.value = summary
    script.value = scriptData
    logs.value = logData
    protocolCatalog.value = protocols.protocols ?? []

    if (!protocolCatalog.value.some((item) => sameProtocol(item.code, selectedProtocolCode.value))) {
      selectedProtocolCode.value = protocolCatalog.value[0]?.code ?? 'modbus'
    }

    baseConfiguration.value = extractConfiguration(config)
    writeConfigurationText(baseConfiguration.value)
    populateFormsFromConfiguration(baseConfiguration.value, selectedProtocol.value)
    setStatus(`last refresh ${formatDate(summary.generatedAtUtc)}`, 'ok')
  } catch (error) {
    setStatus(errorMessage(error), 'warn')
  } finally {
    isLoading.value = false
  }
}

async function refreshLogs() {
  try {
    logs.value = await edgeRequest<LogsResponse>('/api/diagnostics/logs?count=100&level=Information')
  } catch {
    // Keep the last visible log snapshot while the Edge API is restarting.
  }
}

async function applyConfiguration() {
  isSaving.value = true
  try {
    const payload = readMergedConfiguration()
    baseConfiguration.value = payload
    writeConfigurationText(payload)

    await edgeRequest<LocalConfigurationResponse>('/api/local/configuration?apply=true', {
      method: 'PUT',
      body: JSON.stringify(payload),
    })

    topologyFormDirty.value = false
    uploadFormDirty.value = false
    await loadAll()
    setStatus('configuration saved and applied', 'ok')
  } catch (error) {
    setStatus(errorMessage(error), 'warn')
  } finally {
    isSaving.value = false
  }
}

async function resetConfiguration() {
  isSaving.value = true
  try {
    await edgeRequest<LocalConfigurationResponse>('/api/local/configuration/reset', { method: 'POST' })
    await loadAll()
    setStatus('configuration reset', 'ok')
  } catch (error) {
    setStatus(errorMessage(error), 'warn')
  } finally {
    isSaving.value = false
  }
}

function switchPanel(panel: PanelName) {
  activePanel.value = panel
}

function selectProtocol(code: string) {
  if (topologyFormDirty.value || uploadFormDirty.value) {
    syncFormsToJson('draft synced')
  }

  selectedProtocolCode.value = code
  populateFormsFromConfiguration(baseConfiguration.value, selectedProtocol.value)
  activePanel.value = 'topology'
}

function markTopologyDirty() {
  topologyFormDirty.value = true
  setStatus('topology changed')
}

function markUploadDirty() {
  uploadFormDirty.value = true
  setStatus('upload changed')
}

function onJsonInput() {
  const parsed = tryParseConfigurationText()
  if (!parsed) return

  baseConfiguration.value = parsed
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  populateFormsFromConfiguration(parsed, selectedProtocol.value)
  setStatus('JSON changed')
}

function handleBooleanConnectionValue(key: string, event: Event) {
  connectionValues[key] = (event.target as HTMLInputElement).checked ? 'true' : 'false'
  markTopologyDirty()
}

function syncFormsToJson(message = 'forms synced to JSON') {
  const payload = readMergedConfiguration()
  baseConfiguration.value = payload
  writeConfigurationText(payload)
  populateFormsFromConfiguration(payload, selectedProtocol.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  setStatus(message, 'ok')
}

function addPoint() {
  const payload = readMergedConfiguration()
  const { configuration, device } = ensureLocalTopology(payload, selectedProtocol.value)
  device.points = Array.isArray(device.points) ? device.points : []
  device.points.push(defaultPoint(device.points.length, selectedProtocol.value))

  baseConfiguration.value = configuration
  writeConfigurationText(configuration)
  populateFormsFromConfiguration(configuration, selectedProtocol.value)
  setStatus('point added')
}

function removePoint(index: number) {
  pointRows.value.splice(index, 1)
  markTopologyDirty()
}

function setStatus(message: string, tone: StatusTone = 'info') {
  statusText.value = message
  statusTone.value = tone
}

function extractConfiguration(document: LocalConfigurationResponse | null): EdgeCollectionConfiguration {
  const configuration = document?.configuration
  return configuration && typeof configuration === 'object' ? clone(configuration) : {}
}

function populateFormsFromConfiguration(configuration: EdgeCollectionConfiguration, protocol: CollectionProtocolDescriptor) {
  const { task, device } = ensureLocalTopology(configuration, protocol)
  const connection = task.connection ?? {}
  const deviceOptions = asRecord(device.protocolOptions)
  const defaults = protocolDefaults(protocol)

  topologyForm.taskKey = task.taskKey ?? defaults.taskKey
  topologyForm.connectionName = connection.connectionName ?? defaults.connectionName
  topologyForm.deviceKey = device.deviceKey ?? defaults.deviceKey
  topologyForm.deviceName = device.deviceName ?? defaults.deviceName
  topologyForm.stationNumber = String(deviceOptions.stationNumber ?? deviceOptions.slaveId ?? defaults.stationNumber)

  clearRecord(connectionValues)
  for (const setting of protocol.connectionSettings ?? []) {
    connectionValues[setting.key] = settingToString(readConnectionSetting(connection, setting), defaultSettingValue(protocol, setting))
  }

  pointRows.value = (Array.isArray(device.points) ? device.points : []).map(toPointRow)
  const settings = asRecord(configuration.upload?.settings)
  uploadForm.endpoint = configuration.upload?.endpoint ?? ''
  uploadForm.database = String(settings.database ?? '')
  uploadForm.token = String(settings.token ?? '')
  uploadForm.measurement = String(settings.measurement ?? '')
  uploadForm.field = String(settings.field ?? '')
  uploadForm.site = String(settings.site ?? '')
}

function readMergedConfiguration() {
  let payload = clone(baseConfiguration.value)
  if (topologyFormDirty.value) {
    payload = applyTopologyForm(payload, selectedProtocol.value)
  }

  if (uploadFormDirty.value) {
    payload = applyUploadForm(payload)
  }

  return payload
}

function applyTopologyForm(configuration: EdgeCollectionConfiguration, protocol: CollectionProtocolDescriptor) {
  const { configuration: next, task, device } = ensureLocalTopology(configuration, protocol)
  const connection = task.connection ?? {}
  const options = asRecord(connection.protocolOptions)
  const defaults = protocolDefaults(protocol)
  const taskKey = topologyForm.taskKey.trim() || defaults.taskKey
  const deviceKey = topologyForm.deviceKey.trim() || defaults.deviceKey

  task.taskKey = taskKey
  task.protocol = protocol.contractProtocol
  task.connection = {
    ...connection,
    connectionKey: keyFrom(connection.connectionKey || taskKey, `${taskKey}-connection`),
    connectionName: topologyForm.connectionName.trim() || defaults.connectionName,
    protocol: protocol.contractProtocol,
    protocolOptions: options,
  }

  for (const setting of protocol.connectionSettings ?? []) {
    writeConnectionSetting(task.connection, options, setting, connectionValues[setting.key] ?? '')
  }

  task.connection.protocolOptions = pruneEmpty(options)

  device.deviceKey = deviceKey
  device.deviceName = topologyForm.deviceName.trim() || defaults.deviceName
  device.externalKey = device.externalKey || deviceKey
  device.enabled = device.enabled !== false
  const deviceOptions = asRecord(device.protocolOptions)
  if (isModbusProtocol.value) {
    deviceOptions.stationNumber = topologyForm.stationNumber.trim() || defaults.stationNumber
  }
  device.protocolOptions = pruneEmpty(deviceOptions)
  device.points = pointRows.value.map((row, index) => toPoint(row, device.points?.[index]))

  task.reportPolicy = {
    defaultTrigger: 'Always',
    includeQuality: true,
    includeTimestamp: true,
    ...(task.reportPolicy ?? {}),
  }

  return next
}

function applyUploadForm(configuration: EdgeCollectionConfiguration) {
  const next = clone(configuration)
  const settings = asRecord(next.upload?.settings)
  settings.database = uploadForm.database.trim()
  settings.token = uploadForm.token.trim()
  settings.measurement = uploadForm.measurement.trim()
  settings.field = uploadForm.field.trim()
  settings.site = uploadForm.site.trim()

  next.upload = {
    ...(next.upload ?? {}),
    protocol: 'SonnetDb',
    endpoint: uploadForm.endpoint.trim(),
    settings: pruneEmpty(settings),
  }

  return next
}

function ensureLocalTopology(input: EdgeCollectionConfiguration, protocol: CollectionProtocolDescriptor) {
  const configuration = clone(input ?? {})
  const defaults = protocolDefaults(protocol)
  configuration.contractVersion ||= 'edge-collection-v1'
  configuration.edgeNodeId ||= newGuid()
  configuration.version = Math.max(1, toNumber(configuration.version, 1))
  configuration.updatedAt ||= new Date().toISOString()
  configuration.updatedBy ||= 'LocalEdge'

  if (!Array.isArray(configuration.tasks)) {
    configuration.tasks = []
  }

  let task = configuration.tasks.find((item) => sameProtocol(item.protocol, protocol.contractProtocol))
  if (!task) {
    task = {}
    configuration.tasks.push(task)
  }

  task.id ||= newGuid()
  task.taskKey ||= defaults.taskKey
  task.protocol = protocol.contractProtocol
  task.version = Math.max(1, toNumber(task.version, 1))
  task.edgeNodeId ||= configuration.edgeNodeId
  task.connection ||= {}
  task.connection.connectionKey ||= `${task.taskKey}-connection`
  task.connection.connectionName ||= defaults.connectionName
  task.connection.protocol = protocol.contractProtocol
  task.connection.timeoutMs = Math.max(100, toNumber(task.connection.timeoutMs, defaultTimeout(protocol)))
  task.connection.retryCount = Math.max(0, toNumber(task.connection.retryCount, 3))
  task.connection.protocolOptions = asRecord(task.connection.protocolOptions)

  for (const setting of protocol.connectionSettings ?? []) {
    const current = readConnectionSetting(task.connection, setting)
    if (current == null || current === '') {
      writeConnectionSetting(task.connection, task.connection.protocolOptions, setting, defaultSettingValue(protocol, setting))
    }
  }

  if (!Array.isArray(task.devices)) {
    task.devices = []
  }

  if (task.devices.length === 0) {
    task.devices.push({})
  }

  const device = task.devices[0]
  device.deviceKey ||= defaults.deviceKey
  device.deviceName ||= defaults.deviceName
  device.enabled = device.enabled !== false
  device.externalKey ||= device.deviceKey
  device.protocolOptions = asRecord(device.protocolOptions)
  if (isModbusProtocol.value) {
    device.protocolOptions.stationNumber ||= defaults.stationNumber
  }

  if (!Array.isArray(device.points)) {
    device.points = []
  }

  return { configuration, task, device }
}

function readConnectionSetting(connection: CollectionTask['connection'], setting: ConnectionSettingDefinition) {
  if (!connection) return undefined
  const key = normalizeProtocolKey(setting.key)

  if (key === 'host') return connection.host
  if (key === 'port') return connection.port
  if (key === 'transport') return connection.transport
  if (key === 'timeout') return connection.timeoutMs
  if (key === 'retrycount') return connection.retryCount
  if (key === 'serialport') return connection.serialPort

  return asRecord(connection.protocolOptions)[setting.key]
}

function writeConnectionSetting(
  connection: NonNullable<CollectionTask['connection']>,
  options: Record<string, unknown>,
  setting: ConnectionSettingDefinition,
  rawValue: string,
) {
  const key = normalizeProtocolKey(setting.key)
  const value = rawValue.trim()

  if (key === 'host') {
    connection.host = value
    return
  }

  if (key === 'port') {
    connection.port = Math.max(1, toNumber(value, protocolDefaultPort(selectedProtocol.value)))
    return
  }

  if (key === 'transport') {
    connection.transport = value
    return
  }

  if (key === 'timeout') {
    connection.timeoutMs = Math.max(100, toNumber(value, defaultTimeout(selectedProtocol.value)))
    return
  }

  if (key === 'retrycount') {
    connection.retryCount = Math.max(0, toNumber(value, 3))
    return
  }

  if (key === 'serialport') {
    connection.serialPort = value
    return
  }

  options[setting.key] = coerceSettingValue(value, setting.valueType)
}

function tryParseConfigurationText() {
  try {
    const parsed = configurationText.value.trim() ? JSON.parse(configurationText.value) as EdgeCollectionConfiguration : {}
    jsonParseError.value = ''
    return parsed
  } catch (error) {
    jsonParseError.value = errorMessage(error)
    setStatus('JSON parse error', 'warn')
    return null
  }
}

function writeConfigurationText(configuration: EdgeCollectionConfiguration) {
  configurationText.value = JSON.stringify(configuration ?? {}, null, 2)
  jsonParseError.value = ''
}

function buildTopologyRows(configuration: EdgeCollectionConfiguration, protocol: CollectionProtocolDescriptor) {
  const rows: Array<{ taskKey: string; deviceName: string; pointName: string; address: string; target: string; protocol: string }> = []
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

function countTasksForProtocol(configuration: EdgeCollectionConfiguration, protocol: CollectionProtocolDescriptor) {
  return (configuration.tasks ?? []).filter((task) => sameProtocol(task.protocol, protocol.contractProtocol)).length
}

function toPointRow(point: CollectionPoint): PointRow {
  const polling = asRecord(point.polling)
  const mapping = asRecord(point.mapping)

  return {
    pointKey: point.pointKey ?? '',
    pointName: point.pointName ?? '',
    sourceType: point.sourceType ?? pointSourceOptions(selectedProtocol.value)[0],
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

function toPoint(row: PointRow, existing?: CollectionPoint): CollectionPoint {
  const rawValueType = row.rawValueType || 'Float'
  const pointKey = row.pointKey.trim() || 'point'
  const pointName = row.pointName.trim() || pointKey
  const targetName = row.targetName.trim() || pointKey

  return {
    ...(existing ?? {}),
    pointKey,
    pointName,
    sourceType: row.sourceType || pointSourceOptions(selectedProtocol.value)[0],
    address: row.address.trim() || defaultPointAddress(selectedProtocol.value, 0),
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

function defaultPoint(index: number, protocol: CollectionProtocolDescriptor): CollectionPoint {
  const number = index + 1
  return {
    pointKey: `point-${number}`,
    pointName: `Point ${number}`,
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
      displayName: `Point ${number}`,
    },
  }
}

function protocolDefaults(protocol: CollectionProtocolDescriptor) {
  const code = normalizeProtocolKey(protocol.code)
  return {
    taskKey: code === 'modbus' ? 'modbus-device-simulator' : `${code || 'protocol'}-collector`,
    connectionName: protocol.displayName ? `${protocol.displayName} Connection` : 'Protocol Connection',
    deviceKey: code === 'modbus' ? 'device-simulator-01' : `${code || 'protocol'}-01`,
    deviceName: protocol.displayName ? `${protocol.displayName} 01` : 'Device 01',
    stationNumber: '1',
  }
}

function defaultSettingValue(protocol: CollectionProtocolDescriptor, setting: ConnectionSettingDefinition) {
  const code = normalizeProtocolKey(protocol.code)
  const key = normalizeProtocolKey(setting.key)
  const explicit: Record<string, Record<string, string>> = {
    modbus: { transport: 'tcp', host: 'device-simulator', port: '1502', timeout: '3000', endianformat: 'ABCD', plcaddresses: 'true' },
    opcua: { endpoint: 'opc.tcp://127.0.0.1:4840', usesecurity: 'false', securitymode: 'Auto', securitypolicy: 'Auto', timeout: '3000', sessiontimeout: '60000', autoacceptuntrustedcertificates: 'false' },
    opcda: { progid: 'Matrikon.OPC.Simulation.1', host: '127.0.0.1' },
    mtcnc: { baseurl: 'http://127.0.0.1:5000', timeout: '3000', path: 'current' },
    fanuccnc: { host: '127.0.0.1', port: '8193', timeout: '5000' },
    siemenss7: { host: '127.0.0.1', port: '102', model: setting.options?.[0] ?? 'S7_1200', rack: '0', slot: '1', timeout: '3000' },
    mitsubishi: { host: '127.0.0.1', port: '6000', model: setting.options?.[0] ?? 'Qna_3E', timeout: '3000' },
    omronfins: { host: '127.0.0.1', port: '9600', timeout: '3000', endianformat: 'ABCD' },
    allenbradley: { host: '127.0.0.1', port: '44818', slot: '0', timeout: '3000' },
    bacnet: { host: '127.0.0.1', port: '47808', deviceinstance: '1001', networknumber: '0', timeout: '3000' },
    iec104: { host: '127.0.0.1', port: '2404', commonaddress: '1', originatoraddress: '0', timeout: '3000' },
    mqtt: { host: '127.0.0.1', port: '1883', clientid: 'edge-collector', topic: 'edge/collect/#', qos: '1', username: '', password: '' },
  }

  if (explicit[code]?.[key] !== undefined) {
    return explicit[code][key]
  }

  if (setting.valueType === 'number' && key === 'port') return String(protocolDefaultPort(protocol))
  if (setting.valueType === 'boolean') return 'false'
  if (setting.options?.length) return setting.options[0]
  return ''
}

function defaultTimeout(protocol: CollectionProtocolDescriptor) {
  return normalizeProtocolKey(protocol.code) === 'fanuccnc' ? 5000 : 3000
}

function protocolDefaultPort(protocol: CollectionProtocolDescriptor) {
  const code = normalizeProtocolKey(protocol.code)
  return {
    modbus: 502,
    opcua: 4840,
    siemenss7: 102,
    mitsubishi: 6000,
    omronfins: 9600,
    allenbradley: 44818,
    mtcnc: 5000,
    opcda: 135,
    fanuccnc: 8193,
    bacnet: 47808,
    iec104: 2404,
    mqtt: 1883,
  }[code] ?? 502
}

function defaultPointAddress(protocol: CollectionProtocolDescriptor, index: number) {
  const number = index + 1
  const code = normalizeProtocolKey(protocol.code)
  return {
    modbus: String(40001 + index * 2),
    opcua: `ns=2;s=Device.Value${number}`,
    siemenss7: `DB1.DBD${index * 4}`,
    mitsubishi: `D${number}`,
    omronfins: `D${number}`,
    allenbradley: `Tag${number}`,
    mtcnc: 'availability',
    opcda: 'Random.Real8',
    fanuccnc: 'status',
    bacnet: `analogValue/${number}/presentValue`,
    iec104: `M_ME_NC_1:${1000 + index}`,
    mqtt: `edge/collect/device-${number}`,
  }[code] ?? `point-${number}`
}

function pointSourceOptions(protocol: CollectionProtocolDescriptor) {
  const code = normalizeProtocolKey(protocol.code)
  return {
    modbus: ['HoldingRegister', 'InputRegister', 'Coil', 'DiscreteInput'],
    opcua: ['NodeId'],
    opcda: ['Tag'],
    mtcnc: ['DataItem'],
    fanuccnc: ['Address'],
    siemenss7: ['Address', 'DataBlock'],
    mitsubishi: ['DeviceRegister', 'Address'],
    omronfins: ['Address'],
    allenbradley: ['Tag'],
    bacnet: ['ObjectProperty'],
    iec104: ['InformationObject'],
    mqtt: ['Topic', 'JsonPath'],
  }[code] ?? ['Address']
}

function coerceSettingValue(value: string, valueType: string) {
  const normalized = valueType.toLowerCase()
  if (normalized === 'number') return value === '' ? undefined : toNumber(value, 0)
  if (normalized === 'boolean') return value === 'true'
  return value
}

function settingToString(value: unknown, fallback = '') {
  if (value === undefined || value === null || value === '') return fallback
  return String(value)
}

function isBooleanSetting(setting: ConnectionSettingDefinition) {
  return setting.valueType.toLowerCase() === 'boolean'
}

function isSelectSetting(setting: ConnectionSettingDefinition) {
  return setting.valueType.toLowerCase() === 'select' || (setting.options?.length ?? 0) > 0
}

function inputType(setting: ConnectionSettingDefinition) {
  const valueType = setting.valueType.toLowerCase()
  if (valueType === 'password') return 'password'
  if (valueType === 'number') return 'number'
  return 'text'
}

function lifecycleLabel(value: string) {
  return value || 'ready'
}

function lifecycleClass(value: string) {
  return normalizeProtocolKey(value || 'ready')
}

function resolveMappingValueType(rawValueType: string) {
  const normalized = rawValueType.toLowerCase()
  if (normalized.includes('bool')) return 'Boolean'
  if (normalized.includes('int')) return 'Int32'
  if (normalized.includes('string')) return 'String'
  return 'Double'
}

function formatLogEntry(entry: { timestampUtc: string; level: string; category: string; message: string; exception?: string }) {
  const exception = entry.exception ? `\n${entry.exception}` : ''
  return `[${formatDate(entry.timestampUtc)}] ${entry.level} ${entry.category}\n${entry.message}${exception}`
}

function formatDate(value?: string) {
  if (!value) return '--'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

function formatBytes(value?: number) {
  if (!value) return '--'
  const units = ['B', 'KB', 'MB', 'GB']
  let size = value
  let unit = 0
  while (size >= 1024 && unit < units.length - 1) {
    size /= 1024
    unit += 1
  }
  return `${size.toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`
}

function normalizeProtocolKey(value: unknown) {
  return String(value ?? '')
    .trim()
    .replace(/[^a-z0-9]+/gi, '')
    .toLowerCase()
}

function sameProtocol(left: unknown, right: unknown) {
  return normalizeProtocolKey(left) === normalizeProtocolKey(right)
}

function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? { ...(value as Record<string, unknown>) }
    : {}
}

function pruneEmpty(value: Record<string, unknown>) {
  return Object.fromEntries(Object.entries(value).filter(([, entry]) => entry !== undefined && entry !== null)) as Record<string, unknown>
}

function clearRecord(record: Record<string, string>) {
  for (const key of Object.keys(record)) {
    delete record[key]
  }
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value ?? {})) as T
}

function toNumber(value: unknown, fallback: number) {
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : fallback
}

function keyFrom(value: unknown, fallback: string) {
  const key = String(value ?? '')
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')

  return key || fallback
}

function newGuid() {
  return globalThis.crypto?.randomUUID?.()
    ?? 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (token) => {
      const random = Math.random() * 16 | 0
      const value = token === 'x' ? random : (random & 0x3) | 0x8
      return value.toString(16)
    })
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error)
}
</script>

<template>
  <div class="shell">
    <aside class="sidebar">
      <div class="brand">
        <div class="brand-mark">
          <Cpu :size="22" />
        </div>
        <div>
          <div class="brand-title">IoTSharp Edge</div>
          <div class="brand-subtitle">Local Console</div>
        </div>
      </div>

      <nav class="nav" aria-label="主导航">
        <button class="nav-item" :class="{ active: activePanel === 'dashboard' }" type="button" @click="switchPanel('dashboard')">
          <Activity :size="17" />
          <span>仪表盘</span>
        </button>

        <div class="nav-group" :class="{ active: activePanel === 'topology' }">
          <button class="nav-item" :class="{ active: activePanel === 'topology' }" type="button" @click="switchPanel('topology')">
            <ListTree :size="17" />
            <span>采集拓扑</span>
            <ChevronRight class="nav-chevron" :size="16" />
          </button>
          <div class="protocol-nav" aria-label="采集协议">
            <div v-if="!protocolGroups.length" class="nav-empty">协议目录加载中</div>
            <div v-for="group in protocolGroups" :key="group.category" class="protocol-nav-group">
              <div class="protocol-nav-title">{{ group.category }} 采集</div>
              <button
                v-for="protocol in group.protocols"
                :key="protocol.code"
                class="protocol-nav-item"
                :class="{ active: sameProtocol(protocol.code, selectedProtocolCode) }"
                type="button"
                :title="protocol.description"
                @click="selectProtocol(protocol.code)"
              >
                <span>{{ protocol.displayName }}</span>
                <small :class="lifecycleClass(protocol.lifecycle)">{{ lifecycleLabel(protocol.lifecycle) }}</small>
              </button>
            </div>
          </div>
        </div>

        <button class="nav-item" :class="{ active: activePanel === 'upload' }" type="button" @click="switchPanel('upload')">
          <Database :size="17" />
          <span>SonnetDB</span>
        </button>
        <button class="nav-item" :class="{ active: activePanel === 'script' }" type="button" @click="switchPanel('script')">
          <FileCode2 :size="17" />
          <span>BASIC 脚本</span>
        </button>
        <button class="nav-item" :class="{ active: activePanel === 'logs' }" type="button" @click="switchPanel('logs')">
          <TerminalSquare :size="17" />
          <span>运行日志</span>
        </button>
        <button class="nav-item" :class="{ active: activePanel === 'bootstrap' }" type="button" @click="switchPanel('bootstrap')">
          <Settings2 :size="17" />
          <span>本地配置</span>
        </button>
      </nav>

      <div class="sidebar-foot">
        <div class="foot-row">
          <Wifi :size="15" />
          <span>API: {{ edgeApiBaseUrl }}</span>
        </div>
        <div class="foot-row" :data-tone="statusTone">
          <PanelLeft :size="15" />
          <span>{{ statusText }}</span>
        </div>
      </div>
    </aside>

    <main class="content">
      <header class="topbar">
        <div>
          <h1>{{ pageTitle }}</h1>
          <p>本地管理采集配置、上传目标和运行态。</p>
        </div>
        <div class="topbar-tools">
          <div class="metrics">
            <div class="metric">{{ runtimeSummary?.collectionSync?.status ?? 'offline' }}</div>
            <div class="metric">{{ baseConfiguration.updatedBy ?? 'local' }}</div>
            <div class="metric">{{ baseConfiguration.upload?.protocol ?? 'SonnetDB' }}</div>
          </div>
          <div class="actions">
            <button type="button" :disabled="isLoading" @click="loadAll">
              <RefreshCw :size="16" />
              <span>刷新</span>
            </button>
            <button type="button" class="primary" :disabled="isSaving" @click="applyConfiguration">
              <Save :size="16" />
              <span>应用配置</span>
            </button>
            <button type="button" :disabled="isSaving" @click="resetConfiguration">
              <RotateCcw :size="16" />
              <span>重置</span>
            </button>
          </div>
        </div>
      </header>

      <section class="summary-grid">
        <article v-for="card in dashboardCards" :key="card.label" class="summary-card">
          <span>{{ card.label }}</span>
          <strong>{{ card.value }}</strong>
          <small>{{ card.meta }}</small>
        </article>
      </section>

      <section class="workspace">
        <section v-show="activePanel === 'dashboard'" class="panel-group">
          <article class="panel">
            <div class="panel-head">
              <div>
                <h2>本地运行态</h2>
                <small>working set {{ formatBytes(runtimeSummary?.process?.workingSetBytes) }}</small>
              </div>
              <span class="badge">{{ runtimeSummary?.edgeReporting?.enabled ? 'upstream enabled' : 'local mode' }}</span>
            </div>
            <pre class="code-block">{{ JSON.stringify(runtimeSummary ?? {}, null, 2) }}</pre>
          </article>
        </section>

        <section v-show="activePanel === 'topology'" class="panel-group">
          <article class="panel split">
            <div class="stack">
              <div class="protocol-summary">
                <div class="protocol-title">
                  <div>
                    <h2>{{ selectedProtocol.displayName }} 采集</h2>
                    <small>{{ selectedProtocol.category }} · {{ selectedProtocol.contractProtocol }}</small>
                  </div>
                  <span class="badge" :class="lifecycleClass(selectedProtocol.lifecycle)">
                    {{ lifecycleLabel(selectedProtocol.lifecycle) }}
                  </span>
                </div>
                <p>{{ selectedProtocol.description }}</p>
                <div class="protocol-flags">
                  <span v-for="flag in protocolFlags" :key="flag">{{ flag }}</span>
                  <span>{{ selectedProtocol.riskLevel }}</span>
                  <span>{{ protocolTaskCount }} task(s)</span>
                  <span>{{ connectionSettings.length }} setting(s)</span>
                </div>
              </div>

              <div class="panel-section">
                <div class="panel-head compact">
                  <h2>任务与设备</h2>
                  <span class="badge">contract</span>
                </div>
                <div class="form-grid">
                  <label>Task Key<input v-model="topologyForm.taskKey" type="text" @input="markTopologyDirty" /></label>
                  <label>Connection<input v-model="topologyForm.connectionName" type="text" @input="markTopologyDirty" /></label>
                  <label>Device Key<input v-model="topologyForm.deviceKey" type="text" @input="markTopologyDirty" /></label>
                  <label>Device Name<input v-model="topologyForm.deviceName" type="text" @input="markTopologyDirty" /></label>
                  <label v-if="isModbusProtocol">Station / Slave<input v-model="topologyForm.stationNumber" type="number" min="1" max="247" @input="markTopologyDirty" /></label>
                </div>
              </div>

              <div class="panel-section">
                <div class="panel-head compact">
                  <h2>连接参数</h2>
                  <span class="badge">schema</span>
                </div>
                <div v-if="connectionSettings.length" class="form-grid">
                  <label v-for="setting in connectionSettings" :key="setting.key" :class="{ 'checkbox-label': isBooleanSetting(setting) }">
                    <span>{{ setting.label }}<em v-if="setting.required">*</em></span>
                    <select v-if="isSelectSetting(setting)" v-model="connectionValues[setting.key]" @change="markTopologyDirty">
                      <option v-for="option in setting.options ?? []" :key="option" :value="option">{{ option }}</option>
                    </select>
                    <input
                      v-else-if="isBooleanSetting(setting)"
                      type="checkbox"
                      :checked="connectionValues[setting.key] === 'true'"
                      @change="handleBooleanConnectionValue(setting.key, $event)"
                    />
                    <input
                      v-else
                      v-model="connectionValues[setting.key]"
                      :type="inputType(setting)"
                      :required="setting.required"
                      @input="markTopologyDirty"
                    />
                    <small>{{ setting.description }}</small>
                  </label>
                </div>
                <div v-else class="empty">当前协议还没有连接字段定义。</div>
              </div>

              <div class="panel-section">
                <div class="panel-head compact">
                  <h2>点位</h2>
                  <div class="actions dense">
                    <button type="button" @click="addPoint">
                      <Plus :size="15" />
                      <span>新增点位</span>
                    </button>
                    <button type="button" @click="syncFormsToJson('topology synced to JSON')">
                      <Save :size="15" />
                      <span>同步到 JSON</span>
                    </button>
                  </div>
                </div>
                <div class="table-wrap points-editor">
                  <table>
                    <thead>
                      <tr>
                        <th>Key</th>
                        <th>Name</th>
                        <th>Source</th>
                        <th>Address</th>
                        <th>Raw</th>
                        <th>Length</th>
                        <th>Target</th>
                        <th>Value</th>
                        <th>Period</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-if="!pointRows.length">
                        <td colspan="10" class="empty">No points.</td>
                      </tr>
                      <tr v-for="(point, index) in pointRows" :key="index">
                        <td><input v-model="point.pointKey" @input="markTopologyDirty" /></td>
                        <td><input v-model="point.pointName" @input="markTopologyDirty" /></td>
                        <td>
                          <select v-model="point.sourceType" @change="markTopologyDirty">
                            <option v-for="option in pointSourceOptions(selectedProtocol)" :key="option" :value="option">{{ option }}</option>
                          </select>
                        </td>
                        <td><input v-model="point.address" @input="markTopologyDirty" /></td>
                        <td>
                          <select v-model="point.rawValueType" @change="markTopologyDirty">
                            <option>Float</option>
                            <option>Double</option>
                            <option>Int16</option>
                            <option>Int32</option>
                            <option>Boolean</option>
                            <option>String</option>
                          </select>
                        </td>
                        <td><input v-model="point.length" type="number" min="1" @input="markTopologyDirty" /></td>
                        <td><input v-model="point.targetName" @input="markTopologyDirty" /></td>
                        <td>
                          <select v-model="point.valueType" @change="markTopologyDirty">
                            <option>Double</option>
                            <option>Int32</option>
                            <option>Boolean</option>
                            <option>String</option>
                          </select>
                        </td>
                        <td><input v-model="point.readPeriodMs" type="number" min="1000" step="1000" @input="markTopologyDirty" /></td>
                        <td>
                          <button class="icon-button" type="button" title="删除点位" @click="removePoint(index)">
                            <Trash2 :size="15" />
                          </button>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>

              <div class="panel-section">
                <div class="panel-head compact">
                  <h2>运行拓扑</h2>
                  <span class="badge">snapshot</span>
                </div>
                <div class="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Task</th>
                        <th>Device</th>
                        <th>Point</th>
                        <th>Address</th>
                        <th>Target</th>
                        <th>Protocol</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-if="!topologyRows.length">
                        <td colspan="6" class="empty">No {{ selectedProtocol.displayName }} topology configured yet.</td>
                      </tr>
                      <tr v-for="row in topologyRows" :key="`${row.taskKey}-${row.deviceName}-${row.pointName}`">
                        <td>{{ row.taskKey }}</td>
                        <td>{{ row.deviceName }}</td>
                        <td>{{ row.pointName }}</td>
                        <td><code>{{ row.address }}</code></td>
                        <td>{{ row.target }}</td>
                        <td>{{ row.protocol }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            <div class="stack">
              <div class="panel-section stretch">
                <div class="panel-head compact">
                  <h2>本地 JSON</h2>
                  <span class="badge" :class="{ warn: jsonParseError }">{{ jsonParseError ? 'invalid' : 'editable' }}</span>
                </div>
                <textarea v-model="configurationText" spellcheck="false" @input="onJsonInput"></textarea>
                <div v-if="jsonParseError" class="inline-warning">
                  <AlertTriangle :size="15" />
                  <span>{{ jsonParseError }}</span>
                </div>
              </div>
            </div>
          </article>
        </section>

        <section v-show="activePanel === 'upload'" class="panel-group">
          <article class="panel split">
            <div>
              <div class="panel-head">
                <h2>SonnetDB</h2>
                <span class="badge">local target</span>
              </div>
              <div class="info-list">
                <div class="info-row"><span>Protocol</span><strong>{{ baseConfiguration.upload?.protocol ?? '--' }}</strong></div>
                <div class="info-row"><span>Endpoint</span><strong>{{ baseConfiguration.upload?.endpoint || '--' }}</strong></div>
                <div class="info-row"><span>Database</span><strong>{{ uploadSettings.database ?? '--' }}</strong></div>
                <div class="info-row"><span>Measurement</span><strong>{{ uploadSettings.measurement ?? '--' }}</strong></div>
                <div class="info-row"><span>Field</span><strong>{{ uploadSettings.field ?? '--' }}</strong></div>
                <div class="info-row"><span>Site</span><strong>{{ uploadSettings.site ?? '--' }}</strong></div>
              </div>
            </div>
            <div>
              <div class="panel-head">
                <h2>快速编辑</h2>
                <span class="badge">settings</span>
              </div>
              <div class="form-grid">
                <label>Endpoint<input v-model="uploadForm.endpoint" type="text" @input="markUploadDirty" /></label>
                <label>Database<input v-model="uploadForm.database" type="text" @input="markUploadDirty" /></label>
                <label>Token<input v-model="uploadForm.token" type="password" @input="markUploadDirty" /></label>
                <label>Measurement<input v-model="uploadForm.measurement" type="text" @input="markUploadDirty" /></label>
                <label>Field<input v-model="uploadForm.field" type="text" @input="markUploadDirty" /></label>
                <label>Site<input v-model="uploadForm.site" type="text" @input="markUploadDirty" /></label>
              </div>
              <div class="actions align-end">
                <button type="button" @click="syncFormsToJson('SonnetDB settings synced to JSON')">
                  <Save :size="16" />
                  <span>同步到 JSON</span>
                </button>
                <button type="button" class="primary" :disabled="isSaving" @click="applyConfiguration">
                  <Network :size="16" />
                  <span>保存并应用</span>
                </button>
              </div>
            </div>
          </article>
        </section>

        <section v-show="activePanel === 'script'" class="panel-group">
          <article class="panel">
            <div class="panel-head">
              <h2>BASIC 采集脚本</h2>
              <span class="badge">read-only</span>
            </div>
            <pre class="code-block">{{ scriptText }}</pre>
          </article>
        </section>

        <section v-show="activePanel === 'logs'" class="panel-group">
          <article class="panel">
            <div class="panel-head">
              <h2>运行日志</h2>
              <span class="badge">live</span>
            </div>
            <pre class="logs">{{ logLines.length ? logLines.join('\n\n') : 'No logs.' }}</pre>
          </article>
        </section>

        <section v-show="activePanel === 'bootstrap'" class="panel-group">
          <article class="panel">
            <div class="panel-head">
              <h2>本地配置</h2>
              <span class="badge">json</span>
            </div>
            <pre class="code-block">{{ bootstrapJson }}</pre>
          </article>
        </section>
      </section>
    </main>
  </div>
</template>
