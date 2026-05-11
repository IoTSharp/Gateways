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
const selectedDeviceIndex = ref(0)
const edgeApiBaseUrl = ref(window.location.origin)
const localConfiguration = ref<LocalConfigurationResponse | null>(null)
const baseConfiguration = ref<EdgeCollectionConfiguration>({})
const configurationText = ref('{}')
const jsonParseError = ref('')
const runtimeSummary = ref<RuntimeSummary | null>(null)
const protocolCatalog = ref<CollectionProtocolDescriptor[]>([])
const script = ref<ScriptResponse | null>(null)
const logs = ref<LogsResponse | null>(null)
const statusText = ref('等待中')
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
  description: 'A unified collection driver for Modbus TCP, RTU over TCP, serial RTU, serial ASCII, and DTU.',
  lifecycle: 'ready',
  supportsRead: true,
  supportsWrite: true,
  supportsBatchRead: true,
  supportsBatchWrite: true,
  riskLevel: 'normal',
  connectionSettings: [
    { key: 'transport', label: '传输方式', valueType: 'select', required: true, description: 'TCP, RTU over TCP, serial RTU, serial ASCII, or DTU.', options: ['tcp', 'rtuOverTcp', 'serialRtu', 'serialAscii', 'dtu'] },
    { key: 'host', label: '主机', valueType: 'text', required: false, description: 'PLC host name or IP address for TCP transports.' },
    { key: 'port', label: '端口', valueType: 'number', required: false, description: 'PLC TCP port.' },
    { key: 'serialPort', label: '串口', valueType: 'text', required: false, description: 'Serial port name for Modbus RTU/ASCII/DTU.' },
    { key: 'baudRate', label: '波特率', valueType: 'number', required: false, description: 'Serial baud rate.' },
    { key: 'dataBits', label: '数据位', valueType: 'number', required: false, description: 'Serial data bits.' },
    { key: 'parity', label: '校验位', valueType: 'select', required: false, description: 'Serial parity mode.', options: ['None', 'Odd', 'Even', 'Mark', 'Space'] },
    { key: 'stopBits', label: '停止位', valueType: 'select', required: false, description: 'Serial stop bits.', options: ['One', 'OnePointFive', 'Two'] },
    { key: 'timeout', label: '超时', valueType: 'number', required: false, description: 'Timeout in milliseconds.' },
    { key: 'endianFormat', label: '字节序', valueType: 'select', required: false, description: 'Word and byte order.', options: ['ABCD', 'BADC', 'CDAB', 'DCBA'] },
    { key: 'plcAddresses', label: 'PLC 地址', valueType: 'boolean', required: false, description: 'Treat addresses as PLC-style addresses.' },
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
const visibleConnectionSettings = computed(() => connectionSettings.value.filter((setting) => isConnectionSettingVisible(setting, selectedProtocol.value, connectionValues.transport)))
const protocolFlags = computed(() => [
  selectedProtocol.value.supportsRead ? 'read' : '',
  selectedProtocol.value.supportsWrite ? 'write' : '',
  selectedProtocol.value.supportsBatchRead ? 'batch-read' : '',
  selectedProtocol.value.supportsBatchWrite ? 'batch-write' : '',
].filter(Boolean))

const isModbusProtocol = computed(() => normalizeProtocolKey(selectedProtocol.value.contractProtocol) === 'modbus')
const protocolTaskCount = computed(() => countTasksForProtocol(baseConfiguration.value, selectedProtocol.value))
const topologyRows = computed(() => buildTopologyRows(baseConfiguration.value, selectedProtocol.value))
const topologyTask = computed(() => findTopologyTask(baseConfiguration.value, selectedProtocol.value))
const topologyDevices = computed(() => Array.isArray(topologyTask.value?.devices) ? topologyTask.value.devices : [])
const selectedDevice = computed(() => topologyDevices.value[selectedDeviceIndex.value] ?? topologyDevices.value[0] ?? null)
const selectedDeviceCount = computed(() => topologyDevices.value.length)
const selectedDeviceSummary = computed(() => displayDeviceLabel(selectedDevice.value, selectedDeviceIndex.value))
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
      meta: summary?.process ? `PID ${summary.process.id} | ${summary.process.threadCount} 线程` : '--',
    },
    {
      label: '本地配置',
      value: localConfiguration.value ? `v${configuration.version ?? '--'} ${localConfiguration.value.applied ? '已应用' : '已加载'}` : '--',
      meta: localConfiguration.value?.filePath ?? '--',
    },
    {
      label: '上传目标',
      value: displayUploadProtocol(upload?.protocol),
      meta: upload ? `${upload.endpoint || '无端点'} | ${String(settings.database ?? '无数据库')}` : '--',
    },
    {
      label: '采集拓扑',
      value: `${summary?.counts?.enabledDeviceCount ?? 0} 设备`,
      meta: `${summary?.counts?.enabledPointCount ?? 0} 点位 | ${summary?.counts?.enabledUploadRouteCount ?? 0} 路由`,
    },
  ]
})

