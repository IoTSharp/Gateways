import type {
  CollectionProtocolDescriptor,
  CollectionTask,
  ConnectionSettingDefinition,
} from '../types'

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

export function displayStatusText(value: string) {
  const raw = value?.trim() ?? ''
  if (!raw) return '等待中'
  if (raw.startsWith('最近刷新：')) return raw
  return statusLabels[normalizeProtocolKey(raw)] ?? raw
}

export function displayCapability(value: string) {
  return capabilityLabels[normalizeProtocolKey(value)] ?? value
}

export function displayLifecycleLabel(value: string) {
  return lifecycleLabels[normalizeProtocolKey(value)] ?? '可用'
}

export function displayRiskLabel(value: string) {
  return riskLabels[normalizeProtocolKey(value)] ?? value
}

export function displayProtocolCategory(value: string) {
  const raw = value?.trim() ?? ''
  if (!raw) return '其他协议'
  if (raw === '其他协议') return raw
  return protocolCategoryLabels[normalizeProtocolKey(raw)] ?? raw
}

export function displayValueType(value: string) {
  return valueTypeLabels[normalizeProtocolKey(value)] ?? value
}

export function displayPointSource(value: string) {
  return pointSourceLabels[normalizeProtocolKey(value)] ?? value
}

export function displayConnectionSettingLabel(setting: ConnectionSettingDefinition) {
  const key = normalizeProtocolKey(setting.key)
  return connectionSettingLabels[key] ?? setting.label
}

export function displayConnectionSettingDescription(setting: ConnectionSettingDefinition) {
  const key = normalizeProtocolKey(setting.key)
  return connectionSettingDescriptions[key] ?? setting.description
}

