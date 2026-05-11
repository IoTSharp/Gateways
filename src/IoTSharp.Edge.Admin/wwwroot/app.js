const state = {
  edgeApiBaseUrl: 'http://127.0.0.1:18180',
  localConfiguration: null,
  runtimeSummary: null,
  protocolCatalog: [],
  selectedTopologyProtocolCode: 'modbus',
  script: null,
  logs: null,
  uploadFormDirty: false,
  topologyFormDirty: false,
}

const elements = {
  apiStatus: document.getElementById('apiStatus'),
  syncStatus: document.getElementById('syncStatus'),
  metricRuntime: document.getElementById('metricRuntime'),
  metricConfig: document.getElementById('metricConfig'),
  metricUpload: document.getElementById('metricUpload'),
  summaryHealth: document.getElementById('summaryHealth'),
  summaryProcess: document.getElementById('summaryProcess'),
  summaryLocalConfig: document.getElementById('summaryLocalConfig'),
  summaryLocalPath: document.getElementById('summaryLocalPath'),
  summarySonnet: document.getElementById('summarySonnet'),
  summarySonnetMeta: document.getElementById('summarySonnetMeta'),
  summaryTopology: document.getElementById('summaryTopology'),
  summaryTopologyMeta: document.getElementById('summaryTopologyMeta'),
  runtimeBadge: document.getElementById('runtimeBadge'),
  runtimeJson: document.getElementById('runtimeJson'),
  topologyProtocolNav: document.getElementById('topologyProtocolNav'),
  topologyPanelTitle: document.getElementById('topologyPanelTitle'),
  topologyPanelSubtitle: document.getElementById('topologyPanelSubtitle'),
  topologyPanelBadge: document.getElementById('topologyPanelBadge'),
  topologyProtocolSummary: document.getElementById('topologyProtocolSummary'),
  topologyTable: document.getElementById('topologyTable'),
  configurationEditor: document.getElementById('configurationEditor'),
  uploadSummary: document.getElementById('uploadSummary'),
  uploadEndpoint: document.getElementById('uploadEndpoint'),
  uploadDatabase: document.getElementById('uploadDatabase'),
  uploadToken: document.getElementById('uploadToken'),
  uploadMeasurement: document.getElementById('uploadMeasurement'),
  uploadField: document.getElementById('uploadField'),
  uploadSite: document.getElementById('uploadSite'),
  syncUploadButton: document.getElementById('syncUploadButton'),
  applyUploadButton: document.getElementById('applyUploadButton'),
  topologyTaskKey: document.getElementById('topologyTaskKey'),
  topologyConnectionName: document.getElementById('topologyConnectionName'),
  topologyHost: document.getElementById('topologyHost'),
  topologyPort: document.getElementById('topologyPort'),
  topologyDeviceKey: document.getElementById('topologyDeviceKey'),
  topologyDeviceName: document.getElementById('topologyDeviceName'),
  topologyStation: document.getElementById('topologyStation'),
  topologyTimeout: document.getElementById('topologyTimeout'),
  pointsTableBody: document.getElementById('pointsTableBody'),
  addPointButton: document.getElementById('addPointButton'),
  syncTopologyButton: document.getElementById('syncTopologyButton'),
  applyTopologyButton: document.getElementById('applyTopologyButton'),
  scriptOutput: document.getElementById('scriptOutput'),
  logsOutput: document.getElementById('logsOutput'),
  bootstrapOutput: document.getElementById('bootstrapOutput'),
  reloadButton: document.getElementById('reloadButton'),
  applyButton: document.getElementById('applyButton'),
  resetButton: document.getElementById('resetButton'),
}

function setStatus(message, tone = 'info') {
  elements.syncStatus.textContent = message
  elements.syncStatus.dataset.tone = tone
}

function formatDate(value) {
  if (!value) return '--'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

function apiUrl(path) {
  return `${state.edgeApiBaseUrl.replace(/\/$/, '')}${path}`
}

async function requestAdmin(path, init) {
  const response = await fetch(path, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  })
  const text = await response.text()

  if (!response.ok) {
    throw new Error(readErrorMessage(text) || `${response.status} ${response.statusText}`)
  }

  return text ? JSON.parse(text) : {}
}

async function request(path, init) {
  const response = await fetch(apiUrl(path), {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  })
  const text = await response.text()

  if (!response.ok) {
    throw new Error(readErrorMessage(text) || `${response.status} ${response.statusText}`)
  }

  return text ? JSON.parse(text) : {}
}

function readErrorMessage(text) {
  if (!text) return ''
  try {
    const parsed = JSON.parse(text)
    return parsed.message || text
  } catch {
    return text
  }
}