const pageTitle = computed(() => {
  if (activePanel.value === 'topology') return `${selectedProtocol.value.displayName} 采集`
  if (activePanel.value === 'upload') return '上传链路'
  if (activePanel.value === 'script') return 'BASIC 脚本'
  if (activePanel.value === 'logs') return '运行日志'
  if (activePanel.value === 'bootstrap') return '本地配置'
  return '边缘本地控制台'
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
  setStatus('加载中')

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
    setStatus(`最近刷新：${formatDate(summary.generatedAtUtc)}`, 'ok')
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
    // API 重启时保留最后一次可见的日志快照。
  }
}

async function applyConfiguration() {
  await saveConfiguration(true, '配置已保存并应用')
}

async function saveCurrentDeviceConfiguration() {
  await saveConfiguration(false, '当前设备配置已保存')
}

async function saveConfiguration(apply: boolean, successMessage: string) {
  isSaving.value = true
  try {
    const payload = readMergedConfiguration()
    baseConfiguration.value = payload
    writeConfigurationText(payload)

    await edgeRequest<LocalConfigurationResponse>(`/api/local/configuration?apply=${apply ? 'true' : 'false'}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })

    topologyFormDirty.value = false
    uploadFormDirty.value = false
    await loadAll()
    setStatus(successMessage, 'ok')
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
    setStatus('配置已重置', 'ok')
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
    syncFormsToJson('草稿已同步')
  }

  selectedProtocolCode.value = code
  selectedDeviceIndex.value = 0
  populateFormsFromConfiguration(baseConfiguration.value, selectedProtocol.value, selectedDeviceIndex.value)
  activePanel.value = 'topology'
}

function markTopologyDirty() {
  topologyFormDirty.value = true
  setStatus('采集拓扑已修改')
}

function markUploadDirty() {
  uploadFormDirty.value = true
  setStatus('上传配置已修改')
}

function onJsonInput() {
  const parsed = tryParseConfigurationText()
  if (!parsed) return

  baseConfiguration.value = parsed
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  populateFormsFromConfiguration(parsed, selectedProtocol.value, selectedDeviceIndex.value)
  setStatus('JSON 已修改')
}

function handleBooleanConnectionValue(key: string, event: Event) {
  connectionValues[key] = (event.target as HTMLInputElement).checked ? 'true' : 'false'
  markTopologyDirty()
}

function syncFormsToJson(message = '表单已同步到 JSON') {
  const payload = readMergedConfiguration()
  baseConfiguration.value = payload
  writeConfigurationText(payload)
  populateFormsFromConfiguration(payload, selectedProtocol.value, selectedDeviceIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  setStatus(message, 'ok')
}

function selectDevice(index: number) {
  if (Number.isNaN(index) || index < 0 || index >= selectedDeviceCount.value || index === selectedDeviceIndex.value) {
    return
  }

  if (topologyFormDirty.value || uploadFormDirty.value) {
    syncFormsToJson('草稿已同步')
  }

  selectedDeviceIndex.value = index
  populateFormsFromConfiguration(baseConfiguration.value, selectedProtocol.value, selectedDeviceIndex.value)
  setStatus('设备已切换')
}

function handleDeviceSelectChange(event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  selectDevice(value)
}

function addDevice() {
  const previousTask = findTopologyTask(baseConfiguration.value, selectedProtocol.value)
  const previousDeviceCount = Array.isArray(previousTask?.devices) ? previousTask.devices.length : 0
  const hasDraftChanges = topologyFormDirty.value || uploadFormDirty.value
  const payload = previousDeviceCount > 0 || hasDraftChanges
    ? readMergedConfiguration()
    : clone(baseConfiguration.value)
  const { configuration, task } = ensureLocalTopology(payload, selectedProtocol.value, selectedDeviceIndex.value)
  const devices = Array.isArray(task.devices) ? task.devices : (task.devices = [])

  if (previousDeviceCount === 0 && !hasDraftChanges) {
    task.devices = [createDevice(selectedProtocol.value, 0, [])]
    selectedDeviceIndex.value = 0
  } else {
    const device = createDevice(selectedProtocol.value, devices.length, devices)
    devices.push(device)
    selectedDeviceIndex.value = devices.length - 1
  }

  baseConfiguration.value = configuration
  writeConfigurationText(configuration)
  populateFormsFromConfiguration(configuration, selectedProtocol.value, selectedDeviceIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  setStatus('已新建设备', 'ok')
}

function removeDevice() {
  const payload = readMergedConfiguration()
  const { configuration, task, deviceIndex } = ensureLocalTopology(payload, selectedProtocol.value, selectedDeviceIndex.value)
  const devices = Array.isArray(task.devices) ? task.devices : (task.devices = [])

  if (!devices.length) {
    return
  }

  devices.splice(deviceIndex, 1)
  if (devices.length === 0) {
    devices.push(createDevice(selectedProtocol.value, 0, []))
  }

  selectedDeviceIndex.value = Math.min(deviceIndex, devices.length - 1)
  baseConfiguration.value = configuration
  writeConfigurationText(configuration)
  populateFormsFromConfiguration(configuration, selectedProtocol.value, selectedDeviceIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  setStatus('设备已删除', 'ok')
}

function addPoint() {
  const payload = readMergedConfiguration()
  const { configuration, device } = ensureLocalTopology(payload, selectedProtocol.value, selectedDeviceIndex.value)
  device.points = Array.isArray(device.points) ? device.points : []
  device.points.push(defaultPoint(device.points.length, selectedProtocol.value))

  baseConfiguration.value = configuration
  writeConfigurationText(configuration)
  populateFormsFromConfiguration(configuration, selectedProtocol.value, selectedDeviceIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  setStatus('已新增点位')
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

function populateFormsFromConfiguration(configuration: EdgeCollectionConfiguration, protocol: CollectionProtocolDescriptor, deviceIndex = selectedDeviceIndex.value) {
  const { task, device, deviceIndex: resolvedDeviceIndex } = ensureLocalTopology(configuration, protocol, deviceIndex)
  selectedDeviceIndex.value = resolvedDeviceIndex
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
    const rawValue = readConnectionSetting(connection, setting)
    if (normalizeProtocolKey(protocol.contractProtocol) === 'modbus' && normalizeProtocolKey(setting.key) === 'transport') {
      connectionValues[setting.key] = normalizeModbusTransportValue(rawValue ?? defaultSettingValue(protocol, setting))
      continue
    }

    connectionValues[setting.key] = settingToString(rawValue, defaultSettingValue(protocol, setting))
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
  payload = applyTopologyForm(payload, selectedProtocol.value, selectedDeviceIndex.value)
  payload = applyUploadForm(payload)

  return payload
}

function applyTopologyForm(configuration: EdgeCollectionConfiguration, protocol: CollectionProtocolDescriptor, deviceIndex = selectedDeviceIndex.value) {
  const { configuration: next, task, device } = ensureLocalTopology(configuration, protocol, deviceIndex)
  const connection = task.connection ?? {}
  const options = asRecord(connection.protocolOptions)
  const defaults = protocolDefaults(protocol)
  const transport = connectionValues.transport || connection.transport || defaultSettingValue(protocol, { key: 'transport', label: '传输方式', valueType: 'select', required: true, description: '', options: [] })
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
    if (isConnectionSettingVisible(setting, protocol, transport)) {
      writeConnectionSetting(task.connection, options, setting, connectionValues[setting.key] ?? '')
    }
  }

  normalizeConnectionForTransport(task.connection, options, protocol, transport)
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

function ensureLocalTopology(input: EdgeCollectionConfiguration, protocol: CollectionProtocolDescriptor, deviceIndex = 0) {
  const configuration = clone(input ?? {})
  const defaults = protocolDefaults(protocol)
  configuration.contractVersion ||= 'edge-collection-v1'
  configuration.edgeNodeId ||= newGuid()
  configuration.version = Math.max(1, toNumber(configuration.version, 1))
  configuration.updatedAt ||= new Date().toISOString()
  configuration.updatedBy ||= '本地控制台'

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
  const transport = readModbusTransport(task.connection, protocol)

  for (const setting of protocol.connectionSettings ?? []) {
    const current = readConnectionSetting(task.connection, setting)
    if ((current == null || current === '') && isConnectionSettingVisible(setting, protocol, transport)) {
      writeConnectionSetting(task.connection, task.connection.protocolOptions, setting, defaultSettingValue(protocol, setting))
    }
  }

  normalizeConnectionForTransport(task.connection, task.connection.protocolOptions, protocol, transport)

  if (!Array.isArray(task.devices)) {
    task.devices = []
  }

  if (task.devices.length === 0) {
    task.devices.push({})
  }

  const resolvedDeviceIndex = Math.min(Math.max(0, toNumber(deviceIndex, 0)), task.devices.length - 1)
  const device = task.devices[resolvedDeviceIndex]
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

  return { configuration, task, device, deviceIndex: resolvedDeviceIndex }
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
    setStatus('JSON 解析错误', 'warn')
    return null
  }
}

function writeConfigurationText(configuration: EdgeCollectionConfiguration) {
  configurationText.value = JSON.stringify(configuration ?? {}, null, 2)
  jsonParseError.value = ''
}

function findTopologyTask(configuration: EdgeCollectionConfiguration, protocol: CollectionProtocolDescriptor) {
  const tasks = Array.isArray(configuration.tasks) ? configuration.tasks : []
  return tasks.find((task) => sameProtocol(task.protocol, protocol.contractProtocol))
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

function protocolDefaults(protocol: CollectionProtocolDescriptor) {
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

function deviceDefaults(protocol: CollectionProtocolDescriptor, index: number) {
  const code = normalizeProtocolKey(protocol.code)
  const number = String(index + 1).padStart(2, '0')
  return {
    deviceKey: code === 'modbus' ? `device-simulator-${number}` : `${code || 'protocol'}-${number}`,
    deviceName: protocol.displayName ? `${protocol.displayName} 设备 ${number}` : `设备 ${number}`,
    stationNumber: '1',
  }
}

function uniqueDeviceKey(devices: CollectionDevice[], suggested: string) {
  const existingKeys = new Set(devices.map((device) => normalizeProtocolKey(device.deviceKey)))
  let candidate = suggested
  let counter = 1

  while (existingKeys.has(normalizeProtocolKey(candidate))) {
    counter += 1
    candidate = `${suggested}-${counter}`
  }

  return candidate
}

function createDevice(protocol: CollectionProtocolDescriptor, index: number, devices: CollectionDevice[] = []): CollectionDevice {
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

function displayDeviceLabel(device?: CollectionDevice | null, index = 0) {
  if (!device) return '新设备草稿'
  const number = String(index + 1).padStart(2, '0')
  const name = device.deviceName?.trim() || `设备 ${number}`
  const key = device.deviceKey?.trim()
  return key ? `${name} · ${key}` : name
}

function defaultSettingValue(protocol: CollectionProtocolDescriptor, setting: ConnectionSettingDefinition) {
  const code = normalizeProtocolKey(protocol.code)
  const key = normalizeProtocolKey(setting.key)
  const explicit: Record<string, Record<string, string>> = {
    modbus: {
      transport: 'tcp',
      host: 'device-simulator',
      port: '1502',
      serialport: 'COM3',
      baudrate: '9600',
      databits: '8',
      parity: 'None',
      stopbits: 'One',
      timeout: '3000',
      endianformat: 'ABCD',
      plcaddresses: 'true',
    },
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

function isConnectionSettingVisible(setting: ConnectionSettingDefinition, protocol: CollectionProtocolDescriptor, transportValue?: string) {
  if (normalizeProtocolKey(protocol.code) !== 'modbus') {
    return true
  }

  const key = normalizeProtocolKey(setting.key)
  if (key === 'transport' || key === 'timeout' || key === 'endianformat' || key === 'plcaddresses') {
    return true
  }

  const transport = readModbusTransportValue(transportValue)
  if (transport === 'serial') {
    return ['serialport', 'baudrate', 'databits', 'parity', 'stopbits'].includes(key)
  }

  return ['host', 'port'].includes(key)
}

function inputType(setting: ConnectionSettingDefinition) {
  const valueType = setting.valueType.toLowerCase()
  if (valueType === 'password') return 'password'
  if (valueType === 'number') return 'number'
  return 'text'
}

const capabilityLabels: Record<string, string> = {
  read: '读取',
  write: '写入',
  batchread: '批量读',
  batchwrite: '批量写',
}

const lifecycleLabels: Record<string, string> = {
  ready: '可用',
  guarded: '受限',
  planned: '规划中',
}

const riskLabels: Record<string, string> = {
  normal: '常规',
  high: '高风险',
  planned: '规划中',
}

const syncStatusLabels: Record<string, string> = {
  offline: '离线',
  online: '在线',
  waiting: '等待中',
  loading: '加载中',
  disabled: '已禁用',
  syncing: '同步中',
  synced: '已同步',
  uptodate: '已是最新',
  waitingbootstrap: '等待启动配置',
  error: '错误',
  connected: '已连接',
  disconnected: '已断开',
  applied: '已应用',
  loaded: '已加载',
}

const protocolCategoryLabels: Record<string, string> = {
  plc: 'PLC',
  cnc: '数控',
  other: '其他协议',
}

const valueTypeLabels: Record<string, string> = {
  float: '浮点数',
  double: '双精度',
  int16: '16 位整数',
  int32: '32 位整数',
  boolean: '布尔',
  string: '字符串',
}

const pointSourceLabels: Record<string, string> = {
  holdingregister: '保持寄存器',
  inputregister: '输入寄存器',
  coil: '线圈',
  discreteinput: '离散输入',
  nodeid: '节点标识',
  tag: '标签',
  dataitem: '数据项',
  address: '地址',
  datablock: '数据块',
  deviceregister: '设备寄存器',
  objectproperty: '对象属性',
  informationobject: '信息对象',
  topic: '主题',
  jsonpath: 'JSON 路径',
}

const connectionOptionLabels: Record<string, Record<string, string>> = {
  transport: {
    tcp: 'TCP',
    rtuovertcp: 'RTU 透传 TCP',
    serialrtu: '串口 RTU',
    serialascii: '串口 ASCII',
    dtu: '串口 DTU',
    serialdtu: '串口 DTU',
  },
  parity: {
    none: '无',
    odd: '奇校验',
    even: '偶校验',
    mark: '标记校验',
    space: '空格校验',
  },
  stopbits: {
    one: '1 位',
    onepointfive: '1.5 位',
    two: '2 位',
  },
  securitymode: {
    auto: '自动',
    none: '无',
    sign: '签名',
    signandencrypt: '签名并加密',
  },
  securitypolicy: {
    auto: '自动',
    none: '无',
    basic256sha256: '基础 256 位 SHA-256',
    aes128sha256rsaoaep: 'AES-128 / SHA-256 / RSA-OAEP',
    aes256sha256rsapss: 'AES-256 / SHA-256 / RSA-PSS',
  },
  qos: {
    0: '服务质量 0',
    1: '服务质量 1',
    2: '服务质量 2',
  },
}

const connectionSettingLabels: Record<string, string> = {
  transport: '传输方式',
  host: '主机',
  port: '端口',
  serialport: '串口',
  baudrate: '波特率',
  databits: '数据位',
  parity: '校验位',
  stopbits: '停止位',
  timeout: '超时',
  timeoutms: '超时',
  endianformat: '字节序',
  plcaddresses: 'PLC 地址',
  endpoint: '端点',
  baseurl: '基础地址',
  path: '路径',
  progid: '程序标识',
  clsid: '类标识',
  usesecurity: '启用安全',
  securitymode: '安全模式',
  securitypolicy: '安全策略',
  sessiontimeout: '会话超时',
  autoacceptuntrustedcertificates: '自动接受未知证书',
  deviceinstance: '设备实例',
  networknumber: '网络号',
  commonaddress: '公共地址',
  originatoraddress: '源地址',
  clientid: '客户端 ID',
  topic: '主题',
  qos: 'QoS',
  username: '用户名',
  password: '密码',
  model: '型号',
  rack: '机架',
  slot: '槽位',
}

const connectionSettingDescriptions: Record<string, string> = {
  transport: '选择 TCP、RTU over TCP、串口 RTU、串口 ASCII 或 DTU。',
  host: 'TCP 连接的主机名或 IP 地址。',
  port: 'TCP 端口。',
  serialport: '串口设备名。',
  baudrate: '串口波特率。',
  databits: '串口数据位。',
  parity: '串口校验方式。',
  stopbits: '串口停止位。',
  timeout: '超时时间，单位毫秒。',
  timeoutms: '超时时间，单位毫秒。',
  endianformat: '字节和字的排列顺序。',
  plcaddresses: '将地址按 PLC 风格处理。',
  endpoint: '协议端点地址。',
  baseurl: '基础地址。',
  path: '请求路径。',
  progid: 'OPC DA 的程序标识。',
  clsid: 'OPC DA 的类标识。',
  usesecurity: '是否启用安全连接。',
  securitymode: '安全模式。',
  securitypolicy: '安全策略。',
  sessiontimeout: '会话超时时间，单位毫秒。',
  autoacceptuntrustedcertificates: '自动接受不受信任的证书。',
  deviceinstance: '设备实例号。',
  networknumber: '网络号。',
  commonaddress: '公共地址。',
  originatoraddress: '源地址。',
  clientid: '客户端 ID。',
  topic: '订阅主题。',
  qos: 'QoS 等级。',
  username: '用户名。',
  password: '密码。',
  model: '设备型号。',
  rack: '机架号。',
  slot: '槽位号。',
}

const statusLabels: Record<string, string> = {
  waiting: '等待中',
  loading: '加载中',
  configurationsavedandapplied: '配置已保存并应用',
  configurationreset: '配置已重置',
  topologychanged: '采集拓扑已修改',
  uploadchanged: '上传配置已修改',
  jsonchanged: 'JSON 已修改',
  jsonparseerror: 'JSON 解析错误',
  pointadded: '已新增点位',
  draftsynced: '草稿已同步',
  formssyncedtojson: '表单已同步到 JSON',
  topologysyncedtojson: '采集拓扑已同步到 JSON',
  sonnetdbsettingssyncedtojson: 'SonnetDB 设置已同步到 JSON',
}

function displayStatusText(value: string) {
  const raw = value?.trim() ?? ''
  if (!raw) return '等待中'
  if (raw.startsWith('最近刷新：')) return raw
  return statusLabels[normalizeProtocolKey(raw)] ?? raw
}

function displayCapability(value: string) {
  return capabilityLabels[normalizeProtocolKey(value)] ?? value
}

function displayLifecycleLabel(value: string) {
  return lifecycleLabels[normalizeProtocolKey(value)] ?? '可用'
}

function displayRiskLabel(value: string) {
  return riskLabels[normalizeProtocolKey(value)] ?? value
}

function displayProtocolCategory(value: string) {
  const raw = value?.trim() ?? ''
  if (!raw) return '其他协议'
  if (raw === '其他协议') return raw
  return protocolCategoryLabels[normalizeProtocolKey(raw)] ?? raw
}

function displayValueType(value: string) {
  return valueTypeLabels[normalizeProtocolKey(value)] ?? value
}

function displayPointSource(value: string) {
  return pointSourceLabels[normalizeProtocolKey(value)] ?? value
}

function displayConnectionSettingLabel(setting: ConnectionSettingDefinition) {
  const key = normalizeProtocolKey(setting.key)
  return connectionSettingLabels[key] ?? setting.label
}

function displayConnectionSettingDescription(setting: ConnectionSettingDefinition) {
  const key = normalizeProtocolKey(setting.key)
  return connectionSettingDescriptions[key] ?? setting.description
}

function normalizeConnectionSettingOption(setting: ConnectionSettingDefinition, option: string) {
  const key = normalizeProtocolKey(setting.key)
  if (key !== 'transport') {
    return option
  }

  const normalized = normalizeProtocolKey(option)
  if (normalized === 'tcp') return 'tcp'
  if (normalized === 'rtuovertcp') return 'rtuOverTcp'
  if (normalized === 'serialrtu' || normalized === 'rtu' || normalized === 'serial') return 'serialRtu'
  if (normalized === 'serialascii' || normalized === 'ascii') return 'serialAscii'
  if (normalized === 'serialdtu' || normalized === 'dtu') return 'dtu'
  return option
}

function displayConnectionSettingOptions(setting: ConnectionSettingDefinition) {
  const options = [...(setting.options ?? [])]
  if (normalizeProtocolKey(selectedProtocol.value.contractProtocol) === 'modbus' && normalizeProtocolKey(setting.key) === 'transport') {
    if (options.length === 0) {
      options.push('tcp', 'rtuOverTcp', 'serialRtu', 'dtu', 'serialAscii')
    }

    if (!options.some((option) => ['dtu', 'serialdtu'].includes(normalizeProtocolKey(option)))) {
      const insertAt = options.findIndex((option) => normalizeProtocolKey(option) === 'serialascii')
      if (insertAt >= 0) {
        options.splice(insertAt, 0, 'dtu')
      } else {
        options.push('dtu')
      }
    }
  }

  return options
    .map((option) => normalizeConnectionSettingOption(setting, option))
    .filter((option, index, items) => items.indexOf(option) === index)
}

function displayConnectionOption(setting: ConnectionSettingDefinition, option: string) {
  const key = normalizeProtocolKey(setting.key)
  const options = connectionOptionLabels[key]
  if (!options) return option

  return options[normalizeProtocolKey(option)] ?? option
}

function displayUploadProtocol(value?: string) {
  if (!value) return '--'
  return {
    http: 'HTTP 上传',
    iotsharpmqtt: 'IoTSharp MQTT 上传',
    iotsharpdevicehttp: 'IoTSharp 设备 HTTP 上传',
    sonnetdb: 'SonnetDB 上传',
  }[normalizeProtocolKey(value)] ?? value
}

function displaySyncStatus(value?: string) {
  if (!value) return '离线'
  return syncStatusLabels[normalizeProtocolKey(value)] ?? value
}

function displayLogLevel(value: string) {
  return {
    trace: '跟踪',
    debug: '调试',
    information: '信息',
    info: '信息',
    warning: '警告',
    error: '错误',
    critical: '严重',
  }[normalizeProtocolKey(value)] ?? value
}

function displayUpdatedBy(value?: string) {
  const raw = value?.trim() ?? ''
  if (!raw) return '本地'
  const normalized = normalizeProtocolKey(raw)
  if (normalized === 'local' || normalized === 'localedge' || normalized === 'localconfiguration') return '本地'
  if (normalized === 'localdockertemplate') return 'Docker 模板'
  return raw
}

function lifecycleLabel(value: string) {
  return displayLifecycleLabel(value)
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
  return `[${formatDate(entry.timestampUtc)}] ${displayLogLevel(entry.level)} ${entry.category}\n${entry.message}${exception}`
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

function readModbusTransport(connection: CollectionTask['connection'], protocol: CollectionProtocolDescriptor) {
  return normalizeModbusTransportValue(
    connection?.transport
    || asRecord(connection?.protocolOptions).transport
    || defaultSettingValue(protocol, { key: 'transport', label: '传输方式', valueType: 'select', required: true, description: '', options: [] }),
  )
}

function readModbusTransportValue(value?: unknown) {
  const normalized = normalizeProtocolKey(value)
  if (['serialrtu', 'serialascii', 'serialdtu', 'rtu', 'ascii', 'dtu', 'serial'].includes(normalized)) {
    return 'serial'
  }

  if (['rtuovertcp', 'tcp'].includes(normalized)) {
    return normalized
  }

  return 'tcp'
}

function normalizeModbusTransportValue(value?: unknown) {
  const normalized = normalizeProtocolKey(value)
  if (normalized === 'rtuovertcp') return 'rtuOverTcp'
  if (['serialrtu', 'rtu', 'serial'].includes(normalized)) return 'serialRtu'
  if (normalized === 'serialdtu' || normalized === 'dtu') return 'dtu'
  if (['serialascii', 'ascii'].includes(normalized)) return 'serialAscii'
  return 'tcp'
}

function normalizeModbusConnection(connection: NonNullable<CollectionTask['connection']>, options: Record<string, unknown>, transportValue?: string) {
  const normalizedTransport = readModbusTransportValue(transportValue || connection.transport)
  if (normalizedTransport === 'serial') {
    delete connection.host
    delete connection.port
    delete options.host
    delete options.port
    return
  }

  delete connection.serialPort
  delete options.serialPort
  delete options.baudRate
  delete options.dataBits
  delete options.parity
  delete options.stopBits
}

function normalizeConnectionForTransport(
  connection: NonNullable<CollectionTask['connection']>,
  options: Record<string, unknown>,
  protocol: CollectionProtocolDescriptor,
  transportValue?: string,
) {
  if (normalizeProtocolKey(protocol.code) !== 'modbus') {
    return
  }

  const transport = normalizeModbusTransportValue(transportValue || connection.transport)
  connection.transport = transport
  normalizeModbusConnection(connection, options, transport)
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
          <div class="brand-subtitle">本地控制台</div>
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
              <div class="protocol-nav-title">{{ displayProtocolCategory(group.category) }} 采集</div>
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
                <small :class="lifecycleClass(protocol.lifecycle)">{{ displayLifecycleLabel(protocol.lifecycle) }}</small>
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
          <span>接口：{{ edgeApiBaseUrl }}</span>
        </div>
        <div class="foot-row" :data-tone="statusTone">
          <PanelLeft :size="15" />
          <span>{{ displayStatusText(statusText) }}</span>
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
            <div class="metric">{{ displaySyncStatus(runtimeSummary?.collectionSync?.status) }}</div>
            <div class="metric">{{ displayUpdatedBy(baseConfiguration.updatedBy) }}</div>
            <div class="metric">{{ displayUploadProtocol(baseConfiguration.upload?.protocol) }}</div>
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
                <small>工作集 {{ formatBytes(runtimeSummary?.process?.workingSetBytes) }}</small>
              </div>
              <span class="badge">{{ runtimeSummary?.edgeReporting?.enabled ? '上游已启用' : '本地模式' }}</span>
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
                    <small>{{ displayProtocolCategory(selectedProtocol.category) }} · {{ selectedProtocol.contractProtocol }}</small>
                  </div>
                  <span class="badge" :class="lifecycleClass(selectedProtocol.lifecycle)">
                    {{ displayLifecycleLabel(selectedProtocol.lifecycle) }}
                  </span>
                </div>
                <p>{{ selectedProtocol.description }}</p>
                <div class="protocol-flags">
                  <span v-for="flag in protocolFlags" :key="flag">{{ displayCapability(flag) }}</span>
                  <span>{{ displayRiskLabel(selectedProtocol.riskLevel) }}</span>
                  <span>{{ protocolTaskCount }} 个任务</span>
                  <span>{{ visibleConnectionSettings.length }} 个字段</span>
                </div>
              </div>

              <div class="panel-section">
                <div class="panel-head compact">
                  <h2>设备管理</h2>
                  <span class="badge">{{ selectedDeviceCount ? `${selectedDeviceCount} 台` : '新设备草稿' }}</span>
                </div>
                <div class="device-manager">
                  <label class="device-select">
                    <span>选择设备</span>
                    <select :disabled="!selectedDeviceCount" :value="selectedDeviceCount ? selectedDeviceIndex : -1" @change="handleDeviceSelectChange">
                      <option v-if="!selectedDeviceCount" :value="-1">新设备草稿</option>
                      <option v-for="(device, index) in topologyDevices" :key="device.deviceKey || index" :value="index">
                        {{ displayDeviceLabel(device, index) }}
                      </option>
                    </select>
                  </label>
                  <div class="actions device-actions">
                    <button type="button" @click="addDevice">
                      <Plus :size="15" />
                      <span>新建设备</span>
                    </button>
                    <button type="button" @click="saveCurrentDeviceConfiguration">
                      <Save :size="15" />
                      <span>保存当前设备配置</span>
                    </button>
                    <button type="button" :disabled="!selectedDevice" @click="removeDevice">
                      <Trash2 :size="15" />
                      <span>删除设备</span>
                    </button>
                  </div>
                  <small class="device-summary">{{ selectedDeviceSummary }}</small>
                </div>
              </div>

              <div class="panel-section">
                <div class="panel-head compact">
                  <h2>当前设备配置</h2>
                  <span class="badge">当前</span>
                </div>
                <div class="form-grid">
                  <label>任务键<input v-model="topologyForm.taskKey" type="text" @input="markTopologyDirty" /></label>
                  <label>连接名<input v-model="topologyForm.connectionName" type="text" @input="markTopologyDirty" /></label>
                  <label>设备键<input v-model="topologyForm.deviceKey" type="text" @input="markTopologyDirty" /></label>
                  <label>设备名称<input v-model="topologyForm.deviceName" type="text" @input="markTopologyDirty" /></label>
                  <label v-if="isModbusProtocol">站号 / 从站<input v-model="topologyForm.stationNumber" type="number" min="1" max="247" @input="markTopologyDirty" /></label>
                </div>
              </div>

              <div class="panel-section">
                <div class="panel-head compact">
                  <h2>连接参数</h2>
                  <span class="badge">字段</span>
                </div>
                <div v-if="visibleConnectionSettings.length" class="form-grid">
                  <label v-for="setting in visibleConnectionSettings" :key="setting.key" :class="{ 'checkbox-label': isBooleanSetting(setting) }">
                    <span>{{ displayConnectionSettingLabel(setting) }}<em v-if="setting.required">*</em></span>
                    <select v-if="isSelectSetting(setting)" v-model="connectionValues[setting.key]" @change="markTopologyDirty">
                      <option v-for="option in displayConnectionSettingOptions(setting)" :key="option" :value="option">{{ displayConnectionOption(setting, option) }}</option>
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
                    <small>{{ displayConnectionSettingDescription(setting) }}</small>
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
                    <button type="button" @click="syncFormsToJson('采集拓扑已同步到 JSON')">
                      <Save :size="15" />
                      <span>同步到 JSON</span>
                    </button>
                  </div>
                </div>
                <div class="table-wrap points-editor">
                  <table>
                    <thead>
                      <tr>
                        <th>键</th>
                        <th>名称</th>
                        <th>来源</th>
                        <th>地址</th>
                        <th>原始</th>
                        <th>长度</th>
                        <th>目标</th>
                        <th>值</th>
                        <th>周期</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-if="!pointRows.length">
                        <td colspan="10" class="empty">暂无点位。</td>
                      </tr>
                      <tr v-for="(point, index) in pointRows" :key="index">
                        <td><input v-model="point.pointKey" @input="markTopologyDirty" /></td>
                        <td><input v-model="point.pointName" @input="markTopologyDirty" /></td>
                        <td>
                          <select v-model="point.sourceType" @change="markTopologyDirty">
                            <option v-for="option in pointSourceOptions(selectedProtocol)" :key="option" :value="option">{{ displayPointSource(option) }}</option>
                          </select>
                        </td>
                        <td><input v-model="point.address" @input="markTopologyDirty" /></td>
                        <td>
                          <select v-model="point.rawValueType" @change="markTopologyDirty">
                            <option value="Float">浮点数</option>
                            <option value="Double">双精度</option>
                            <option value="Int16">16 位整数</option>
                            <option value="Int32">32 位整数</option>
                            <option value="Boolean">布尔</option>
                            <option value="String">字符串</option>
                          </select>
                        </td>
                        <td><input v-model="point.length" type="number" min="1" @input="markTopologyDirty" /></td>
                        <td><input v-model="point.targetName" @input="markTopologyDirty" /></td>
                        <td>
                          <select v-model="point.valueType" @change="markTopologyDirty">
                            <option value="Double">双精度</option>
                            <option value="Int32">32 位整数</option>
                            <option value="Boolean">布尔</option>
                            <option value="String">字符串</option>
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
                  <span class="badge">快照</span>
                </div>
                <div class="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>任务</th>
                        <th>设备</th>
                        <th>点位</th>
                        <th>地址</th>
                        <th>目标</th>
                        <th>协议</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-if="!topologyRows.length">
                        <td colspan="6" class="empty">当前还未配置 {{ selectedProtocol.displayName }} 拓扑。</td>
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
                  <span class="badge" :class="{ warn: jsonParseError }">{{ jsonParseError ? '有误' : '可编辑' }}</span>
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
                <span class="badge">本地上传</span>
              </div>
              <div class="info-list">
                <div class="info-row"><span>协议</span><strong>{{ displayUploadProtocol(baseConfiguration.upload?.protocol) }}</strong></div>
                <div class="info-row"><span>地址</span><strong>{{ baseConfiguration.upload?.endpoint || '--' }}</strong></div>
                <div class="info-row"><span>数据库</span><strong>{{ uploadSettings.database ?? '--' }}</strong></div>
                <div class="info-row"><span>测点集</span><strong>{{ uploadSettings.measurement ?? '--' }}</strong></div>
                <div class="info-row"><span>字段</span><strong>{{ uploadSettings.field ?? '--' }}</strong></div>
                <div class="info-row"><span>站点</span><strong>{{ uploadSettings.site ?? '--' }}</strong></div>
              </div>
            </div>
            <div>
              <div class="panel-head">
                <h2>快速编辑</h2>
                <span class="badge">设置</span>
              </div>
              <div class="form-grid">
                <label>地址<input v-model="uploadForm.endpoint" type="text" @input="markUploadDirty" /></label>
                <label>数据库<input v-model="uploadForm.database" type="text" @input="markUploadDirty" /></label>
                <label>令牌<input v-model="uploadForm.token" type="password" @input="markUploadDirty" /></label>
                <label>测点集<input v-model="uploadForm.measurement" type="text" @input="markUploadDirty" /></label>
                <label>字段<input v-model="uploadForm.field" type="text" @input="markUploadDirty" /></label>
                <label>站点<input v-model="uploadForm.site" type="text" @input="markUploadDirty" /></label>
              </div>
              <div class="actions align-end">
                <button type="button" @click="syncFormsToJson('SonnetDB 设置已同步到 JSON')">
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
              <span class="badge">只读</span>
            </div>
            <pre class="code-block">{{ scriptText }}</pre>
          </article>
        </section>

        <section v-show="activePanel === 'logs'" class="panel-group">
          <article class="panel">
            <div class="panel-head">
              <h2>运行日志</h2>
              <span class="badge">实时</span>
            </div>
            <pre class="logs">{{ logLines.length ? logLines.join('\n\n') : '暂无日志。' }}</pre>
          </article>
        </section>

        <section v-show="activePanel === 'bootstrap'" class="panel-group">
          <article class="panel">
            <div class="panel-head">
              <h2>本地配置</h2>
              <span class="badge">原始</span>
            </div>
            <pre class="code-block">{{ bootstrapJson }}</pre>
          </article>
        </section>
      </section>
    </main>
  </div>
</template>
