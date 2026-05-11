<script setup lang="ts">
import { reactive, ref, toRefs, watchEffect } from 'vue'
import {
  Activity,
  ChevronRight,
  CloudUpload,
  Cpu,
  FileCode2,
  ListTree,
  PanelLeft,
  Settings2,
  TerminalSquare,
  Wifi,
} from 'lucide-vue-next'
import type { PanelName, ProtocolGroup, StatusTone, UploadProtocolGroup } from '../uiTypes'
import { normalizeProtocolKey } from '../support/common'

const props = defineProps<{
  activePanel: PanelName
  edgeApiBaseUrl: string
  protocolGroups: ProtocolGroup[]
  selectedProtocolCode: string
  uploadProtocolGroups: UploadProtocolGroup[]
  selectedUploadProtocolCode: string
  statusText: string
  statusTone: StatusTone
  displayProtocolCategory: (value: string) => string
  displayLifecycleLabel: (value: string) => string
  displayStatusText: (value: string) => string
  lifecycleClass: (value: string) => string
  sameProtocol: (left: unknown, right: unknown) => boolean
  sameUploadProtocol: (left: unknown, right: unknown) => boolean
}>()

const {
  activePanel,
  edgeApiBaseUrl,
  protocolGroups,
  selectedProtocolCode,
  uploadProtocolGroups,
  selectedUploadProtocolCode,
  statusText,
  statusTone,
  displayProtocolCategory,
  displayLifecycleLabel,
  displayStatusText,
  lifecycleClass,
  sameProtocol,
  sameUploadProtocol,
} = toRefs(props)

const emit = defineEmits<{
  'switch-panel': [panel: PanelName]
  'select-protocol': [code: string]
  'select-upload-protocol': [code: string]
}>()

const topologyExpanded = ref(true)
const uploadExpanded = ref(true)
const topologyCategoryExpanded = reactive<Record<string, boolean>>({})
const uploadCategoryExpanded = reactive<Record<string, boolean>>({})

watchEffect(() => {
  for (const group of protocolGroups.value ?? []) {
    const key = sectionKey('topology', group.category)
    if (!(key in topologyCategoryExpanded)) {
      topologyCategoryExpanded[key] = true
    }
  }

  for (const group of uploadProtocolGroups.value ?? []) {
    const key = sectionKey('upload', group.category)
    if (!(key in uploadCategoryExpanded)) {
      uploadCategoryExpanded[key] = true
    }
  }
})

function toggleTopologySection() {
  topologyExpanded.value = !topologyExpanded.value
  emit('switch-panel', 'topology')
}

function toggleUploadSection() {
  uploadExpanded.value = !uploadExpanded.value
  emit('switch-panel', 'upload')
}

function sectionKey(prefix: string, category: string) {
  return `${prefix}:${normalizeProtocolKey(category || 'other')}`
}

function isTopologyCategoryExpanded(category: string) {
  return topologyCategoryExpanded[sectionKey('topology', category)] !== false
}

function isUploadCategoryExpanded(category: string) {
  return uploadCategoryExpanded[sectionKey('upload', category)] !== false
}

function toggleTopologyCategory(category: string) {
  const key = sectionKey('topology', category)
  topologyCategoryExpanded[key] = !isTopologyCategoryExpanded(category)
}

function toggleUploadCategory(category: string) {
  const key = sectionKey('upload', category)
  uploadCategoryExpanded[key] = !isUploadCategoryExpanded(category)
}
</script>