function clone(value) {
  return value == null ? {} : JSON.parse(JSON.stringify(value))
}

function asSettings(value) {
  return value && typeof value === 'object' && !Array.isArray(value) ? { ...value } : {}
}

function toNumber(value, fallback) {
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : fallback
}

function newGuid() {
  if (crypto.randomUUID) {
    return crypto.randomUUID()
  }

  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (token) => {
    const random = Math.random() * 16 | 0
    const value = token === 'x' ? random : (random & 0x3 | 0x8)
    return value.toString(16)
  })
}

function keyFrom(value, fallback) {
  const key = String(value ?? '')
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')

  return key || fallback
}

function normalizeProtocolKey(value) {
  return String(value ?? '')
    .trim()
    .replace(/[^a-z0-9]+/gi, '')
    .toLowerCase()
}

function getFallbackProtocol() {
  return {
    code: 'modbus',
    contractProtocol: 'Modbus',
    displayName: 'Modbus',
    category: 'PLC',
    description: 'Modbus TCP / RTU over TCP 采集。',
    lifecycle: 'ready',
    supportsRead: true,
    supportsWrite: true,
    supportsBatchRead: true,
    supportsBatchWrite: true,
    riskLevel: 'normal',
    connectionSettings: [],
  }
}

function getSelectedProtocol() {
  const protocol = (state.protocolCatalog || []).find((item) => normalizeProtocolKey(item.code) === normalizeProtocolKey(state.selectedTopologyProtocolCode))
  return protocol || (state.protocolCatalog || []).find((item) => normalizeProtocolKey(item.code) === 'modbus') || getFallbackProtocol()
}

function getProtocolByContractProtocol(protocolName) {
  const normalized = normalizeProtocolKey(protocolName)
  return (state.protocolCatalog || []).find((item) => normalizeProtocolKey(item.contractProtocol) === normalized)
    || (state.protocolCatalog || []).find((item) => normalizeProtocolKey(item.code) === normalized)
    || null
}

function getProtocolDefaults(protocol) {
  const code = normalizeProtocolKey(protocol?.code)

  return {
    taskKey: code === 'modbus' ? 'modbus-device-simulator' : `${code || 'protocol'}-collector`,
    connectionName: protocol?.displayName ? `${protocol.displayName} Connection` : 'Protocol Connection',
    host: code === 'modbus' ? 'device-simulator' : '127.0.0.1',
    port: protocolDefaultPort(code),
    deviceKey: code === 'modbus' ? 'device-simulator-01' : `${code || 'protocol'}-01`,
    deviceName: protocol?.displayName ? `${protocol.displayName} 01` : 'Device 01',
    stationNumber: '1',
  }
}

function protocolDefaultPort(code) {
  return {
    modbus: 1502,
    'opc-ua': 4840,
    'siemens-s7': 102,
    mitsubishi: 6000,
    'omron-fins': 9600,
    'allen-bradley': 44818,
    'mt-cnc': 5000,
    'opc-da': 135,
    'fanuc-cnc': 8193,
  }[code] || 502
}

function normalizeConfigurationDocument(document) {
  const configuration = document?.configuration ?? document
  const uploadSettings = asSettings(configuration?.upload?.settings)
  const tasks = Array.isArray(configuration?.tasks) ? configuration.tasks : []
  const points = tasks.flatMap((task) => Array.isArray(task?.devices) ? task.devices : [])
    .flatMap((device) => Array.isArray(device?.points) ? device.points : [])

  return { configuration, uploadSettings, tasks, points }
}

function getEditorConfiguration() {
  const json = elements.configurationEditor.value.trim()
  return json ? JSON.parse(json) : {}
}

function writeEditorConfiguration(configuration) {
  elements.configurationEditor.value = JSON.stringify(configuration ?? {}, null, 2)
}

