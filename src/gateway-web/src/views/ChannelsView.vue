<script setup lang="ts">
import ResourcePanel from '@/components/ResourcePanel.vue'
</script>

<template>
  <ResourcePanel
    title="通道管理"
    endpoint="/api/channels"
    description="通道绑定统一驱动和连接配置，是设备采集的入口层。"
    empty-message="暂无通道，请先创建与驱动关联的采集通道。"
  />
</template>
