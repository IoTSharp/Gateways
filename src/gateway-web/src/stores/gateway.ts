import { defineStore } from 'pinia'
import { getJson } from '@/api'

export interface ConnectionSettingDefinition {
  key: string
  label: string
  valueType: string
  required: boolean
  description: string
  options?: string[]
}

export interface DriverDefinition {
  code: string
  driverType: string
  displayName: string
  description: string
  supportsRead: boolean
  supportsWrite: boolean
  supportsBatchRead: boolean
  supportsBatchWrite: boolean
  riskLevel: string
  connectionSettings: ConnectionSettingDefinition[]
}

export const useGatewayStore = defineStore('gateway', {
  state: () => ({
    drivers: [] as DriverDefinition[],
    loading: false,
    error: '' as string | null,
  }),
  actions: {
    async loadDrivers() {
      this.loading = true
      this.error = null
      try {
        this.drivers = await getJson<DriverDefinition[]>('/api/drivers')
      } catch (error) {
        this.error = error instanceof Error ? error.message : 'Failed to load drivers.'
      } finally {
        this.loading = false
      }
    },
  },
})