function ensureLocalTopology(input, protocol = getSelectedProtocol()) {
  const selectedProtocol = protocol || getFallbackProtocol()
  const configuration = clone(input)
  configuration.contractVersion ||= 'edge-collection-v1'
  configuration.edgeNodeId ||= newGuid()
  configuration.version = Math.max(1, toNumber(configuration.version, 1))
  configuration.updatedAt ||= new Date().toISOString()
  configuration.updatedBy ||= 'LocalAdmin'

  if (!Array.isArray(configuration.tasks)) {
    configuration.tasks = []
  }

  let task = configuration.tasks.find((item) => normalizeProtocolKey(item?.protocol) === normalizeProtocolKey(selectedProtocol.contractProtocol))
  if (!task) {
    task = {}
    configuration.tasks.push(task)
  }

  const defaults = getProtocolDefaults(selectedProtocol)
  const contractProtocol = selectedProtocol.contractProtocol || 'Modbus'
  task.id ||= newGuid()
  task.taskKey ||= defaults.taskKey
  task.protocol = contractProtocol
  task.version = Math.max(1, toNumber(task.version, 1))
  task.edgeNodeId ||= configuration.edgeNodeId

  if (!task.connection || typeof task.connection !== 'object') {
    task.connection = {}
  }

  task.connection.connectionKey ||= `${task.taskKey}-connection`
  task.connection.connectionName ||= defaults.connectionName
  task.connection.protocol = contractProtocol
  task.connection.transport ||= contractProtocol === 'Modbus' ? 'tcp' : task.connection.transport || ''
  task.connection.host ||= defaults.host
  task.connection.port = Math.max(1, toNumber(task.connection.port, defaults.port))
  task.connection.timeoutMs = Math.max(100, toNumber(task.connection.timeoutMs, 3000))
  task.connection.retryCount = Math.max(0, toNumber(task.connection.retryCount, 3))
  const connectionOptions = {
    ...asSettings(task.connection.protocolOptions),
  }

  if (contractProtocol === 'Modbus') {
    connectionOptions.endianFormat ||= 'ABCD'
    connectionOptions.plcAddresses ||= 'true'
  }

  task.connection.protocolOptions = connectionOptions

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
  const deviceOptions = {
    ...asSettings(device.protocolOptions),
  }

  if (contractProtocol === 'Modbus') {
    deviceOptions.stationNumber ||= defaults.stationNumber
  }

  device.protocolOptions = deviceOptions

  if (!Array.isArray(device.points)) {
    device.points = []
  }

  task.reportPolicy = {
    defaultTrigger: 'Always',
    includeQuality: true,
    includeTimestamp: true,
    ...(task.reportPolicy && typeof task.reportPolicy === 'object' ? task.reportPolicy : {}),
  }

  return { configuration, task, device }
}

function renderSummary() {
  const summary = state.runtimeSummary
  const local = state.localConfiguration
  const normalized = local ? normalizeConfigurationDocument(local) : null
  const configuration = normalized?.configuration
  const uploadSettings = normalized?.uploadSettings ?? {}

  elements.summaryHealth.textContent = summary?.process?.name ?? '--'
  elements.summaryProcess.textContent = summary
    ? `PID ${summary.process.id} | ${summary.process.threadCount} threads`
    : '--'

  elements.summaryLocalConfig.textContent = local
    ? `v${configuration?.version ?? '--'} ${local.applied ? 'applied' : 'loaded'}`
    : '--'
  elements.summaryLocalPath.textContent = local?.filePath ?? '--'

  elements.summarySonnet.textContent = configuration?.upload?.protocol ?? '--'
  elements.summarySonnetMeta.textContent = configuration?.upload
    ? `${configuration.upload.endpoint || 'no endpoint'} | ${uploadSettings.database || 'no database'}`
    : '--'

  elements.summaryTopology.textContent = `${summary?.counts?.enabledDeviceCount ?? 0} device(s)`
  elements.summaryTopologyMeta.textContent = `${summary?.counts?.enabledPointCount ?? 0} point(s) | ${summary?.counts?.enabledUploadRouteCount ?? 0} route(s)`

  elements.metricRuntime.textContent = summary?.collectionSync?.status ?? 'offline'
  elements.metricConfig.textContent = local?.configuration?.updatedBy ?? 'local'
  elements.metricUpload.textContent = configuration?.upload?.protocol ?? 'SonnetDB'

  elements.runtimeBadge.textContent = summary?.edgeReporting?.enabled ? 'upstream enabled' : 'local mode'
  elements.runtimeJson.textContent = JSON.stringify(summary ?? {}, null, 2)
  elements.bootstrapOutput.textContent = JSON.stringify(summary?.bootstrap ?? {}, null, 2)

  renderProtocolNav()
  renderEditableConfiguration(configuration ?? {})
  renderScript()
  renderLogs()
}

