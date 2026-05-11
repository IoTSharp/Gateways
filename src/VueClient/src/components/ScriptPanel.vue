<script setup lang="ts">
defineProps<{
  scriptText: string
}>()
</script>

<template>
  <section class="panel-group">
    <article class="panel">
      <div class="panel-head">
        <h2>BASIC 采集脚本</h2>
        <span class="badge">只读</span>
      </div>
      <pre class="code-block">{{ scriptText }}</pre>
    </article>
  </section>
</template>
