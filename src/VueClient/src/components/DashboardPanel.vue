<script setup lang="ts">
import type { RuntimeSummary } from '../types'

defineProps<{
  runtimeSummary: RuntimeSummary | null
  formatBytes: (value?: number) => string
}>()
</script>

<template>
  <section class="panel-group">
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
</template>