function renderProtocolNav() {
  const protocols = state.protocolCatalog || []
  if (!protocols.length) {
    elements.topologyProtocolNav.innerHTML = '<div class="nav-subempty">协议目录加载中...</div>'
    return
  }

  const groups = protocols.reduce((acc, protocol) => {
    const category = protocol.category || 'Other'
    if (!acc[category]) {
      acc[category] = []
    }
    acc[category].push(protocol)
    return acc
  }, {})

  const categories = Object.keys(groups).sort((left, right) => left.localeCompare(right, 'zh-Hans-CN'))
  elements.topologyProtocolNav.innerHTML = categories.map((category) => `
    <div class="nav-subgroup">
      <div class="nav-subtitle">${escapeHtml(category)} 采集</div>
      ${groups[category]
        .sort((left, right) => left.displayName.localeCompare(right.displayName, 'zh-Hans-CN'))
        .map((protocol) => {
          const isActive = normalizeProtocolKey(protocol.code) === normalizeProtocolKey(state.selectedTopologyProtocolCode) ? ' active' : ''
          const status = protocol.lifecycle === 'guarded' ? ' guarded' : ''
          return `
            <button
              class="nav-subitem${isActive}"
              type="button"
              data-topology-protocol="${escapeAttr(protocol.code)}"
              title="${escapeAttr(protocol.description || protocol.displayName)}"
            >
              ${escapeHtml(protocol.displayName)}
              <span class="nav-subitem-meta${status}">${escapeHtml(protocol.contractProtocol)}</span>
            </button>
          `
        }).join('')}
    </div>
  `).join('')
}

function renderEditableConfiguration(configuration) {
  writeEditorConfiguration(configuration)
  renderTopologyProtocolSummary(configuration)
  renderTopologyTable(configuration)
  renderTopologyEditor(configuration)
  renderUploadSummary(configuration, asSettings(configuration?.upload?.settings))
  state.topologyFormDirty = false
  state.uploadFormDirty = false
}

function renderTopologyProtocolSummary(configuration) {
  const protocol = getSelectedProtocol()
  const descriptorCount = (protocol.connectionSettings || []).length
  const summary = (configuration?.tasks || []).filter((task) => normalizeProtocolKey(task?.protocol) === normalizeProtocolKey(protocol.contractProtocol)).length

  elements.topologyPanelTitle.textContent = `${protocol.displayName} 采集`
  elements.topologyPanelSubtitle.textContent = protocol.description || protocol.lifecycle || '--'
  elements.topologyPanelBadge.textContent = protocol.lifecycle === 'guarded'
    ? 'guarded'
    : protocol.category || 'ready'

  const settings = (protocol.connectionSettings || []).map((setting) => `
    <div class="protocol-setting">
      <span>${escapeHtml(setting.label || setting.key)}</span>
      <strong>${escapeHtml(setting.required ? 'required' : 'optional')} · ${escapeHtml(setting.valueType || '--')}</strong>
    </div>
  `).join('')

  const flags = [
    protocol.supportsRead ? 'read' : '',
    protocol.supportsWrite ? 'write' : '',
    protocol.supportsBatchRead ? 'batch-read' : '',
    protocol.supportsBatchWrite ? 'batch-write' : '',
  ].filter(Boolean)

  elements.topologyProtocolSummary.innerHTML = `
    <div class="protocol-card">
      <div class="protocol-card-head">
        <div class="protocol-card-title">
          <strong>${escapeHtml(protocol.displayName)}</strong>
          <small>${escapeHtml(protocol.category || 'Other')} · ${escapeHtml(protocol.contractProtocol)}</small>
        </div>
        <span class="badge">${escapeHtml(protocol.lifecycle || 'ready')}</span>
      </div>
      <div class="protocol-flags">
        ${flags.map((flag) => `<span class="protocol-flag">${escapeHtml(flag)}</span>`).join('')}
        <span class="protocol-flag">${escapeHtml(protocol.riskLevel || 'normal')}</span>
        <span class="protocol-flag">${escapeHtml(summary)} task(s)</span>
        <span class="protocol-flag">${escapeHtml(descriptorCount)} setting(s)</span>
      </div>
      <div class="protocol-settings">
        ${settings || '<div class="empty">No connection settings defined.</div>'}
      </div>
      <div class="empty">${escapeHtml(protocol.description || '')}</div>
    </div>
  `
}

