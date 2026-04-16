<script setup lang="ts">
import { ref } from 'vue'
import { postJson } from '@/api'

const readPayload = ref({
  driverCode: 'modbus',
  connectionSettings: {
    transport: 'tcp',
    host: '127.0.0.1',
    port: '502',
  },
  pointSettings: {
    stationNumber: '1',
    functionCode: '3',
  },
  address: '0',
  dataType: 'Int16',
  length: 1,
})

const writePayload = ref({
  driverCode: 'modbus',
  connectionSettings: {
    transport: 'tcp',
    host: '127.0.0.1',
    port: '502',
  },
  pointSettings: {
    stationNumber: '1',
    functionCode: '16',
  },
  address: '0',
  dataType: 'Int16',
  value: 1,
  length: 1,
})

const readResult = ref('')
const writeResult = ref('')
const error = ref('')

async function executeRead() {
  error.value = ''
  try {
    readResult.value = JSON.stringify(await postJson('/api/runtime/read', readPayload.value), null, 2)
  } catch (requestError) {
    error.value = requestError instanceof Error ? requestError.message : 'Read failed.'
  }
}

async function executeWrite() {
  error.value = ''
  try {
    writeResult.value = JSON.stringify(await postJson('/api/runtime/write', writePayload.value), null, 2)
  } catch (requestError) {
    error.value = requestError instanceof Error ? requestError.message : 'Write failed.'
  }
}
</script>

<template>
  <section class="panel stack">
    <div class="panel__header">
      <div>
        <h1>调试读写</h1>
        <p>直接调用统一读写接口，验证驱动连接配置和地址读写行为。</p>
      </div>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <div class="card-grid runtime-grid">
      <article class="panel card stack">
        <h2>读取</h2>
        <label>
          驱动代码
          <input v-model="readPayload.driverCode" />
        </label>
        <label>
          地址
          <input v-model="readPayload.address" />
        </label>
        <label>
          数据类型
          <input v-model="readPayload.dataType" />
        </label>
        <button type="button" @click="executeRead">执行读取</button>
        <pre v-if="readResult">{{ readResult }}</pre>
      </article>

      <article class="panel card stack">
        <h2>写入</h2>
        <label>
          驱动代码
          <input v-model="writePayload.driverCode" />
        </label>
        <label>
          地址
          <input v-model="writePayload.address" />
        </label>
        <label>
          数据类型
          <input v-model="writePayload.dataType" />
        </label>
        <label>
          值
          <input v-model="writePayload.value" />
        </label>
        <button type="button" @click="executeWrite">执行写入</button>
        <pre v-if="writeResult">{{ writeResult }}</pre>
      </article>
    </div>
  </section>
</template>
