<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { apiGet } from '../../lib/api'
import { getSelectedOrgId, setSelectedOrgId } from '../../lib/auth'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Organization } from '../../types'

const router = useRouter()
const { t } = useI18n()
const orgs = ref<Organization[]>([])
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const selectedOrgId = ref(getSelectedOrgId())

const loadOrgs = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null

  try {
    orgs.value = await apiGet<Organization[]>('/api/organizations')
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.organizations.load'
    }
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
      <h1 class="text-2xl font-semibold">{{ t('admin.organizations.title') }}</h1>
      <p class="mt-2 text-sm text-slate-600">
        {{ t('admin.organizations.subtitle') }}
      </p>
    </section>

    <LoadingState v-if="loading" message-key="admin.organizations.loading" />
    <ErrorState
      v-else-if="errorKey || errorMessage"
      :message="errorMessage ?? undefined"
      :message-key="errorKey ?? undefined"
      @retry="loadOrgs"
    />

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
            {{ selectedOrgId === org.id ? t('admin.organizations.selected') : t('admin.organizations.select') }}
          </button>
        </div>
      </div>

      <div v-if="orgs.length === 0" class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500">
        {{ t('admin.organizations.empty') }}
      </div>
    </div>
  </div>
</template>
