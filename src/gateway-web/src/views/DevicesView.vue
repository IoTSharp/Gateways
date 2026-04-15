<script setup lang="ts">
import ResourcePanel from '@/components/ResourcePanel.vue'
</script>

<template>
  <ResourcePanel
    title="设备管理"
    endpoint="/api/devices"
    description="设备隶属于通道，并提供轮询间隔与设备级配置。"
    empty-message="暂无设备，请先创建采集设备。"
  />
</template>