function renderTopologyTable(configuration) {
  if (!configuration) {
    elements.topologyTable.innerHTML = '<div class="empty">No local configuration loaded.</div>'
    return
  }

  const tasks = Array.isArray(configuration.tasks) ? configuration.tasks : []
  const selectedProtocol = getSelectedProtocol()
  const selectedProtocolName = normalizeProtocolKey(selectedProtocol.contractProtocol)
  const rows = []
  for (const task of tasks) {
    if (normalizeProtocolKey(task.protocol) !== selectedProtocolName) {
      continue
    }

    for (const device of Array.isArray(task.devices) ? task.devices : []) {
      for (const point of Array.isArray(device.points) ? device.points : []) {
        rows.push({
          taskKey: task.taskKey,
          deviceName: device.deviceName || device.deviceKey,
          pointName: point.pointName,
          address: point.address,
          target: point.mapping?.targetName || '--',
          protocol: task.protocol,
        })
      }
    }
  }

  if (!rows.length) {
    elements.topologyTable.innerHTML = `<div class="empty">No ${escapeHtml(selectedProtocol.displayName)} topology configured yet.</div>`
    return
  }

  const table = `
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
        ${rows.map((row) => `
          <tr>
            <td>${escapeHtml(row.taskKey)}</td>
            <td>${escapeHtml(row.deviceName)}</td>
            <td>${escapeHtml(row.pointName)}</td>
            <td><code>${escapeHtml(row.address)}</code></td>
            <td>${escapeHtml(row.target)}</td>
            <td>${escapeHtml(row.protocol)}</td>
          </tr>
        `).join('')}
      </tbody>
    </table>
  `
  elements.topologyTable.innerHTML = table
}

function renderTopologyEditor(configuration) {
  const protocol = getSelectedProtocol()
  const { task, device } = ensureLocalTopology(configuration, protocol)
  const deviceOptions = asSettings(device.protocolOptions)
  const isModbus = normalizeProtocolKey(protocol.contractProtocol) === 'modbus'

  elements.topologyTaskKey.value = task.taskKey ?? ''
  elements.topologyConnectionName.value = task.connection.connectionName ?? ''
  elements.topologyHost.value = task.connection.host ?? ''
  elements.topologyPort.value = task.connection.port ?? protocolDefaultPort(normalizeProtocolKey(protocol.code))
  elements.topologyDeviceKey.value = device.deviceKey ?? ''
  elements.topologyDeviceName.value = device.deviceName ?? ''
  elements.topologyStation.value = isModbus ? (deviceOptions.stationNumber ?? deviceOptions.slaveId ?? '1') : ''
  elements.topologyStation.disabled = !isModbus
  elements.topologyStation.placeholder = isModbus ? '' : 'Modbus only'
  elements.topologyTimeout.value = task.connection.timeoutMs ?? 3000

  const points = Array.isArray(device.points) ? device.points : []
  if (!points.length) {
    elements.pointsTableBody.innerHTML = '<tr><td colspan="10" class="empty">No points.</td></tr>'
    return
  }

  elements.pointsTableBody.innerHTML = points.map((point, index) => renderPointRow(point, index)).join('')
}

function renderPointRow(point, index) {
  const polling = point.polling ?? {}
  const mapping = point.mapping ?? {}
  return `
    <tr data-point-row>
      <td><input data-point-field="pointKey" value="${escapeAttr(point.pointKey ?? '')}"></td>
      <td><input data-point-field="pointName" value="${escapeAttr(point.pointName ?? '')}"></td>
      <td>${renderSelect('sourceType', ['HoldingRegister', 'InputRegister', 'Coil', 'DiscreteInput', 'NodeId', 'Tag', 'DataItem', 'Address'], point.sourceType || 'HoldingRegister')}</td>
      <td><input data-point-field="address" value="${escapeAttr(point.address ?? '')}"></td>
      <td>${renderSelect('rawValueType', ['Float', 'Double', 'Int16', 'Int32', 'Boolean', 'String'], point.rawValueType || 'Float')}</td>
      <td><input data-point-field="length" type="number" min="1" value="${escapeAttr(point.length || 1)}"></td>
      <td><input data-point-field="targetName" value="${escapeAttr(mapping.targetName ?? point.pointKey ?? '')}"></td>
      <td><input data-point-field="unit" value="${escapeAttr(mapping.unit ?? '')}"></td>
      <td><input data-point-field="readPeriodMs" type="number" min="1000" step="1000" value="${escapeAttr(polling.readPeriodMs || 5000)}"></td>
      <td><button class="compact-button" type="button" data-remove-point="${index}">Del</button></td>
    </tr>
  `
}

function renderSelect(field, values, selected) {
  const options = values.map((value) => {
    const isSelected = String(value).toLowerCase() === String(selected).toLowerCase() ? ' selected' : ''
    return `<option value="${escapeAttr(value)}"${isSelected}>${escapeHtml(value)}</option>`
  }).join('')

  return `<select data-point-field="${escapeAttr(field)}">${options}</select>`
}

