<script setup lang="ts">
import ResourcePanel from '@/components/ResourcePanel.vue'
</script>

<template>
  <ResourcePanel
    title="点位配置"
    endpoint="/api/points"
    description="点位定义地址、数据类型、读写方式和点位级参数。"
    empty-message="暂无点位，请先为设备添加采集点。"
  />
</template>
