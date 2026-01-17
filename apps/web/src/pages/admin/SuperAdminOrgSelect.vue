<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiGet } from '../../lib/api'
import { getSelectedOrgId, setSelectedOrgId } from '../../lib/auth'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Organization } from '../../types'

const router = useRouter()
const orgs = ref<Organization[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedOrgId = ref(getSelectedOrgId())

const loadOrgs = async () => {
  loading.value = true
  error.value = null

  try {
    orgs.value = await apiGet<Organization[]>('/api/organizations')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load organizations.'
  } finally {
    loading.value = false
  }
}

const selectOrg = async (org: Organization) => {
  setSelectedOrgId(org.id)
  selectedOrgId.value = org.id
  await router.push('/admin/tours')
}

onMounted(loadOrgs)
</script>

<template>
  <div class="space-y-6">
    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h1 class="text-2xl font-semibold">Select organization</h1>
      <p class="mt-2 text-sm text-slate-600">
        Choose which agency you want to manage.
      </p>
    </section>

    <LoadingState v-if="loading" message="Loading organizations..." />
    <ErrorState v-else-if="error" :message="error" @retry="loadOrgs" />

    <div v-else class="grid gap-4">
      <div
        v-for="org in orgs"
        :key="org.id"
        class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"
      >
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div class="text-lg font-semibold">{{ org.name }}</div>
            <div class="text-xs text-slate-500">{{ org.slug }}</div>
          </div>
          <button
            class="rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:border-slate-300"
            type="button"
            @click="selectOrg(org)"
          >
            {{ selectedOrgId === org.id ? 'Selected' : 'Select' }}
          </button>
        </div>
      </div>

      <div v-if="orgs.length === 0" class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500">
        No organizations found.
      </div>
    </div>
  </div>
</template>