function renderUploadSummary(configuration, uploadSettings) {
  if (!configuration?.upload) {
    elements.uploadSummary.innerHTML = '<div class="empty">No upload channel configured.</div>'
    elements.uploadEndpoint.value = ''
    elements.uploadDatabase.value = ''
    elements.uploadToken.value = ''
    elements.uploadMeasurement.value = ''
    elements.uploadField.value = ''
    elements.uploadSite.value = ''
    return
  }

  elements.uploadEndpoint.value = configuration.upload.endpoint ?? ''
  elements.uploadDatabase.value = uploadSettings.database ?? ''
  elements.uploadToken.value = uploadSettings.token ?? ''
  elements.uploadMeasurement.value = uploadSettings.measurement ?? ''
  elements.uploadField.value = uploadSettings.field ?? ''
  elements.uploadSite.value = uploadSettings.site ?? ''

  elements.uploadSummary.innerHTML = `
    <div class="info-row"><span>Protocol</span><strong>${escapeHtml(configuration.upload.protocol || '--')}</strong></div>
    <div class="info-row"><span>Endpoint</span><strong>${escapeHtml(configuration.upload.endpoint || '--')}</strong></div>
    <div class="info-row"><span>Database</span><strong>${escapeHtml(uploadSettings.database || '--')}</strong></div>
    <div class="info-row"><span>Measurement</span><strong>${escapeHtml(uploadSettings.measurement || '--')}</strong></div>
    <div class="info-row"><span>Field</span><strong>${escapeHtml(uploadSettings.field || '--')}</strong></div>
    <div class="info-row"><span>Site</span><strong>${escapeHtml(uploadSettings.site || '--')}</strong></div>
  `
}

function renderScript() {
  if (!state.runtimeSummary) return
  elements.scriptOutput.textContent = state.script?.script ?? ''
}

function formatLogEntry(entry) {
  const timestamp = formatDate(entry.timestampUtc)
  return `[${timestamp}] ${entry.level} ${entry.category}\n${entry.message}${entry.exception ? `\n${entry.exception}` : ''}`
}

function renderLogs() {
  const entries = state.logs?.entries ?? []
  elements.logsOutput.textContent = entries.length ? entries.map(formatLogEntry).join('\n\n') : 'No logs.'
}

function readPointField(row, field) {
  return row.querySelector(`[data-point-field="${field}"]`)?.value?.trim() ?? ''
}

function resolveMappingValueType(rawValueType) {
  const normalized = rawValueType.toLowerCase()
  if (normalized.includes('bool')) return 'Boolean'
  if (normalized.includes('int')) return 'Int32'
  if (normalized.includes('string')) return 'String'
  return 'Double'
}

function readPointsFromTable(existingPoints) {
  const rows = Array.from(elements.pointsTableBody.querySelectorAll('tr[data-point-row]'))
  return rows.map((row, index) => {
    const existing = existingPoints[index] ?? {}
    const pointKey = readPointField(row, 'pointKey') || `point-${index + 1}`
    const pointName = readPointField(row, 'pointName') || pointKey
    const rawValueType = readPointField(row, 'rawValueType') || 'Float'
    const targetName = readPointField(row, 'targetName') || pointKey

    return {
      ...existing,
      pointKey,
      pointName,
      sourceType: readPointField(row, 'sourceType') || 'HoldingRegister',
      address: readPointField(row, 'address') || '40001',
      rawValueType,
      length: Math.max(1, toNumber(readPointField(row, 'length'), 1)),
      polling: {
        ...(existing.polling ?? {}),
        readPeriodMs: Math.max(1000, toNumber(readPointField(row, 'readPeriodMs'), 5000)),
        group: existing.polling?.group || 'default',
      },
      mapping: {
        ...(existing.mapping ?? {}),
        targetType: existing.mapping?.targetType || 'Telemetry',
        targetName,
        valueType: existing.mapping?.valueType || resolveMappingValueType(rawValueType),
        displayName: pointName,
        unit: readPointField(row, 'unit') || undefined,
      },
    }
  })
}