export function normalizeConnectionSettingOption(setting: ConnectionSettingDefinition, option: string) {
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

export function displayConnectionSettingOptions(setting: ConnectionSettingDefinition, protocol?: CollectionProtocolDescriptor) {
  const options = [...(setting.options ?? [])]
  if (protocol && normalizeProtocolKey(protocol.contractProtocol) === 'modbus' && normalizeProtocolKey(setting.key) === 'transport') {
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

export function displayConnectionOption(setting: ConnectionSettingDefinition, option: string) {
  const key = normalizeProtocolKey(setting.key)
  const options = connectionOptionLabels[key]
  if (!options) return option

  return options[normalizeProtocolKey(option)] ?? option
}

export function displaySyncStatus(value?: string) {
  if (!value) return '离线'
  return syncStatusLabels[normalizeProtocolKey(value)] ?? value
}

export function displayLogLevel(value: string) {
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

export function displayUpdatedBy(value?: string) {
  const raw = value?.trim() ?? ''
  if (!raw) return '本地'
  const normalized = normalizeProtocolKey(raw)
  if (normalized === 'local' || normalized === 'localedge' || normalized === 'localconfiguration') return '本地'
  if (normalized === 'localdockertemplate') return 'Docker 模板'
  return raw
}

export function lifecycleLabel(value: string) {
  return displayLifecycleLabel(value)
}

export function lifecycleClass(value: string) {
  return normalizeProtocolKey(value || 'ready')
}

export function formatLogEntry(entry: { timestampUtc: string; level: string; category: string; message: string; exception?: string }) {
  const exception = entry.exception ? `\n${entry.exception}` : ''
  return `[${formatDate(entry.timestampUtc)}] ${displayLogLevel(entry.level)} ${entry.category}\n${entry.message}${exception}`
}

export function formatDate(value?: string) {
  if (!value) return '--'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString()
}

export function formatBytes(value?: number) {
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

export function normalizeProtocolKey(value: unknown) {
  return String(value ?? '')
    .trim()
    .replace(/[^a-z0-9]+/gi, '')
    .toLowerCase()
}

export function sameProtocol(left: unknown, right: unknown) {
  return normalizeProtocolKey(left) === normalizeProtocolKey(right)
}

export function defaultSettingValue(protocol: CollectionProtocolDescriptor, setting: ConnectionSettingDefinition) {
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

export function defaultTimeout(protocol: CollectionProtocolDescriptor) {
  return normalizeProtocolKey(protocol.code) === 'fanuccnc' ? 5000 : 3000
}

export function protocolDefaultPort(protocol: CollectionProtocolDescriptor) {
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

export function defaultPointAddress(protocol: CollectionProtocolDescriptor, index: number) {
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

export function pointSourceOptions(protocol: CollectionProtocolDescriptor) {
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

export function coerceSettingValue(value: string, valueType: string) {
  const normalized = valueType.toLowerCase()
  if (normalized === 'number') return value === '' ? undefined : toNumber(value, 0)
  if (normalized === 'boolean') return value === 'true'
  return value
}

export function settingToString(value: unknown, fallback = '') {
  if (value === undefined || value === null || value === '') return fallback
  return String(value)
}

export function isBooleanSetting(setting: ConnectionSettingDefinition) {
  return setting.valueType.toLowerCase() === 'boolean'
}

export function isSelectSetting(setting: ConnectionSettingDefinition) {
  return setting.valueType.toLowerCase() === 'select' || (setting.options?.length ?? 0) > 0
}

export function isConnectionSettingVisible(setting: ConnectionSettingDefinition, protocol: CollectionProtocolDescriptor, transportValue?: string) {
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

export function inputType(setting: ConnectionSettingDefinition) {
  const valueType = setting.valueType.toLowerCase()
  if (valueType === 'password') return 'password'
  if (valueType === 'number') return 'number'
  return 'text'
}

export function readModbusTransport(connection: CollectionTask['connection'], protocol: CollectionProtocolDescriptor) {
  return normalizeModbusTransportValue(
    connection?.transport
    || asRecord(connection?.protocolOptions).transport
    || defaultSettingValue(protocol, { key: 'transport', label: '传输方式', valueType: 'select', required: true, description: '', options: [] }),
  )
}

export function readModbusTransportValue(value?: unknown) {
  const normalized = normalizeProtocolKey(value)
  if (['serialrtu', 'serialascii', 'serialdtu', 'rtu', 'ascii', 'dtu', 'serial'].includes(normalized)) {
    return 'serial'
  }

  if (['rtuovertcp', 'tcp'].includes(normalized)) {
    return normalized
  }

  return 'tcp'
}

export function normalizeModbusTransportValue(value?: unknown) {
  const normalized = normalizeProtocolKey(value)
  if (normalized === 'rtuovertcp') return 'rtuOverTcp'
  if (['serialrtu', 'rtu', 'serial'].includes(normalized)) return 'serialRtu'
  if (normalized === 'serialdtu' || normalized === 'dtu') return 'dtu'
  if (['serialascii', 'ascii'].includes(normalized)) return 'serialAscii'
  return 'tcp'
}

export function normalizeModbusConnection(connection: NonNullable<CollectionTask['connection']>, options: Record<string, unknown>, transportValue?: string) {
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

export function normalizeConnectionForTransport(
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

export function asRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value)
    ? { ...(value as Record<string, unknown>) }
    : {}
}

export function pruneEmpty(value: Record<string, unknown>) {
  return Object.fromEntries(Object.entries(value).filter(([, entry]) => entry !== undefined && entry !== null)) as Record<string, unknown>
}

export function clearRecord(record: Record<string, string>) {
  for (const key of Object.keys(record)) {
    delete record[key]
  }
}

export function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value ?? {})) as T
}

export function toNumber(value: unknown, fallback: number) {
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : fallback
}

export function keyFrom(value: unknown, fallback: string) {
  const key = String(value ?? '')
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')

  return key || fallback
}

export function newGuid() {
  return globalThis.crypto?.randomUUID?.()
    ?? 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (token) => {
      const random = Math.random() * 16 | 0
      const value = token === 'x' ? random : (random & 0x3) | 0x8
      return value.toString(16)
    })
}

export function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : String(error)
}

export function normalizeStructuralKey(value: unknown) {
  return String(value ?? '').trim().toLowerCase()
}

export function normalizeTargetName(value: string) {
  return String(value ?? '')
    .trim()
    .replace(/[^a-z0-9]+/gi, '_')
    .replace(/^_+|_+$/g, '')
    .toLowerCase()
}
