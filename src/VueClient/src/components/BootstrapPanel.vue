<script setup lang="ts">
defineProps<{
  bootstrapJson: string
}>()
</script>

<template>
  <section class="panel-group">
    <article class="panel">
      <div class="panel-head">
        <h2>本地配置</h2>
        <span class="badge">原始</span>
      </div>
      <pre class="code-block">{{ bootstrapJson }}</pre>
    </article>
  </section>
</template>