<template>
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
      <button class="nav-item" :class="{ active: activePanel === 'dashboard' }" type="button" @click="emit('switch-panel', 'dashboard')">
        <Activity :size="17" />
        <span>仪表盘</span>
      </button>

      <div class="nav-group" :class="{ active: activePanel === 'topology', collapsed: !topologyExpanded }">
        <button class="nav-item nav-group-toggle" :class="{ active: activePanel === 'topology' }" type="button" :aria-expanded="topologyExpanded" @click="toggleTopologySection">
          <ListTree :size="17" />
          <span>采集拓扑</span>
          <ChevronRight class="nav-chevron" :class="{ open: topologyExpanded }" :size="16" />
        </button>
        <div v-show="topologyExpanded" class="protocol-nav" aria-label="采集协议">
          <div v-if="!protocolGroups.length" class="nav-empty">协议目录加载中</div>
          <div v-for="group in protocolGroups" :key="group.category" class="protocol-nav-group">
            <button
              class="protocol-nav-title protocol-nav-toggle"
              type="button"
              :aria-expanded="isTopologyCategoryExpanded(group.category)"
              @click="toggleTopologyCategory(group.category)"
            >
              <span>{{ displayProtocolCategory(group.category) }} 采集</span>
              <ChevronRight class="nav-chevron" :class="{ open: isTopologyCategoryExpanded(group.category) }" :size="14" />
            </button>
            <div v-show="isTopologyCategoryExpanded(group.category)" class="protocol-nav-items">
              <button
                v-for="protocol in group.protocols"
                :key="protocol.code"
                class="protocol-nav-item"
                :class="{ active: sameProtocol(protocol.code, selectedProtocolCode) }"
                type="button"
                :title="protocol.description"
                @click="emit('select-protocol', protocol.code)"
              >
                <span>{{ protocol.displayName }}</span>
                <small :class="lifecycleClass(protocol.lifecycle)">{{ displayLifecycleLabel(protocol.lifecycle) }}</small>
              </button>
            </div>
          </div>
        </div>
      </div>

      <div class="nav-group" :class="{ active: activePanel === 'upload', collapsed: !uploadExpanded }">
        <button class="nav-item nav-group-toggle" :class="{ active: activePanel === 'upload' }" type="button" :aria-expanded="uploadExpanded" @click="toggleUploadSection">
          <CloudUpload :size="17" />
          <span>上传目标</span>
          <ChevronRight class="nav-chevron" :class="{ open: uploadExpanded }" :size="16" />
        </button>
        <div v-show="uploadExpanded" class="protocol-nav" aria-label="上传协议">
          <div v-if="!uploadProtocolGroups.length" class="nav-empty">协议目录加载中</div>
          <div v-for="group in uploadProtocolGroups" :key="group.category" class="protocol-nav-group">
            <button
              class="protocol-nav-title protocol-nav-toggle"
              type="button"
              :aria-expanded="isUploadCategoryExpanded(group.category)"
              @click="toggleUploadCategory(group.category)"
            >
              <span>{{ group.category }} 上传</span>
              <ChevronRight class="nav-chevron" :class="{ open: isUploadCategoryExpanded(group.category) }" :size="14" />
            </button>
            <div v-show="isUploadCategoryExpanded(group.category)" class="protocol-nav-items">
              <button
                v-for="protocol in group.protocols"
                :key="protocol.code"
                class="protocol-nav-item"
                :class="{ active: sameUploadProtocol(protocol.code, selectedUploadProtocolCode) }"
                type="button"
                :title="protocol.description"
                @click="emit('select-upload-protocol', protocol.code)"
              >
                <span>{{ protocol.displayName }}</span>
                <small :class="lifecycleClass(protocol.lifecycle)">{{ displayLifecycleLabel(protocol.lifecycle) }}</small>
              </button>
            </div>
          </div>
        </div>
      </div>

      <button class="nav-item" :class="{ active: activePanel === 'script' }" type="button" @click="emit('switch-panel', 'script')">
        <FileCode2 :size="17" />
        <span>BASIC 脚本</span>
      </button>
      <button class="nav-item" :class="{ active: activePanel === 'logs' }" type="button" @click="emit('switch-panel', 'logs')">
        <TerminalSquare :size="17" />
        <span>运行日志</span>
      </button>
      <button class="nav-item" :class="{ active: activePanel === 'bootstrap' }" type="button" @click="emit('switch-panel', 'bootstrap')">
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
</template>
