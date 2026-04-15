<script setup lang="ts">
const navigation = [
  { to: '/', label: '概览' },
  { to: '/drivers', label: '驱动' },
  { to: '/channels', label: '通道' },
  { to: '/devices', label: '设备' },
  { to: '/points', label: '点位' },
  { to: '/tasks', label: '任务' },
  { to: '/transforms', label: '转换' },
  { to: '/uploads', label: '上传' },
  { to: '/runtime', label: '调试' },
]
</script>

<template>
  <div class="layout">
    <aside class="sidebar">
      <h1>Gateways</h1>
      <nav>
        <RouterLink v-for="item in navigation" :key="item.to" :to="item.to" class="nav-link">
          {{ item.label }}
        </RouterLink>
      </nav>
    </aside>

    <main class="content">
      <RouterView />
    </main>
  </div>
</template>
