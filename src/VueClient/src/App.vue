<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { edgeRequest } from './api'
import AppSidebar from './components/AppSidebar.vue'
import AppTopbar from './components/AppTopbar.vue'
import BootstrapPanel from './components/BootstrapPanel.vue'
import DashboardPanel from './components/DashboardPanel.vue'
import LogsPanel from './components/LogsPanel.vue'
import ScriptPanel from './components/ScriptPanel.vue'
import SummaryGrid from './components/SummaryGrid.vue'
import RoutingPanel from './components/RoutingPanel.vue'
import TopologyPanel from './components/TopologyPanel.vue'
import UploadTargetsPanel from './components/UploadTargetsPanel.vue'
import type {
  CollectionDevice,
  CollectionPoint,
  CollectionProtocolDescriptor,
  CollectionTask,
  CollectionUpload,
  CollectionRoute,
  ConnectionSettingDefinition,
  EdgeCollectionConfiguration,
  LocalConfigurationResponse,
  LogsResponse,
  ProtocolCatalogResponse,
  RuntimeSummary,
  ScriptResponse,
  UploadProtocolCatalogResponse,
  UploadProtocolDescriptor,
} from './types'
import type {
  PanelName,
  PointRow,
  ProtocolGroup,
  RoutePointOption,
  RouteRow,
  RouteUploadTargetOption,
  StatusTone,
  TopologyRow,
  UploadProtocolGroup,
} from './uiTypes'

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
const uploadProtocolCatalog = ref<UploadProtocolDescriptor[]>([])
const script = ref<ScriptResponse | null>(null)
const logs = ref<LogsResponse | null>(null)
const statusText = ref('等待中')
const statusTone = ref<StatusTone>('info')
const isLoading = ref(false)
const isSaving = ref(false)
const topologyFormDirty = ref(false)
const uploadFormDirty = ref(false)
const routeFormDirty = ref(false)
const selectedUploadProtocolCode = ref('IoTSharp')
const selectedUploadTargetIndex = ref(-1)
const selectedRouteIndex = ref(-1)

const topologyForm = reactive({
  taskKey: '',
  connectionName: '',
  deviceKey: '',
  deviceName: '',
  stationNumber: '1',
})

const connectionValues = reactive<Record<string, string>>({})
const uploadForm = reactive({
  targetKey: '',
  displayName: '',
  endpoint: '',
  enabled: true,
  batchSize: '1',
  bufferingEnabled: false,
})
const uploadSettingValues = reactive<Record<string, string>>({})
const pointRows = ref<PointRow[]>([])
const routeForm = reactive({
  pointRef: '',
  uploadTargetKey: '',
  targetName: '',
  payloadTemplate: '',
  enabled: true,
})

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

