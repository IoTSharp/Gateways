<script setup lang="ts">
import { AlertTriangle, Plus, Save, Trash2 } from 'lucide-vue-next'
import type {
  CollectionDevice,
  CollectionProtocolDescriptor,
  ConnectionSettingDefinition,
} from '../types'
import type { PointRow, TopologyRow } from '../uiTypes'

defineProps<{
  selectedProtocol: CollectionProtocolDescriptor
  protocolFlags: string[]
  protocolTaskCount: number
  visibleConnectionSettings: ConnectionSettingDefinition[]
  selectedDeviceCount: number
  selectedDeviceIndex: number
  topologyDevices: CollectionDevice[]
  selectedDevice: CollectionDevice | null
  selectedDeviceSummary: string
  isModbusProtocol: boolean
  topologyForm: {
    taskKey: string
    connectionName: string
    deviceKey: string
    deviceName: string
    stationNumber: string
  }
  connectionValues: Record<string, string>
  pointRows: PointRow[]
  topologyRows: TopologyRow[]
  configurationText: string
  jsonParseError: string
  isSaving: boolean
  displayProtocolCategory: (value: string) => string
  displayLifecycleLabel: (value: string) => string
  lifecycleClass: (value: string) => string
  displayCapability: (value: string) => string
  displayRiskLabel: (value: string) => string
  displayDeviceLabel: (device?: CollectionDevice | null, index?: number) => string
  isBooleanSetting: (setting: ConnectionSettingDefinition) => boolean
  isSelectSetting: (setting: ConnectionSettingDefinition) => boolean
  displayConnectionSettingLabel: (setting: ConnectionSettingDefinition) => string
  displayConnectionSettingOptions: (setting: ConnectionSettingDefinition) => string[]
  displayConnectionOption: (setting: ConnectionSettingDefinition, option: string) => string
  inputType: (setting: ConnectionSettingDefinition) => string
  displayConnectionSettingDescription: (setting: ConnectionSettingDefinition) => string
  pointSourceOptions: (protocol: CollectionProtocolDescriptor) => string[]
  displayPointSource: (value: string) => string
  markTopologyDirty: () => void
  handleDeviceSelectChange: (event: Event) => void
  addDevice: () => void
  saveCurrentDeviceConfiguration: () => unknown
  removeDevice: () => void
  handleBooleanConnectionValue: (key: string, event: Event) => void
  addPoint: () => void
  syncFormsToJson: (message?: string) => void
  removePoint: (index: number) => void
}>()

const emit = defineEmits<{
  'update:configurationText': [value: string]
  'json-input': []
}>()

function handleJsonInput(event: Event) {
  emit('update:configurationText', (event.target as HTMLTextAreaElement).value)
  emit('json-input')
}
</script>

<template>
  <section class="panel-group">
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
          <div v-if="protocolTaskCount > 1" class="inline-warning">
            <AlertTriangle :size="15" />
            <span>检测到 {{ protocolTaskCount }} 个同协议任务，当前页面只编辑第一个任务。</span>
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
              <button type="button" :disabled="isSaving || !!jsonParseError" @click="saveCurrentDeviceConfiguration">
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
          <textarea :value="configurationText" spellcheck="false" @input="handleJsonInput"></textarea>
          <div v-if="jsonParseError" class="inline-warning">
            <AlertTriangle :size="15" />
            <span>{{ jsonParseError }}</span>
          </div>
        </div>
      </div>
    </article>
  </section>
</template>
