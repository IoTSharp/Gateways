<script setup lang="ts">
import { AlertTriangle, Network, Plus, Save, Trash2 } from 'lucide-vue-next'
import type { RoutePointOption, RouteRow, RouteUploadTargetOption } from '../uiTypes'

defineProps<{
  routeRows: RouteRow[]
  selectedRoute: RouteRow | null
  selectedRouteCount: number
  selectedRouteIndex: number
  routePointOptions: RoutePointOption[]
  routeUploadTargetOptions: RouteUploadTargetOption[]
  routeForm: {
    pointRef: string
    uploadTargetKey: string
    payloadTemplate: string
    enabled: boolean
  }
  configurationText: string
  jsonParseError: string
  isSaving: boolean
  selectRoute: (index: number) => void
  addRoute: () => void
  removeRoute: () => void
  handleRoutePointChange: () => void
  markRouteDirty: () => void
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
              <h2>数据路由</h2>
              <small>{{ routePointOptions.length }} 个点位 · {{ routeUploadTargetOptions.length }} 个上传目标</small>
            </div>
            <span class="badge">{{ selectedRouteCount ? `${selectedRouteCount} 条规则` : '默认转发' }}</span>
          </div>
        </div>

        <div class="panel-section">
          <div class="panel-head compact">
            <h2>路由规则</h2>
            <div class="actions dense">
              <button type="button" :disabled="!routePointOptions.length || !routeUploadTargetOptions.length" @click="addRoute">
                <Plus :size="15" />
                <span>新增路由</span>
              </button>
              <button type="button" :disabled="!selectedRoute" @click="removeRoute">
                <Trash2 :size="15" />
                <span>删除路由</span>
              </button>
            </div>
          </div>
          <div v-if="selectedRouteCount" class="upload-target-list">
            <button
              v-for="(route, index) in routeRows"
              :key="`${route.configIndex}-${route.pointLabel}-${route.uploadTargetKey}`"
              class="upload-target-item"
              :class="{ active: index === selectedRouteIndex }"
              type="button"
              @click="selectRoute(index)"
            >
              <div class="upload-target-main">
                <strong>{{ route.pointLabel || '未命名点位' }}</strong>
                <small>{{ route.uploadTargetLabel || route.uploadTargetKey || '--' }}</small>
              </div>
              <div class="upload-target-meta">
                <span>{{ route.enabled ? '启用' : '已禁用' }}</span>
                <span>{{ route.targetName || '--' }}</span>
                <span v-if="route.payloadTemplate">模板</span>
              </div>
            </button>
          </div>
          <div v-else class="empty">暂无显式路由规则。</div>
        </div>

        <div class="panel-section">
          <div class="panel-head compact">
            <h2>规则配置</h2>
            <span class="badge">{{ selectedRoute ? (selectedRoute.enabled ? '启用' : '已禁用') : '新规则草稿' }}</span>
          </div>
          <div class="form-grid">
            <label>
              采集点
              <select v-model="routeForm.pointRef" :disabled="!routePointOptions.length" @change="handleRoutePointChange">
                <option v-if="!routePointOptions.length" value="">暂无点位</option>
                <option v-for="option in routePointOptions" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </select>
            </label>
            <label>
              上传目标
              <select v-model="routeForm.uploadTargetKey" :disabled="!routeUploadTargetOptions.length" @change="markRouteDirty">
                <option v-if="!routeUploadTargetOptions.length" value="">暂无上传目标</option>
                <option v-for="option in routeUploadTargetOptions" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </select>
            </label>
            <label>
              负载模板
              <input v-model="routeForm.payloadTemplate" type="text" @input="markRouteDirty" />
            </label>
            <label class="checkbox-label">
              <span>启用</span>
              <input v-model="routeForm.enabled" type="checkbox" @change="markRouteDirty" />
              <small>禁用后该规则不参与转发。</small>
            </label>
          </div>
        </div>

        <div class="panel-section">
          <div class="panel-head compact">
            <h2>规则概览</h2>
            <span class="badge">当前</span>
          </div>
          <div class="info-list">
            <div class="info-row"><span>采集点</span><strong>{{ selectedRoute?.pointLabel || '--' }}</strong></div>
            <div class="info-row"><span>上传目标</span><strong>{{ selectedRoute?.uploadTargetLabel || '--' }}</strong></div>
            <div class="info-row"><span>上报名</span><strong>{{ selectedRoute?.targetName || '--' }}</strong></div>
            <div class="info-row"><span>模板</span><strong>{{ selectedRoute?.payloadTemplate ? '已设置' : '--' }}</strong></div>
          </div>
        </div>

        <div class="actions align-end">
          <button type="button" @click="syncFormsToJson('路由配置已同步到 JSON')">
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