const fallbackUploadProtocol = computed<UploadProtocolDescriptor>(() => ({
  code: 'IoTSharp',
  displayName: 'IoTSharp',
  category: '平台',
  description: 'IoTSharp 平台上传目标，支持遥测与属性通道自动展开。',
  lifecycle: 'ready',
  connectionSettings: [
    { key: 'endpoint', label: '端点', valueType: 'text', required: true, description: 'IoTSharp 平台基础地址或完整上报地址。' },
    { key: 'token', label: '访问令牌', valueType: 'password', required: true, description: 'IoTEdge 访问令牌或设备令牌。' },
    { key: 'site', label: '站点', valueType: 'text', required: false, description: '可选的站点标识，会作为上传标签写入。' },
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

const selectedUploadProtocol = computed(() => {
  const normalized = normalizeUploadProtocolKey(selectedUploadProtocolCode.value)
  return uploadProtocolCatalog.value.find((item) => normalizeUploadProtocolKey(item.code) === normalized)
    ?? uploadProtocolCatalog.value[0]
    ?? fallbackUploadProtocol.value
})

const uploadProtocolGroups = computed<UploadProtocolGroup[]>(() => {
  const groups = new Map<string, UploadProtocolDescriptor[]>()
  for (const protocol of uploadProtocolCatalog.value) {
    const category = protocol.category || '其他'
    groups.set(category, [...(groups.get(category) ?? []), protocol])
  }

  return Array.from(groups.entries()).map(([category, protocols]) => ({
    category,
    protocols,
  }))
})

const uploadTargets = computed(() => getUploadTargets(baseConfiguration.value))
const selectedUploadTargets = computed(() => uploadTargets.value.filter((target) => sameUploadProtocol(target.protocol, selectedUploadProtocol.value.code)))
const selectedUploadTarget = computed(() => selectedUploadTargets.value[selectedUploadTargetIndex.value] ?? selectedUploadTargets.value[0] ?? null)
const selectedUploadTargetCount = computed(() => selectedUploadTargets.value.length)
const uploadSettings = computed(() => selectedUploadProtocol.value.connectionSettings ?? [])
const visibleUploadSettings = computed(() => uploadSettings.value.filter((setting) => normalizeProtocolKey(setting.key) !== 'endpoint'))
const uploadProtocolFlags = computed(() => [
  selectedUploadTargetCount.value ? `${selectedUploadTargetCount.value} 个目标` : '',
  displayUploadProtocol(selectedUploadProtocol.value.code),
  selectedUploadTarget.value?.endpoint ? '已配置端点' : '',
].filter(Boolean))

const routeRows = computed(() => buildRouteRows(baseConfiguration.value))
const routePointOptions = computed(() => buildRoutePointOptions(baseConfiguration.value))
const routeUploadTargetOptions = computed(() => buildRouteUploadTargetOptions(baseConfiguration.value))
const selectedRouteRows = computed(() => routeRows.value)
const selectedRoute = computed(() => selectedRouteRows.value[selectedRouteIndex.value] ?? selectedRouteRows.value[0] ?? null)
const selectedRouteCount = computed(() => selectedRouteRows.value.length)

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
const logLines = computed(() => (logs.value?.entries ?? []).map(formatLogEntry))
const scriptText = computed(() => script.value?.script ?? '')
const bootstrapJson = computed(() => JSON.stringify(runtimeSummary.value?.bootstrap ?? {}, null, 2))

const dashboardCards = computed(() => {
  const configuration = baseConfiguration.value
  const summary = runtimeSummary.value
  const uploadTargets = getUploadTargets(configuration)
  const firstUpload = uploadTargets[0]

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
      value: displayUploadSummary(configuration),
      meta: firstUpload ? `${firstUpload.endpoint || '无端点'} | ${displayUploadProtocol(firstUpload.protocol)}` : '--',
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
  if (activePanel.value === 'upload') return `${selectedUploadProtocol.value.displayName} 上传`
  if (activePanel.value === 'routing') return '数据路由'
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
    const [config, summary, scriptData, logData, protocols, uploadProtocols] = await Promise.all([
      edgeRequest<LocalConfigurationResponse>('/api/local/configuration'),
      edgeRequest<RuntimeSummary>('/api/diagnostics/summary'),
      edgeRequest<ScriptResponse>('/api/scripts/polling'),
      edgeRequest<LogsResponse>('/api/diagnostics/logs?count=100&level=Information'),
      edgeRequest<ProtocolCatalogResponse>('/api/collection/protocols'),
      edgeRequest<UploadProtocolCatalogResponse>('/api/upload/protocols'),
    ])

    localConfiguration.value = config
    runtimeSummary.value = summary
    script.value = scriptData
    logs.value = logData
    protocolCatalog.value = protocols.protocols ?? []
    uploadProtocolCatalog.value = uploadProtocols.protocols ?? []
    baseConfiguration.value = extractConfiguration(config)

    if (!protocolCatalog.value.some((item) => sameProtocol(item.code, selectedProtocolCode.value))) {
      selectedProtocolCode.value = protocolCatalog.value[0]?.code ?? 'modbus'
    }

    const configuredUploadProtocol = findConfiguredUploadProtocol(baseConfiguration.value)
    if (configuredUploadProtocol && uploadProtocolCatalog.value.some((item) => sameUploadProtocol(item.code, configuredUploadProtocol))) {
      selectedUploadProtocolCode.value = configuredUploadProtocol
    } else if (!uploadProtocolCatalog.value.some((item) => sameUploadProtocol(item.code, selectedUploadProtocolCode.value))) {
      selectedUploadProtocolCode.value = uploadProtocolCatalog.value[0]?.code ?? 'IoTSharp'
    }

    writeConfigurationText(baseConfiguration.value)
    populateFormsFromConfiguration(baseConfiguration.value, selectedProtocol.value)
    populateUploadFormsFromConfiguration(baseConfiguration.value, selectedUploadProtocol.value)
    populateRouteFormsFromConfiguration(baseConfiguration.value)
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
  if (jsonParseError.value) {
    setStatus('请先修正本地 JSON 解析错误', 'warn')
    return
  }

  const payload = readMergedConfiguration()
  const validationError = validateStructuralKeys(payload)
  if (validationError) {
    setStatus(validationError, 'warn')
    return
  }

  isSaving.value = true
  try {
    baseConfiguration.value = payload
    writeConfigurationText(payload)

    await edgeRequest<LocalConfigurationResponse>(`/api/local/configuration?apply=${apply ? 'true' : 'false'}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    })

    topologyFormDirty.value = false
    uploadFormDirty.value = false
    routeFormDirty.value = false
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
  if (topologyFormDirty.value || uploadFormDirty.value || routeFormDirty.value) {
    syncFormsToJson('草稿已同步')
  }

  selectedProtocolCode.value = code
  selectedDeviceIndex.value = 0
  populateFormsFromConfiguration(baseConfiguration.value, selectedProtocol.value, selectedDeviceIndex.value)
  activePanel.value = 'topology'
}

function selectUploadProtocol(code: string) {
  if (topologyFormDirty.value || uploadFormDirty.value || routeFormDirty.value) {
    syncFormsToJson('草稿已同步')
  }

  selectedUploadProtocolCode.value = code
  selectedUploadTargetIndex.value = selectedUploadTargets.value.length ? 0 : -1
  populateUploadFormsFromConfiguration(baseConfiguration.value, selectedUploadProtocol.value, selectedUploadTargetIndex.value)
  activePanel.value = 'upload'
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
  routeFormDirty.value = false
  populateFormsFromConfiguration(parsed, selectedProtocol.value, selectedDeviceIndex.value)
  populateUploadFormsFromConfiguration(parsed, selectedUploadProtocol.value, selectedUploadTargetIndex.value)
  populateRouteFormsFromConfiguration(parsed, selectedRouteIndex.value)
  setStatus('JSON 已修改')
}

function handleBooleanConnectionValue(key: string, event: Event) {
  connectionValues[key] = (event.target as HTMLInputElement).checked ? 'true' : 'false'
  markTopologyDirty()
}

function handleUploadSettingBooleanChange(key: string, event: Event) {
  uploadSettingValues[key] = (event.target as HTMLInputElement).checked ? 'true' : 'false'
  markUploadDirty()
}

function syncFormsToJson(message = '表单已同步到 JSON') {
  const payload = readMergedConfiguration()
  baseConfiguration.value = payload
  writeConfigurationText(payload)
  populateFormsFromConfiguration(payload, selectedProtocol.value, selectedDeviceIndex.value)
  populateUploadFormsFromConfiguration(payload, selectedUploadProtocol.value, selectedUploadTargetIndex.value)
  populateRouteFormsFromConfiguration(payload, selectedRouteIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  routeFormDirty.value = false
  setStatus(message, 'ok')
}

function selectDevice(index: number) {
  if (Number.isNaN(index) || index < 0 || index >= selectedDeviceCount.value || index === selectedDeviceIndex.value) {
    return
  }

  if (topologyFormDirty.value || uploadFormDirty.value || routeFormDirty.value) {
    syncFormsToJson('草稿已同步')
  }

  selectedDeviceIndex.value = index
  populateFormsFromConfiguration(baseConfiguration.value, selectedProtocol.value, selectedDeviceIndex.value)
  setStatus('设备已切换')
}

function selectUploadTarget(index: number) {
  if (Number.isNaN(index) || index < 0 || index >= selectedUploadTargets.value.length || index === selectedUploadTargetIndex.value) {
    return
  }

  if (topologyFormDirty.value || uploadFormDirty.value || routeFormDirty.value) {
    syncFormsToJson('草稿已同步')
  }

  selectedUploadTargetIndex.value = index
  populateUploadFormsFromConfiguration(baseConfiguration.value, selectedUploadProtocol.value, selectedUploadTargetIndex.value)
  setStatus('上传目标已切换')
}

function handleDeviceSelectChange(event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  selectDevice(value)
}

function addUploadTarget() {
  const payload = readMergedConfiguration()
  const configuration = clone(payload)
  const uploads = Array.isArray(configuration.uploads) && configuration.uploads.length > 0
    ? [...configuration.uploads]
    : configuration.upload ? [clone(configuration.upload)] : []
  const newTarget = createUploadTarget(selectedUploadProtocol.value, uploads.length)
  uploads.push(newTarget)

  configuration.uploads = uploads
  configuration.upload = uploads[0] ?? undefined

  const nextIndex = uploads
    .map((upload, index) => ({ upload, index }))
    .filter((item) => sameUploadProtocol(item.upload.protocol, selectedUploadProtocol.value.code))
    .length - 1

  baseConfiguration.value = configuration
  writeConfigurationText(configuration)
  selectedUploadTargetIndex.value = Math.max(0, nextIndex)
  populateUploadFormsFromConfiguration(configuration, selectedUploadProtocol.value, selectedUploadTargetIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  routeFormDirty.value = false
  setStatus('已新增上传目标', 'ok')
}

function removeUploadTarget() {
  const payload = readMergedConfiguration()
  const configuration = clone(payload)
  const uploads = Array.isArray(configuration.uploads) && configuration.uploads.length > 0
    ? [...configuration.uploads]
    : configuration.upload ? [clone(configuration.upload)] : []
  const matches = uploads
    .map((upload, index) => ({ upload, index }))
    .filter((item) => sameUploadProtocol(item.upload.protocol, selectedUploadProtocol.value.code))

  const target = matches[selectedUploadTargetIndex.value]
  if (!target) {
    return
  }

  uploads.splice(target.index, 1)
  configuration.uploads = uploads
  configuration.upload = uploads[0] ?? undefined

  baseConfiguration.value = configuration
  writeConfigurationText(configuration)
  const nextMatches = uploads
    .map((upload, index) => ({ upload, index }))
    .filter((item) => sameUploadProtocol(item.upload.protocol, selectedUploadProtocol.value.code))
  selectedUploadTargetIndex.value = nextMatches.length ? Math.min(selectedUploadTargetIndex.value, nextMatches.length - 1) : -1
  populateUploadFormsFromConfiguration(configuration, selectedUploadProtocol.value, selectedUploadTargetIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  routeFormDirty.value = false
  setStatus('上传目标已删除', 'ok')
}

function addDevice() {
  const previousTask = findTopologyTask(baseConfiguration.value, selectedProtocol.value)
  const previousDeviceCount = Array.isArray(previousTask?.devices) ? previousTask.devices.length : 0
  const hasDraftChanges = topologyFormDirty.value || uploadFormDirty.value || routeFormDirty.value
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
  routeFormDirty.value = false
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
  routeFormDirty.value = false
  setStatus('设备已删除', 'ok')
}

function addPoint() {
  const payload = readMergedConfiguration()
  const { configuration, device } = ensureLocalTopology(payload, selectedProtocol.value, selectedDeviceIndex.value)
  device.points = Array.isArray(device.points) ? device.points : []
  device.points.push(createPoint(selectedProtocol.value, device.points.length, device.points))

  baseConfiguration.value = configuration
  writeConfigurationText(configuration)
  populateFormsFromConfiguration(configuration, selectedProtocol.value, selectedDeviceIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  routeFormDirty.value = false
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
}

function populateUploadFormsFromConfiguration(configuration: EdgeCollectionConfiguration, protocol: UploadProtocolDescriptor, targetIndex = selectedUploadTargetIndex.value) {
  const targets = getUploadTargets(configuration).filter((target) => sameUploadProtocol(target.protocol, protocol.code))
  const resolvedTargetIndex = targets.length ? Math.min(Math.max(0, targetIndex), targets.length - 1) : -1
  selectedUploadTargetIndex.value = resolvedTargetIndex
  const target = resolvedTargetIndex >= 0 ? targets[resolvedTargetIndex] : null
  const defaults = uploadDefaults(protocol, Math.max(resolvedTargetIndex, 0))
  const settings = asRecord(target?.settings)

  uploadForm.targetKey = target?.targetKey ?? defaults.targetKey
  uploadForm.displayName = target?.displayName ?? defaults.displayName
  uploadForm.endpoint = target?.endpoint ?? defaults.endpoint
  uploadForm.enabled = target?.enabled !== false
  uploadForm.batchSize = String(target?.batchSize ?? defaults.batchSize)
  uploadForm.bufferingEnabled = target?.bufferingEnabled === true

  clearRecord(uploadSettingValues)
  for (const setting of uploadEditableSettings(protocol)) {
    uploadSettingValues[setting.key] = settingToString(settings[setting.key], defaultUploadSettingValue(protocol, setting))
  }
}

function populateRouteFormsFromConfiguration(configuration: EdgeCollectionConfiguration, routeIndex = selectedRouteIndex.value) {
  const routes = buildRouteRows(configuration)
  const resolvedRouteIndex = routes.length ? Math.min(Math.max(0, routeIndex), routes.length - 1) : -1
  selectedRouteIndex.value = resolvedRouteIndex
  const route = resolvedRouteIndex >= 0 ? routes[resolvedRouteIndex] : null
  const pointOptions = buildRoutePointOptions(configuration)
  const uploadTargetOptions = buildRouteUploadTargetOptions(configuration)

  const pointRef = route ? buildPointRef(route.taskKey, route.deviceKey, route.pointKey) : pointOptions[0]?.value ?? ''
  const pointTargetName = resolvePointTargetName(configuration, pointRef)

  routeForm.pointRef = pointRef
  routeForm.uploadTargetKey = route?.uploadTargetKey ?? uploadTargetOptions[0]?.value ?? ''
  routeForm.targetName = route?.targetName || pointTargetName
  routeForm.payloadTemplate = route?.payloadTemplate ?? ''
  routeForm.enabled = route?.enabled !== false
}

function markRouteDirty() {
  routeFormDirty.value = true
  setStatus('路由已修改')
}

function handleRoutePointChange() {
  routeForm.targetName = resolvePointTargetName(baseConfiguration.value, routeForm.pointRef)
  markRouteDirty()
}

function selectRoute(index: number) {
  if (Number.isNaN(index) || index < 0 || index >= selectedRouteRows.value.length || index === selectedRouteIndex.value) {
    return
  }

  if (topologyFormDirty.value || uploadFormDirty.value || routeFormDirty.value) {
    syncFormsToJson('草稿已同步')
  }

  selectedRouteIndex.value = index
  populateRouteFormsFromConfiguration(baseConfiguration.value, selectedRouteIndex.value)
  setStatus('路由已切换')
}

function addRoute() {
  const payload = readMergedConfiguration()
  const configuration = clone(payload)
  const routes = Array.isArray(configuration.uploadRoutes) && configuration.uploadRoutes.length > 0
    ? [...configuration.uploadRoutes]
    : []

  const pointOptions = buildRoutePointOptions(configuration)
  const uploadTargetOptions = buildRouteUploadTargetOptions(configuration)
  const defaultPointRef = pointOptions[0]?.value ?? ''
  const defaultPoint = parsePointRef(defaultPointRef)
  const newRoute: CollectionRoute = {
    taskKey: defaultPoint?.taskKey ?? '',
    deviceKey: defaultPoint?.deviceKey ?? '',
    pointKey: defaultPoint?.pointKey ?? '',
    uploadTargetKey: uploadTargetOptions[0]?.value ?? '',
    targetName: defaultPointRef ? resolvePointTargetName(configuration, defaultPointRef) : '',
    payloadTemplate: '',
    enabled: true,
  }

  routes.push(newRoute)
  configuration.uploadRoutes = routes

  baseConfiguration.value = configuration
  writeConfigurationText(configuration)
  const nextRows = buildRouteRows(configuration)
  selectedRouteIndex.value = Math.max(0, nextRows.findIndex((row) => row.configIndex === routes.length - 1))
  populateRouteFormsFromConfiguration(configuration, selectedRouteIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  routeFormDirty.value = false
  setStatus('已新增路由', 'ok')
}

function removeRoute() {
  const payload = readMergedConfiguration()
  const configuration = clone(payload)
  const routes = Array.isArray(configuration.uploadRoutes) && configuration.uploadRoutes.length > 0
    ? [...configuration.uploadRoutes]
    : []
  const selected = selectedRouteRows.value[selectedRouteIndex.value]

  if (!selected) {
    return
  }

  routes.splice(selected.configIndex, 1)
  configuration.uploadRoutes = routes

  baseConfiguration.value = configuration
  writeConfigurationText(configuration)
  const nextRows = buildRouteRows(configuration)
  selectedRouteIndex.value = nextRows.length ? Math.min(selectedRouteIndex.value, nextRows.length - 1) : -1
  populateRouteFormsFromConfiguration(configuration, selectedRouteIndex.value)
  topologyFormDirty.value = false
  uploadFormDirty.value = false
  routeFormDirty.value = false
  setStatus('路由已删除', 'ok')
}

function readMergedConfiguration() {
  let payload = clone(baseConfiguration.value)
  payload = applyTopologyForm(payload, selectedProtocol.value, selectedDeviceIndex.value)
  payload = applyUploadForm(payload)
  payload = applyRouteForm(payload)

  return payload
}

function validateStructuralKeys(configuration: EdgeCollectionConfiguration) {
  const tasks = Array.isArray(configuration.tasks) ? configuration.tasks : []
  const taskKeys = new Set<string>()
  const pointRefs = new Set<string>()

  for (const task of tasks) {
    const taskKey = normalizeStructuralKey(task.taskKey)
    if (!taskKey) {
      return '采集任务键不能为空。'
    }
    if (taskKeys.has(taskKey)) {
      return `采集任务键“${task.taskKey?.trim() ?? ''}”重复。`
    }
    taskKeys.add(taskKey)

    const devices = Array.isArray(task.devices) ? task.devices : []
    const deviceKeys = new Set<string>()
    for (const device of devices) {
      const deviceKey = normalizeStructuralKey(device.deviceKey)
      if (!deviceKey) {
        return `任务“${task.taskKey?.trim() ?? ''}”包含未设置设备键的设备。`
      }
      if (deviceKeys.has(deviceKey)) {
        return `任务“${task.taskKey?.trim() ?? ''}”中设备键“${device.deviceKey?.trim() ?? ''}”重复。`
      }
      deviceKeys.add(deviceKey)

      const points = Array.isArray(device.points) ? device.points : []
      const pointKeys = new Set<string>()
      for (const point of points) {
        const pointKey = normalizeStructuralKey(point.pointKey)
        if (!pointKey) {
          return `任务“${task.taskKey?.trim() ?? ''}”、设备“${device.deviceKey?.trim() ?? ''}”包含未设置点位键的点位。`
        }
        if (pointKeys.has(pointKey)) {
          return `任务“${task.taskKey?.trim() ?? ''}”、设备“${device.deviceKey?.trim() ?? ''}”中点位键“${point.pointKey?.trim() ?? ''}”重复。`
        }
        pointKeys.add(pointKey)
        pointRefs.add(buildPointRef(task.taskKey ?? '', device.deviceKey ?? '', point.pointKey ?? ''))
      }
    }
  }

  const uploads = getUploadTargets(configuration)
  const uploadKeys = new Set(
    uploads
      .map((upload) => normalizeStructuralKey(upload.targetKey))
      .filter(Boolean),
  )

  const routeKeys = new Set<string>()
  for (const route of Array.isArray(configuration.uploadRoutes) ? configuration.uploadRoutes : []) {
    const taskKey = normalizeStructuralKey(route.taskKey)
    const deviceKey = normalizeStructuralKey(route.deviceKey)
    const pointKey = normalizeStructuralKey(route.pointKey)
    const uploadTargetKey = normalizeStructuralKey(route.uploadTargetKey)

    if (!taskKey) {
      return '上传路由的 taskKey 不能为空。'
    }
    if (!deviceKey) {
      return `上传路由“${route.taskKey?.trim() ?? ''}”的 deviceKey 不能为空。`
    }
    if (!pointKey) {
      return `上传路由“${route.taskKey?.trim() ?? ''}/${route.deviceKey?.trim() ?? ''}”的 pointKey 不能为空。`
    }
    if (!uploadTargetKey) {
      return `上传路由“${route.taskKey?.trim() ?? ''}/${route.deviceKey?.trim() ?? ''}/${route.pointKey?.trim() ?? ''}”的 uploadTargetKey 不能为空。`
    }

    const pointRef = buildPointRef(route.taskKey ?? '', route.deviceKey ?? '', route.pointKey ?? '')
    if (!pointRefs.has(pointRef)) {
      return `上传路由引用了不存在的点位“${route.taskKey?.trim() ?? ''}/${route.deviceKey?.trim() ?? ''}/${route.pointKey?.trim() ?? ''}”。`
    }

    if (!uploadKeys.has(uploadTargetKey)) {
      return `上传路由“${route.taskKey?.trim() ?? ''}/${route.deviceKey?.trim() ?? ''}/${route.pointKey?.trim() ?? ''}”引用了不存在的上传目标“${route.uploadTargetKey?.trim() ?? ''}”。`
    }

    const routeKey = stringJoinKey(pointRef, uploadTargetKey, normalizeStructuralKey(route.targetName), normalizeStructuralKey(route.payloadTemplate))
    if (routeKeys.has(routeKey)) {
      return `上传路由“${route.taskKey?.trim() ?? ''}/${route.deviceKey?.trim() ?? ''}/${route.pointKey?.trim() ?? ''} -> ${route.uploadTargetKey?.trim() ?? ''}”重复。`
    }
    routeKeys.add(routeKey)
  }

  return ''
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
  const uploads = Array.isArray(next.uploads) && next.uploads.length > 0
    ? [...next.uploads]
    : next.upload ? [clone(next.upload)] : []
  const matches = uploads
    .map((upload, index) => ({ upload, index }))
    .filter((item) => sameUploadProtocol(item.upload.protocol, selectedUploadProtocol.value.code))
  const selected = matches[selectedUploadTargetIndex.value] ?? null

  if (!selected && !uploadFormDirty.value) {
    next.uploads = uploads
    next.upload = uploads[0] ?? undefined
    return next
  }

  let targetIndex = selected?.index ?? -1
  let target = targetIndex >= 0 ? clone(uploads[targetIndex]) : createUploadTarget(selectedUploadProtocol.value, uploads.length)
  const settings = asRecord(target.settings)

  target.targetKey = uploadForm.targetKey.trim() || target.targetKey || createUploadTarget(selectedUploadProtocol.value, uploads.length).targetKey
  target.displayName = uploadForm.displayName.trim() || target.displayName || selectedUploadProtocol.value.displayName
  target.protocol = selectedUploadProtocol.value.code
  target.endpoint = uploadForm.endpoint.trim()
  target.enabled = uploadForm.enabled !== false
  target.batchSize = Math.max(1, toNumber(uploadForm.batchSize, target.batchSize ?? 1))
  target.bufferingEnabled = uploadForm.bufferingEnabled

  for (const setting of uploadEditableSettings(selectedUploadProtocol.value)) {
    settings[setting.key] = uploadSettingValues[setting.key] ?? ''
  }

  target.settings = pruneEmpty(settings)

  if (targetIndex >= 0) {
    uploads[targetIndex] = target
  } else {
    targetIndex = uploads.length
    uploads.push(target)
  }

  next.uploads = uploads
  next.upload = uploads[0] ?? undefined
  selectedUploadTargetIndex.value = selected ? selectedUploadTargetIndex.value : Math.max(0, uploads.filter((item) => sameUploadProtocol(item.protocol, selectedUploadProtocol.value.code)).length - 1)

  return next
}

function applyRouteForm(configuration: EdgeCollectionConfiguration) {
  const next = clone(configuration)
  const routes = Array.isArray(next.uploadRoutes) && next.uploadRoutes.length > 0
    ? [...next.uploadRoutes]
    : []
  const selected = selectedRouteRows.value[selectedRouteIndex.value] ?? null

  if (!selected && !routeFormDirty.value) {
    next.uploadRoutes = routes
    return next
  }

  const parsedPointRef = parsePointRef(routeForm.pointRef) ?? (selected
    ? { taskKey: selected.taskKey, deviceKey: selected.deviceKey, pointKey: selected.pointKey }
    : null)

  if (!parsedPointRef) {
    next.uploadRoutes = routes
    return next
  }

  let routeIndex = selected?.configIndex ?? -1
  const route: CollectionRoute = routeIndex >= 0 ? clone(routes[routeIndex]) : {}
  const pointRef = buildPointRef(parsedPointRef.taskKey, parsedPointRef.deviceKey, parsedPointRef.pointKey)

  route.taskKey = parsedPointRef.taskKey
  route.deviceKey = parsedPointRef.deviceKey
  route.pointKey = parsedPointRef.pointKey
  route.uploadTargetKey = routeForm.uploadTargetKey.trim()
  route.targetName = routeForm.targetName.trim() || resolvePointTargetName(next, pointRef) || parsedPointRef.pointKey
  route.payloadTemplate = routeForm.payloadTemplate.trim()
  route.enabled = routeForm.enabled !== false

  if (routeIndex >= 0) {
    routes[routeIndex] = route
  } else {
    routeIndex = routes.length
    routes.push(route)
  }

  next.uploadRoutes = routes
  const nextRows = buildRouteRows(next)
  const nextSelectedIndex = nextRows.findIndex((row) => row.configIndex === routeIndex)
  selectedRouteIndex.value = nextSelectedIndex >= 0 ? nextSelectedIndex : Math.max(0, nextRows.length - 1)

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

function buildRoutePointOptions(configuration: EdgeCollectionConfiguration) {
  const pointMap = new Map<string, RoutePointOption>()
  for (const { task, device, point } of enumeratePoints(configuration)) {
    const pointRef = buildPointRef(task.taskKey ?? '', device.deviceKey ?? '', point.pointKey ?? '')
    pointMap.set(pointRef, {
      value: pointRef,
      label: buildPointOptionLabel(task, device, point)
    })
  }

  for (const route of Array.isArray(configuration.uploadRoutes) ? configuration.uploadRoutes : []) {
    const pointRef = buildPointRef(route.taskKey ?? '', route.deviceKey ?? '', route.pointKey ?? '')
    if (!pointMap.has(pointRef)) {
      pointMap.set(pointRef, {
        value: pointRef,
        label: buildMissingPointOptionLabel(route.taskKey ?? '', route.deviceKey ?? '', route.pointKey ?? '')
      })
    }
  }

  return Array.from(pointMap.values()).sort((left, right) => left.label.localeCompare(right.label, 'zh-Hans-CN'))
}

function buildRouteUploadTargetOptions(configuration: EdgeCollectionConfiguration) {
  const targets = getUploadTargets(configuration)
  const targetMap = new Map<string, RouteUploadTargetOption>()

  targets.forEach((upload, index) => {
    const targetKey = normalizeStructuralKey(upload.targetKey)
    if (!targetKey) return
    targetMap.set(targetKey, {
      value: upload.targetKey ?? '',
      label: buildUploadTargetOptionLabel(upload, index)
    })
  })

  for (const route of Array.isArray(configuration.uploadRoutes) ? configuration.uploadRoutes : []) {
    const targetKey = normalizeStructuralKey(route.uploadTargetKey)
    if (!targetKey || targetMap.has(targetKey)) {
      continue
    }

    targetMap.set(targetKey, {
      value: route.uploadTargetKey ?? '',
      label: buildMissingUploadTargetLabel(route.uploadTargetKey ?? '')
    })
  }

  return Array.from(targetMap.values()).sort((left, right) => left.label.localeCompare(right.label, 'zh-Hans-CN'))
}

function buildRouteRows(configuration: EdgeCollectionConfiguration) {
  const uploads = getUploadTargets(configuration)
  const uploadMap = new Map(uploads.map((upload) => [normalizeStructuralKey(upload.targetKey), upload] as const))
  const rows: RouteRow[] = []

  for (const [index, route] of (Array.isArray(configuration.uploadRoutes) ? configuration.uploadRoutes : []).entries()) {
    const point = findPointByRef(configuration, route.taskKey ?? '', route.deviceKey ?? '', route.pointKey ?? '')
    const upload = uploadMap.get(normalizeStructuralKey(route.uploadTargetKey))
    const pointTargetName = String(point?.point.mapping?.targetName ?? '').trim()
    rows.push({
      configIndex: index,
      taskKey: route.taskKey ?? '',
      deviceKey: route.deviceKey ?? '',
      pointKey: route.pointKey ?? '',
      pointLabel: point ? buildPointOptionLabel(point.task, point.device, point.point) : buildMissingPointOptionLabel(route.taskKey ?? '', route.deviceKey ?? '', route.pointKey ?? ''),
      uploadTargetKey: route.uploadTargetKey ?? '',
      uploadTargetLabel: upload ? buildUploadTargetOptionLabel(upload, uploads.indexOf(upload)) : buildMissingUploadTargetLabel(route.uploadTargetKey ?? ''),
      targetName: route.targetName?.trim() || pointTargetName || route.pointKey || '',
      payloadTemplate: route.payloadTemplate ?? '',
      enabled: route.enabled !== false,
    })
  }

  return rows.sort((left, right) => {
    const pointCompare = left.pointLabel.localeCompare(right.pointLabel, 'zh-Hans-CN')
    if (pointCompare !== 0) return pointCompare
    return left.uploadTargetLabel.localeCompare(right.uploadTargetLabel, 'zh-Hans-CN')
  })
}

function enumeratePoints(configuration: EdgeCollectionConfiguration) {
  const items: Array<{ task: CollectionTask; device: CollectionDevice; point: CollectionPoint }> = []
  for (const task of Array.isArray(configuration.tasks) ? configuration.tasks : []) {
    for (const device of Array.isArray(task.devices) ? task.devices : []) {
      for (const point of Array.isArray(device.points) ? device.points : []) {
        items.push({ task, device, point })
      }
    }
  }

  return items
}

function findPointByRef(configuration: EdgeCollectionConfiguration, taskKey: string, deviceKey: string, pointKey: string) {
  const pointRef = buildPointRef(taskKey, deviceKey, pointKey)
  return enumeratePoints(configuration).find(({ task, device, point }) =>
    buildPointRef(task.taskKey ?? '', device.deviceKey ?? '', point.pointKey ?? '') === pointRef)
}

function resolvePointTargetName(configuration: EdgeCollectionConfiguration, pointRef: string) {
  const parsed = parsePointRef(pointRef)
  if (!parsed) return ''

  const point = findPointByRef(configuration, parsed.taskKey, parsed.deviceKey, parsed.pointKey)
  return String(point?.point.mapping?.targetName ?? point?.point.pointKey ?? '').trim()
}

function buildPointOptionLabel(task: CollectionTask, device: CollectionDevice, point: CollectionPoint) {
  const taskKey = task.taskKey?.trim() || '--'
  const deviceLabel = device.deviceName?.trim() || device.deviceKey?.trim() || '--'
  const pointLabel = point.pointName?.trim() || point.pointKey?.trim() || '--'
  const pointKey = point.pointKey?.trim()
  return pointKey ? `${taskKey} / ${deviceLabel} / ${pointLabel} · ${pointKey}` : `${taskKey} / ${deviceLabel} / ${pointLabel}`
}

function buildMissingPointOptionLabel(taskKey: string, deviceKey: string, pointKey: string) {
  return `无效点位 · ${taskKey || '--'} / ${deviceKey || '--'} / ${pointKey || '--'}`
}

function buildUploadTargetOptionLabel(upload: CollectionUpload, index = 0) {
  const fallback = `上传目标 ${index + 1}`
  const name = upload.displayName?.trim() || upload.targetKey?.trim() || fallback
  const targetKey = upload.targetKey?.trim() || '--'
  return `${name} · ${targetKey} · ${displayUploadProtocol(upload.protocol)}`
}

function buildMissingUploadTargetLabel(targetKey: string) {
  return `无效目标 · ${targetKey || '--'}`
}

function buildPointRef(taskKey: string, deviceKey: string, pointKey: string) {
  return stringJoinKey(taskKey, deviceKey, pointKey)
}

function parsePointRef(value: string) {
  const parts = String(value ?? '').split('::')
  if (parts.length !== 3 || parts.some((part) => !part)) {
    return null
  }

  return {
    taskKey: parts[0],
    deviceKey: parts[1],
    pointKey: parts[2],
  }
}

function stringJoinKey(...values: Array<unknown>) {
  return values.map((value) => normalizeStructuralKey(value)).join('::')
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

function createPoint(protocol: CollectionProtocolDescriptor, index: number, points: CollectionPoint[] = []) {
  const point = defaultPoint(index, protocol)
  point.pointKey = uniquePointKey(points, point.pointKey ?? 'point')
  point.pointName = point.pointName?.trim() || point.pointKey
  if (point.mapping) {
    point.mapping.targetName = uniquePointTargetName(points, point.pointKey ?? 'point')
    point.mapping.displayName = point.pointName
  }

  return point
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

function uniquePointKey(points: CollectionPoint[], suggested: string) {
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

function uniquePointTargetName(points: CollectionPoint[], suggested: string) {
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

function getUploadTargets(configuration: EdgeCollectionConfiguration): CollectionUpload[] {
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

function findConfiguredUploadProtocol(configuration: EdgeCollectionConfiguration) {
  const targets = getUploadTargets(configuration)
  return (targets.find((upload) => upload.enabled !== false) ?? targets[0])?.protocol?.trim() ?? ''
}

function sameUploadProtocol(left: unknown, right: unknown) {
  return normalizeUploadProtocolKey(left) === normalizeUploadProtocolKey(right)
}

function normalizeUploadProtocolKey(value: unknown) {
  const normalized = normalizeProtocolKey(value)
  if (normalized === 'thingboard') return 'thingsboard'
  if (normalized === 'iotsharpdevicehttp' || normalized === 'iotsharpmqtt') return 'iotsharp'
  if (normalized === 'sonnet') return 'sonnetdb'
  if (normalized === 'influx') return 'influxdb'
  return normalized
}

function uploadEditableSettings(protocol: UploadProtocolDescriptor) {
  return (protocol.connectionSettings ?? []).filter((setting) => normalizeProtocolKey(setting.key) !== 'endpoint')
}

function uploadDefaults(protocol: UploadProtocolDescriptor, index: number) {
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

function createUploadTarget(protocol: UploadProtocolDescriptor, index: number): CollectionUpload {
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

function defaultUploadSettingValue(protocol: UploadProtocolDescriptor, setting: ConnectionSettingDefinition) {
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

function displayUploadProtocol(value?: string) {
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

function displayUploadSummary(configuration: EdgeCollectionConfiguration) {
  const targets = getUploadTargets(configuration)
  if (!targets.length) return '--'
  if (targets.length === 1) return displayUploadProtocol(targets[0].protocol)

  const protocolLabels = [...new Set(targets.map((upload) => displayUploadProtocol(upload.protocol)))]
  return `${targets.length} 个目标 · ${protocolLabels.slice(0, 2).join(' / ')}`
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
  connectionstring: '连接串',
  token: '令牌',
  accesstoken: '访问令牌',
  devicetoken: '设备令牌',
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
  targetkey: '目标键',
  displayname: '显示名称',
  enabled: '启用',
  batchsize: '批大小',
  bufferingenabled: '启用缓冲',
  database: '数据库',
  bucket: 'Bucket',
  org: '组织',
  measurement: '测量',
  field: '字段',
  site: '站点',
  includerawvalue: '包含原始值',
  rawfield: '原始值字段',
  precision: '精度',
  retentionpolicy: '保留策略',
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
  connectionstring: '完整连接串。',
  token: '访问令牌。',
  accesstoken: '访问令牌。',
  devicetoken: '设备令牌。',
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
  targetkey: '上传目标键。',
  displayname: '上传目标显示名称。',
  enabled: '是否启用该上传目标。',
  batchsize: '批量写入大小。',
  bufferingenabled: '是否启用缓冲。',
  database: '数据库名称。',
  bucket: 'Bucket 名称。',
  org: '组织名称。',
  measurement: '测量名称。',
  field: '默认字段名称。',
  site: '站点标识。',
  includerawvalue: '同时写入原始值。',
  rawfield: '原始值字段名称。',
  precision: '时间精度。',
  retentionpolicy: '保留策略。',
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

function normalizeStructuralKey(value: unknown) {
  return String(value ?? '').trim().toLowerCase()
}

function normalizeTargetName(value: string) {
  return String(value ?? '')
    .trim()
    .replace(/[^a-z0-9]+/gi, '_')
    .replace(/^_+|_+$/g, '')
    .toLowerCase()
}
</script>

<template>
  <div class="shell">
    <AppSidebar
      :active-panel="activePanel"
      :edge-api-base-url="edgeApiBaseUrl"
      :protocol-groups="protocolGroups"
      :selected-protocol-code="selectedProtocolCode"
      :upload-protocol-groups="uploadProtocolGroups"
      :selected-upload-protocol-code="selectedUploadProtocolCode"
      :status-text="statusText"
      :status-tone="statusTone"
      :display-protocol-category="displayProtocolCategory"
      :display-lifecycle-label="displayLifecycleLabel"
      :display-status-text="displayStatusText"
      :lifecycle-class="lifecycleClass"
      :same-protocol="sameProtocol"
      :same-upload-protocol="sameUploadProtocol"
      @switch-panel="switchPanel"
      @select-protocol="selectProtocol"
      @select-upload-protocol="selectUploadProtocol"
    />

    <main class="content">
      <AppTopbar
        :page-title="pageTitle"
        :runtime-summary="runtimeSummary"
        :base-configuration="baseConfiguration"
        :is-loading="isLoading"
        :is-saving="isSaving"
        :json-parse-error="jsonParseError"
        :display-sync-status="displaySyncStatus"
        :display-updated-by="displayUpdatedBy"
        :display-upload-summary="displayUploadSummary"
        @refresh="loadAll"
        @apply="applyConfiguration"
        @reset="resetConfiguration"
      />

      <SummaryGrid :cards="dashboardCards" />

      <section class="workspace">
        <DashboardPanel
          v-show="activePanel === 'dashboard'"
          :runtime-summary="runtimeSummary"
          :format-bytes="formatBytes"
        />

        <TopologyPanel
          v-show="activePanel === 'topology'"
          v-model:configuration-text="configurationText"
          :selected-protocol="selectedProtocol"
          :protocol-flags="protocolFlags"
          :protocol-task-count="protocolTaskCount"
          :visible-connection-settings="visibleConnectionSettings"
          :selected-device-count="selectedDeviceCount"
          :selected-device-index="selectedDeviceIndex"
          :topology-devices="topologyDevices"
          :selected-device="selectedDevice"
          :selected-device-summary="selectedDeviceSummary"
          :is-modbus-protocol="isModbusProtocol"
          :topology-form="topologyForm"
          :connection-values="connectionValues"
          :point-rows="pointRows"
          :topology-rows="topologyRows"
          :json-parse-error="jsonParseError"
          :is-saving="isSaving"
          :display-protocol-category="displayProtocolCategory"
          :display-lifecycle-label="displayLifecycleLabel"
          :lifecycle-class="lifecycleClass"
          :display-capability="displayCapability"
          :display-risk-label="displayRiskLabel"
          :display-device-label="displayDeviceLabel"
          :is-boolean-setting="isBooleanSetting"
          :is-select-setting="isSelectSetting"
          :display-connection-setting-label="displayConnectionSettingLabel"
          :display-connection-setting-options="displayConnectionSettingOptions"
          :display-connection-option="displayConnectionOption"
          :input-type="inputType"
          :display-connection-setting-description="displayConnectionSettingDescription"
          :point-source-options="pointSourceOptions"
          :display-point-source="displayPointSource"
          :mark-topology-dirty="markTopologyDirty"
          :handle-device-select-change="handleDeviceSelectChange"
          :add-device="addDevice"
          :save-current-device-configuration="saveCurrentDeviceConfiguration"
          :remove-device="removeDevice"
          :handle-boolean-connection-value="handleBooleanConnectionValue"
          :add-point="addPoint"
          :sync-forms-to-json="syncFormsToJson"
          :remove-point="removePoint"
          @json-input="onJsonInput"
        />

        <UploadTargetsPanel
          v-show="activePanel === 'upload'"
          v-model:configuration-text="configurationText"
          :selected-upload-protocol="selectedUploadProtocol"
          :selected-upload-targets="selectedUploadTargets"
          :selected-upload-target="selectedUploadTarget"
          :selected-upload-target-count="selectedUploadTargetCount"
          :selected-upload-target-index="selectedUploadTargetIndex"
          :upload-protocol-flags="uploadProtocolFlags"
          :visible-upload-settings="visibleUploadSettings"
          :upload-form="uploadForm"
          :upload-setting-values="uploadSettingValues"
          :json-parse-error="jsonParseError"
          :is-saving="isSaving"
          :lifecycle-class="lifecycleClass"
          :display-lifecycle-label="displayLifecycleLabel"
          :display-upload-protocol="displayUploadProtocol"
          :select-upload-target="selectUploadTarget"
          :add-upload-target="addUploadTarget"
          :remove-upload-target="removeUploadTarget"
          :mark-upload-dirty="markUploadDirty"
          :is-boolean-setting="isBooleanSetting"
          :is-select-setting="isSelectSetting"
          :display-connection-setting-label="displayConnectionSettingLabel"
          :display-connection-setting-options="displayConnectionSettingOptions"
          :display-connection-option="displayConnectionOption"
          :input-type="inputType"
          :display-connection-setting-description="displayConnectionSettingDescription"
          :handle-upload-setting-boolean-change="handleUploadSettingBooleanChange"
          :sync-forms-to-json="syncFormsToJson"
          :apply-configuration="applyConfiguration"
          @json-input="onJsonInput"
        />

        <RoutingPanel
          v-show="activePanel === 'routing'"
          v-model:configuration-text="configurationText"
          :route-rows="routeRows"
          :selected-route="selectedRoute"
          :selected-route-count="selectedRouteCount"
          :selected-route-index="selectedRouteIndex"
          :route-point-options="routePointOptions"
          :route-upload-target-options="routeUploadTargetOptions"
          :route-form="routeForm"
          :json-parse-error="jsonParseError"
          :is-saving="isSaving"
          :select-route="selectRoute"
          :add-route="addRoute"
          :remove-route="removeRoute"
          :handle-route-point-change="handleRoutePointChange"
          :mark-route-dirty="markRouteDirty"
          :sync-forms-to-json="syncFormsToJson"
          :apply-configuration="applyConfiguration"
          @json-input="onJsonInput"
        />

        <ScriptPanel v-show="activePanel === 'script'" :script-text="scriptText" />
        <LogsPanel v-show="activePanel === 'logs'" :log-lines="logLines" />
        <BootstrapPanel v-show="activePanel === 'bootstrap'" :bootstrap-json="bootstrapJson" />
      </section>
    </main>
  </div>
</template>
