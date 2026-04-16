<script setup lang="ts">
import ResourcePanel from '@/components/ResourcePanel.vue'
</script>

<template>
  <div class="stack">
    <ResourcePanel
      title="上传通道"
      endpoint="/api/upload-channels"
      description="当前实现支持 HTTP 与 IoTSharp MQTT 上传通道。"
      empty-message="暂无上传通道。"
    />
    <ResourcePanel
      title="上传路由"
      endpoint="/api/upload-routes"
      description="上传路由将点位值分发到上传通道和目标。"
      empty-message="暂无上传路由。"
    />
  </div>
</template>
