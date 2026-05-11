<script setup lang="ts">
import { RefreshCw, RotateCcw, Save } from 'lucide-vue-next'
import type { EdgeCollectionConfiguration, RuntimeSummary } from '../types'

defineProps<{
  pageTitle: string
  runtimeSummary: RuntimeSummary | null
  baseConfiguration: EdgeCollectionConfiguration
  isLoading: boolean
  isSaving: boolean
  jsonParseError: string
  displaySyncStatus: (value?: string) => string
  displayUpdatedBy: (value?: string) => string
  displayUploadSummary: (configuration: EdgeCollectionConfiguration) => string
}>()

const emit = defineEmits<{
  refresh: []
  apply: []
  reset: []
}>()
</script>

<template>
  <header class="topbar">
    <div>
      <h1>{{ pageTitle }}</h1>
      <p>本地管理采集配置、上传目标、数据路由和运行态。</p>
    </div>
    <div class="topbar-tools">
      <div class="metrics">
        <div class="metric">{{ displaySyncStatus(runtimeSummary?.collectionSync?.status) }}</div>
        <div class="metric">{{ displayUpdatedBy(baseConfiguration.updatedBy) }}</div>
        <div class="metric">{{ displayUploadSummary(baseConfiguration) }}</div>
      </div>
      <div class="actions">
        <button type="button" :disabled="isLoading" @click="emit('refresh')">
          <RefreshCw :size="16" />
          <span>刷新</span>
        </button>
        <button type="button" class="primary" :disabled="isSaving || !!jsonParseError" @click="emit('apply')">
          <Save :size="16" />
          <span>应用配置</span>
        </button>
        <button type="button" :disabled="isSaving" @click="emit('reset')">
          <RotateCcw :size="16" />
          <span>重置</span>
        </button>
      </div>
    </div>
  </header>
</template>
