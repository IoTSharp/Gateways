<script setup lang="ts">
import ResourcePanel from '@/components/ResourcePanel.vue'
</script>

<template>
  <ResourcePanel
    title="采集任务"
    endpoint="/api/polling-tasks"
    description="采集任务定义周期采集、批量读取与变更上传策略。"
    empty-message="暂无采集任务，请先为设备配置轮询任务。"
  />
</template>