function applyTopologyForm(configuration) {
  const protocol = getSelectedProtocol()
  const { configuration: next, task, device } = ensureLocalTopology(configuration, protocol)
  const taskKey = elements.topologyTaskKey.value.trim() || task.taskKey
  const deviceKey = elements.topologyDeviceKey.value.trim() || device.deviceKey
  const stationNumber = String(Math.max(1, toNumber(elements.topologyStation.value, 1)))
  const contractProtocol = protocol.contractProtocol || 'Modbus'
  const isModbus = normalizeProtocolKey(contractProtocol) === 'modbus'

  task.taskKey = taskKey
  task.protocol = contractProtocol
  task.connection = {
    ...task.connection,
    connectionKey: keyFrom(task.connection.connectionKey || taskKey, `${taskKey}-connection`),
    connectionName: elements.topologyConnectionName.value.trim() || task.connection.connectionName,
    protocol: contractProtocol,
    transport: isModbus ? 'tcp' : task.connection.transport || '',
    host: elements.topologyHost.value.trim() || task.connection.host,
    port: Math.max(1, toNumber(elements.topologyPort.value, task.connection.port || protocolDefaultPort(normalizeProtocolKey(protocol.code)))),
    timeoutMs: Math.max(100, toNumber(elements.topologyTimeout.value, task.connection.timeoutMs || 3000)),
    protocolOptions: {
      ...asSettings(task.connection.protocolOptions),
      ...(isModbus
        ? {
            endianFormat: asSettings(task.connection.protocolOptions).endianFormat || 'ABCD',
            plcAddresses: asSettings(task.connection.protocolOptions).plcAddresses || 'true',
          }
        : {}),
    },
  }

  device.deviceKey = deviceKey
  device.deviceName = elements.topologyDeviceName.value.trim() || device.deviceName || deviceKey
  device.externalKey = device.externalKey || deviceKey
  device.enabled = true
  device.protocolOptions = {
    ...asSettings(device.protocolOptions),
    ...(isModbus ? { stationNumber } : {}),
  }
  device.points = readPointsFromTable(device.points ?? [])

  return next
}

function applyUploadForm(configuration) {
  const next = clone(configuration)
  const settings = asSettings(next.upload?.settings)
  settings.database = elements.uploadDatabase.value.trim()
  settings.token = elements.uploadToken.value.trim()
  settings.measurement = elements.uploadMeasurement.value.trim()
  settings.field = elements.uploadField.value.trim()
  settings.site = elements.uploadSite.value.trim()

  next.upload = {
    ...(next.upload ?? {}),
    protocol: 'SonnetDb',
    endpoint: elements.uploadEndpoint.value.trim(),
    settings,
  }

  return next
}

function readMergedConfiguration() {
  let payload = getEditorConfiguration()
  if (state.topologyFormDirty) {
    payload = applyTopologyForm(payload)
  }

  if (state.uploadFormDirty) {
    payload = applyUploadForm(payload)
  }

  return payload
}

function syncFormsToJson(toneMessage) {
  const payload = readMergedConfiguration()
  renderEditableConfiguration(payload)
  setStatus(toneMessage, 'ok')
  return payload
}

