<script setup lang="ts">
import { AlertTriangle, Network, Plus, Save, Trash2 } from 'lucide-vue-next'
import type {
  CollectionUpload,
  ConnectionSettingDefinition,
  UploadProtocolDescriptor,
} from '../types'

defineProps<{
  selectedUploadProtocol: UploadProtocolDescriptor
  selectedUploadTargets: CollectionUpload[]
  selectedUploadTarget: CollectionUpload | null
  selectedUploadTargetCount: number
  selectedUploadTargetIndex: number
  uploadProtocolFlags: string[]
  visibleUploadSettings: ConnectionSettingDefinition[]
  uploadForm: {
    targetKey: string
    displayName: string
    endpoint: string
    enabled: boolean
    batchSize: string
    bufferingEnabled: boolean
  }
  uploadSettingValues: Record<string, string>
  configurationText: string
  jsonParseError: string
  isSaving: boolean
  lifecycleClass: (value: string) => string
  displayLifecycleLabel: (value: string) => string
  displayUploadProtocol: (value?: string) => string
  selectUploadTarget: (index: number) => void
  addUploadTarget: () => void
  removeUploadTarget: () => void
  markUploadDirty: () => void
  isBooleanSetting: (setting: ConnectionSettingDefinition) => boolean
  isSelectSetting: (setting: ConnectionSettingDefinition) => boolean
  displayConnectionSettingLabel: (setting: ConnectionSettingDefinition) => string
  displayConnectionSettingOptions: (setting: ConnectionSettingDefinition) => string[]
  displayConnectionOption: (setting: ConnectionSettingDefinition, option: string) => string
  inputType: (setting: ConnectionSettingDefinition) => string
  displayConnectionSettingDescription: (setting: ConnectionSettingDefinition) => string
  handleUploadSettingBooleanChange: (key: string, event: Event) => void
  syncFormsToJson: (message?: string) => void
  applyConfiguration: () => unknown
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
              <h2>{{ selectedUploadProtocol.displayName }} 上传</h2>
              <small>{{ selectedUploadProtocol.category }} · {{ selectedUploadProtocol.code }}</small>
            </div>
            <span class="badge" :class="lifecycleClass(selectedUploadProtocol.lifecycle)">
              {{ displayLifecycleLabel(selectedUploadProtocol.lifecycle) }}
            </span>
          </div>
          <p>{{ selectedUploadProtocol.description }}</p>
          <div class="protocol-flags">
            <span v-for="flag in uploadProtocolFlags" :key="flag">{{ flag }}</span>
            <span>{{ selectedUploadProtocol.connectionSettings.length }} 个字段</span>
          </div>
        </div>

        <div class="panel-section">
          <div class="panel-head compact">
            <h2>上传目标</h2>
            <div class="actions dense">
              <button type="button" @click="addUploadTarget">
                <Plus :size="15" />
                <span>新增目标</span>
              </button>
              <button type="button" :disabled="!selectedUploadTarget" @click="removeUploadTarget">
                <Trash2 :size="15" />
                <span>删除目标</span>
              </button>
            </div>
          </div>
          <div v-if="selectedUploadTargetCount" class="upload-target-list">
            <button
              v-for="(target, index) in selectedUploadTargets"
              :key="target.targetKey || index"
              class="upload-target-item"
              :class="{ active: index === selectedUploadTargetIndex }"
              type="button"
              @click="selectUploadTarget(index)"
            >
              <div class="upload-target-main">
                <strong>{{ target.displayName || target.targetKey || '未命名目标' }}</strong>
                <small>{{ target.targetKey || '--' }}</small>
              </div>
              <div class="upload-target-meta">
                <span>{{ target.enabled === false ? '已禁用' : '启用' }}</span>
                <span>{{ displayUploadProtocol(target.protocol) }}</span>
                <span>{{ target.batchSize ?? 1 }} 批</span>
              </div>
            </button>
          </div>
          <div v-else class="empty">当前协议还没有上传目标，先新建一个。</div>
        </div>

        <div class="panel-section">
          <div class="panel-head compact">
            <h2>目标配置</h2>
            <span class="badge">{{ selectedUploadTarget ? (selectedUploadTarget.enabled === false ? '已禁用' : '启用') : '新目标草稿' }}</span>
          </div>
          <div class="form-grid">
            <label>目标键<input v-model="uploadForm.targetKey" type="text" @input="markUploadDirty" /></label>
            <label>显示名称<input v-model="uploadForm.displayName" type="text" @input="markUploadDirty" /></label>
            <label>端点<input v-model="uploadForm.endpoint" type="text" @input="markUploadDirty" /></label>
            <label>批大小<input v-model="uploadForm.batchSize" type="number" min="1" @input="markUploadDirty" /></label>
            <label class="checkbox-label">
              <span>启用</span>
              <input v-model="uploadForm.enabled" type="checkbox" @change="markUploadDirty" />
              <small>禁用后不会生成上传通道。</small>
            </label>
            <label class="checkbox-label">
              <span>启用缓冲</span>
              <input v-model="uploadForm.bufferingEnabled" type="checkbox" @change="markUploadDirty" />
              <small>开启后由运行时缓存后批量发送。</small>
            </label>
          </div>
        </div>

        <div class="panel-section">
          <div class="panel-head compact">
            <h2>协议字段</h2>
            <span class="badge">{{ visibleUploadSettings.length }} 个字段</span>
          </div>
          <div v-if="visibleUploadSettings.length" class="form-grid">
            <label
              v-for="setting in visibleUploadSettings"
              :key="setting.key"
              :class="{ 'checkbox-label': isBooleanSetting(setting) }"
            >
              <span>{{ displayConnectionSettingLabel(setting) }}<em v-if="setting.required">*</em></span>
              <select v-if="isSelectSetting(setting)" v-model="uploadSettingValues[setting.key]" @change="markUploadDirty">
                <option v-for="option in displayConnectionSettingOptions(setting)" :key="option" :value="option">
                  {{ displayConnectionOption(setting, option) }}
                </option>
              </select>
              <input
                v-else-if="isBooleanSetting(setting)"
                type="checkbox"
                :checked="uploadSettingValues[setting.key] === 'true'"
                @change="handleUploadSettingBooleanChange(setting.key, $event)"
              />
              <input
                v-else
                v-model="uploadSettingValues[setting.key]"
                :type="inputType(setting)"
                :required="setting.required"
                @input="markUploadDirty"
              />
              <small>{{ displayConnectionSettingDescription(setting) }}</small>
            </label>
          </div>
          <div v-else class="empty">当前协议没有额外字段。</div>
        </div>

        <div class="panel-section">
          <div class="panel-head compact">
            <h2>目标概览</h2>
            <span class="badge">快照</span>
          </div>
          <div class="info-list">
            <div class="info-row"><span>协议</span><strong>{{ displayUploadProtocol(selectedUploadProtocol.code) }}</strong></div>
            <div class="info-row"><span>目标键</span><strong>{{ selectedUploadTarget?.targetKey || '--' }}</strong></div>
            <div class="info-row"><span>显示名称</span><strong>{{ selectedUploadTarget?.displayName || '--' }}</strong></div>
            <div class="info-row"><span>端点</span><strong>{{ selectedUploadTarget?.endpoint || '--' }}</strong></div>
            <div class="info-row"><span>状态</span><strong>{{ selectedUploadTarget?.enabled === false ? '已禁用' : '启用' }}</strong></div>
            <div class="info-row"><span>批大小</span><strong>{{ selectedUploadTarget?.batchSize ?? '--' }}</strong></div>
            <div class="info-row"><span>缓冲</span><strong>{{ selectedUploadTarget?.bufferingEnabled ? '已启用' : '未启用' }}</strong></div>
          </div>
        </div>

        <div class="actions align-end">
          <button type="button" @click="syncFormsToJson('上传配置已同步到 JSON')">
            <Save :size="16" />
            <span>同步到 JSON</span>
          </button>
          <button type="button" class="primary" :disabled="isSaving || !!jsonParseError" @click="applyConfiguration">
            <Network :size="16" />
            <span>保存并应用</span>
          </button>
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
