<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { getJson } from '@/api'

const props = defineProps<{
  title: string
  endpoint: string
  description: string
  emptyMessage: string
}>()

const items = ref<unknown[]>([])
const loading = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    items.value = await getJson<unknown[]>(props.endpoint)
  } catch (loadError) {
    error.value = loadError instanceof Error ? loadError.message : 'Load failed.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <section class="panel">
    <div class="panel__header">
      <div>
        <h2>{{ title }}</h2>
        <p>{{ description }}</p>
      </div>
      <button type="button" class="secondary" @click="load">刷新</button>
    </div>

    <p v-if="loading">加载中…</p>
    <p v-else-if="error" class="error">{{ error }}</p>
    <p v-else-if="items.length === 0" class="empty">{{ emptyMessage }}</p>
    <pre v-else>{{ JSON.stringify(items, null, 2) }}</pre>
  </section>
</template>