function defaultPoint(index, protocol = getSelectedProtocol()) {
  const number = index + 1
  const code = normalizeProtocolKey(protocol?.code)
  const address = {
    modbus: String(40001 + index * 2),
    'opc-ua': `ns=2;s=Device.Value${number}`,
    'siemens-s7': `DB1.DBD${index * 4}`,
    mitsubishi: `D${index + 1}`,
    'omron-fins': `D${index + 1}`,
    'allen-bradley': `Tag${number}`,
    'mt-cnc': 'availability',
    'opc-da': 'Random.Real8',
    'fanuc-cnc': 'status',
  }[code] || `point-${number}`

  return {
    pointKey: `point-${number}`,
    pointName: `Point ${number}`,
    sourceType: 'HoldingRegister',
    address,
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

async function loadAll() {
  setStatus('loading')
  const [config, summary, script, logs, protocols] = await Promise.all([
    request('/api/local/configuration'),
    request('/api/diagnostics/summary'),
    request('/api/scripts/polling'),
    request('/api/diagnostics/logs?count=100&level=Information'),
    request('/api/collection/protocols'),
  ])

  state.localConfiguration = config
  state.runtimeSummary = summary
  state.script = script
  state.logs = logs
  state.protocolCatalog = protocols?.protocols ?? []
  if (!state.protocolCatalog.some((item) => normalizeProtocolKey(item.code) === normalizeProtocolKey(state.selectedTopologyProtocolCode))) {
    state.selectedTopologyProtocolCode = state.protocolCatalog[0]?.code || 'modbus'
  }
  renderSummary()
  elements.apiStatus.textContent = `API: ${state.edgeApiBaseUrl}`
  setStatus(`last refresh ${formatDate(summary.generatedAtUtc)}`, 'ok')
}

async function applyConfiguration() {
  elements.applyButton.disabled = true
  elements.applyTopologyButton.disabled = true
  elements.applyUploadButton.disabled = true
  try {
    const payload = readMergedConfiguration()
    writeEditorConfiguration(payload)
    const result = await request('/api/local/configuration?apply=true', {
      method: 'PUT',
      body: JSON.stringify(payload),
    })
    state.localConfiguration = result
    await loadAll()
    setStatus('configuration saved and applied', 'ok')
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    setStatus(message, 'warn')
  } finally {
    elements.applyButton.disabled = false
    elements.applyTopologyButton.disabled = false
    elements.applyUploadButton.disabled = false
  }
}

async function resetConfiguration() {
  elements.resetButton.disabled = true
  try {
    const result = await request('/api/local/configuration/reset', { method: 'POST' })
    state.localConfiguration = result
    await loadAll()
    setStatus('configuration reset', 'ok')
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    setStatus(message, 'warn')
  } finally {
    elements.resetButton.disabled = false
  }
}

function switchPanel(panelName) {
  document.querySelectorAll('.panel-group').forEach((panel) => {
    panel.classList.toggle('active', panel.dataset.panel === panelName)
  })
  document.querySelectorAll('.nav-item').forEach((button) => {
    button.classList.toggle('active', button.dataset.panel === panelName)
  })
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;')
}

function escapeAttr(value) {
  return escapeHtml(value)
}

function setSelectedTopologyProtocol(protocolCode) {
  let local = state.localConfiguration
  if (local && (state.topologyFormDirty || state.uploadFormDirty)) {
    const payload = readMergedConfiguration()
    local = local.configuration !== undefined
      ? { ...local, configuration: payload }
      : payload
    state.localConfiguration = local
  }

  state.selectedTopologyProtocolCode = protocolCode
  if (local) {
    const configuration = local.configuration ?? local
    renderEditableConfiguration(configuration)
    renderProtocolNav()
    renderLogs()
  }

  switchPanel('topology')
}

document.querySelector('.nav').addEventListener('click', (event) => {
  const protocolButton = event.target.closest('[data-topology-protocol]')
  if (protocolButton) {
    setSelectedTopologyProtocol(protocolButton.dataset.topologyProtocol)
    return
  }

  const button = event.target.closest('[data-panel]')
  if (button) {
    switchPanel(button.dataset.panel)
  }
})

elements.reloadButton.addEventListener('click', loadAll)
elements.applyButton.addEventListener('click', applyConfiguration)
elements.resetButton.addEventListener('click', resetConfiguration)
elements.syncUploadButton.addEventListener('click', () => syncFormsToJson('SonnetDB settings synced to JSON'))
elements.applyUploadButton.addEventListener('click', applyConfiguration)
elements.syncTopologyButton.addEventListener('click', () => syncFormsToJson('topology synced to JSON'))
elements.applyTopologyButton.addEventListener('click', applyConfiguration)
elements.addPointButton.addEventListener('click', () => {
  const payload = readMergedConfiguration()
  const protocol = getSelectedProtocol()
  const { configuration, device } = ensureLocalTopology(payload, protocol)
  device.points.push(defaultPoint(device.points.length, protocol))
  renderEditableConfiguration(configuration)
  setStatus('point added', 'info')
})

elements.configurationEditor.addEventListener('input', () => {
  state.topologyFormDirty = false
  state.uploadFormDirty = false
  setStatus('JSON changed', 'info')
})

;[
  elements.uploadEndpoint,
  elements.uploadDatabase,
  elements.uploadToken,
  elements.uploadMeasurement,
  elements.uploadField,
  elements.uploadSite,
].forEach((input) => input.addEventListener('input', () => {
  state.uploadFormDirty = true
  setStatus('SonnetDB settings changed', 'info')
}))

;[
  elements.topologyTaskKey,
  elements.topologyConnectionName,
  elements.topologyHost,
  elements.topologyPort,
  elements.topologyDeviceKey,
  elements.topologyDeviceName,
  elements.topologyStation,
  elements.topologyTimeout,
].forEach((input) => input.addEventListener('input', () => {
  state.topologyFormDirty = true
  setStatus('topology changed', 'info')
}))

elements.pointsTableBody.addEventListener('input', () => {
  state.topologyFormDirty = true
  setStatus('point changed', 'info')
})

elements.pointsTableBody.addEventListener('click', (event) => {
  const button = event.target.closest('[data-remove-point]')
  if (!button) return

  button.closest('tr')?.remove()
  state.topologyFormDirty = true
  setStatus('point removed', 'info')
})

requestAdmin('/api/frontend/config')
  .then((config) => {
    state.edgeApiBaseUrl = config.edgeApiBaseUrl || state.edgeApiBaseUrl
    return loadAll()
  })
  .catch((error) => {
    const message = error instanceof Error ? error.message : String(error)
    elements.apiStatus.textContent = message
    setStatus(message, 'warn')
  })

setInterval(() => {
  request('/api/diagnostics/logs?count=100&level=Information')
    .then((logs) => {
      state.logs = logs
      renderLogs()
    })
    .catch(() => {})
}, 5000)
