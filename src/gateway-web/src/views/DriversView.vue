<script setup lang="ts">
import { onMounted } from 'vue'
import { storeToRefs } from 'pinia'
import { useGatewayStore } from '@/stores/gateway'

const store = useGatewayStore()
const { drivers, loading, error } = storeToRefs(store)

onMounted(() => {
  if (drivers.value.length === 0) {
    void store.loadDrivers()
  }
})
</script>

<template>
  <section class="panel">
    <div class="panel__header">
      <div>
        <h1>驱动目录</h1>
        <p>展示统一驱动契约及其连接配置模板。</p>
      </div>
      <button type="button" class="secondary" @click="store.loadDrivers()">刷新</button>
    </div>

    <p v-if="loading">加载中…</p>
    <p v-else-if="error" class="error">{{ error }}</p>
    <div v-else class="card-grid">
      <article v-for="driver in drivers" :key="driver.code" class="panel card">
        <div class="row-between">
          <h2>{{ driver.displayName }}</h2>
          <span class="badge" :class="driver.riskLevel === 'high' ? 'badge--warn' : ''">{{ driver.riskLevel }}</span>
        </div>
        <p>{{ driver.description }}</p>
        <ul class="meta-list">
          <li><strong>代码：</strong>{{ driver.code }}</li>
          <li><strong>类型：</strong>{{ driver.driverType }}</li>
          <li><strong>读：</strong>{{ driver.supportsRead ? '支持' : '不支持' }}</li>
          <li><strong>写：</strong>{{ driver.supportsWrite ? '支持' : '不支持' }}</li>
        </ul>
        <h3>连接参数</h3>
        <ul class="meta-list">
          <li v-for="setting in driver.connectionSettings" :key="setting.key">
            <strong>{{ setting.label }}</strong>（{{ setting.key }}） - {{ setting.description }}
          </li>
        </ul>
      </article>
    </div>
  </section>
</template>
