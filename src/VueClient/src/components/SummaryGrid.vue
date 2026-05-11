<script setup lang="ts">
import type { DashboardCard } from '../uiTypes'

defineProps<{
  cards: DashboardCard[]
}>()
</script>

<template>
  <section class="summary-grid">
    <article v-for="card in cards" :key="card.label" class="summary-card">
      <span>{{ card.label }}</span>
      <strong>{{ card.value }}</strong>
      <small>{{ card.meta }}</small>
    </article>
  </section>
</template>
