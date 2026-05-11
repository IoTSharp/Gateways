<script setup lang="ts">
defineProps<{
  logLines: string[]
}>()
</script>

<template>
  <section class="panel-group">
    <article class="panel">
      <div class="panel-head">
        <h2>运行日志</h2>
        <span class="badge">实时</span>
      </div>
      <pre class="logs">{{ logLines.length ? logLines.join('\n\n') : '暂无日志。' }}</pre>
    </article>
  </section>
</template>
