<script setup lang="ts">
import ResourcePanel from '@/components/ResourcePanel.vue'
</script>

<template>
  <ResourcePanel
    title="转换规则"
    endpoint="/api/transform-rules"
    description="支持缩放、偏移、类型转换、位提取和枚举映射。"
    empty-message="暂无转换规则。"
  />
</template>
