import type { CollectionProtocolDescriptor, UploadProtocolDescriptor } from '../types'

export const fallbackProtocol: CollectionProtocolDescriptor = {
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
}

export const fallbackUploadProtocol: UploadProtocolDescriptor = {
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
}
