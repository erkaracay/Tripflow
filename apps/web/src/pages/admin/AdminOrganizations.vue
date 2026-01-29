<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiDelete, apiGet, apiPost, apiPut } from '../../lib/api'
import { clearSelectedOrgId, getSelectedOrgId, setSelectedOrgId } from '../../lib/auth'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Organization } from '../../types'

const { t } = useI18n()
const router = useRouter()
const { pushToast } = useToast()

const orgs = ref<Organization[]>([])
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const showArchived = ref(false)

const selectedOrgId = ref<string | null>(getSelectedOrgId() || null)

const createForm = reactive({
  name: '',
  slug: '',
  requireLast4ForQr: false,
  requireLast4ForPortal: false,
})
const creating = ref(false)
const createErrorKey = ref<string | null>(null)
const createErrorMessage = ref<string | null>(null)

const editingOrgId = ref<string | null>(null)
const editForm = reactive({
  name: '',
  slug: '',
  isActive: true,
  requireLast4ForQr: false,
  requireLast4ForPortal: false,
})
const savingOrgId = ref<string | null>(null)
const archivingOrgId = ref<string | null>(null)
const restoringOrgId = ref<string | null>(null)
const purgingOrgId = ref<string | null>(null)
const purgeConfirm = reactive<Record<string, string>>({})
const purgeErrorKey = ref<string | null>(null)

const loadOrgs = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null

  try {
    const query = showArchived.value ? '?includeArchived=true' : ''
    orgs.value = await apiGet<Organization[]>(`/api/organizations${query}`)
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.organizations.load'
    }
  } finally {
    loading.value = false
  }
}

const isPurgeConfirmValid = (org: Organization) => {
  const value = purgeConfirm[org.id]?.trim()
  if (!value) {
    return false
  }

  return value.toLowerCase() === 'sil' || value === org.slug
}

const archiveOrg = async (org: Organization) => {
  if (archivingOrgId.value) {
    return
  }
  archivingOrgId.value = org.id
  try {
    const updated = await apiPost<Organization>(`/api/organizations/${org.id}/archive`, {})
    orgs.value = orgs.value.map((item) => (item.id === org.id ? updated : item))
    if (selectedOrgId.value === org.id) {
      clearSelectedOrgId()
      selectedOrgId.value = null
    }
    pushToast({ key: 'toast.orgArchived', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.orgArchiveFailed', tone: 'error' })
  } finally {
    archivingOrgId.value = null
  }
}

const restoreOrg = async (org: Organization) => {
  if (restoringOrgId.value) {
    return
  }
  restoringOrgId.value = org.id
  try {
    const updated = await apiPost<Organization>(`/api/organizations/${org.id}/restore`, {})
    orgs.value = orgs.value.map((item) => (item.id === org.id ? updated : item))
    pushToast({ key: 'toast.orgRestored', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.orgRestoreFailed', tone: 'error' })
  } finally {
    restoringOrgId.value = null
  }
}

const purgeOrg = async (org: Organization) => {
  purgeErrorKey.value = null
  if (!org.isDeleted) {
    purgeErrorKey.value = 'admin.organizations.manage.purgeRequiresArchive'
    return
  }

  if (!isPurgeConfirmValid(org)) {
    purgeErrorKey.value = 'admin.organizations.manage.purgeConfirmMismatch'
    return
  }

  purgingOrgId.value = org.id
  try {
    await apiDelete(`/api/organizations/${org.id}/purge`)
    orgs.value = orgs.value.filter((item) => item.id !== org.id)
    if (selectedOrgId.value === org.id) {
      clearSelectedOrgId()
      selectedOrgId.value = null
    }
    pushToast({ key: 'toast.orgPurged', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.orgPurgeFailed', tone: 'error' })
  } finally {
    purgingOrgId.value = null
  }
}
const createOrg = async () => {
  createErrorKey.value = null
  createErrorMessage.value = null

  if (!createForm.name.trim()) {
    createErrorKey.value = 'validation.orgNameRequired'
    return
  }

  creating.value = true
  try {
    const payload = {
      name: createForm.name.trim(),
      slug: createForm.slug.trim() || undefined,
      requireLast4ForQr: createForm.requireLast4ForQr,
      requireLast4ForPortal: createForm.requireLast4ForPortal,
    }
    const created = await apiPost<Organization>('/api/organizations', payload)
    orgs.value = [created, ...orgs.value]
    createForm.name = ''
    createForm.slug = ''
    createForm.requireLast4ForQr = false
    createForm.requireLast4ForPortal = false
    pushToast({ key: 'toast.orgCreated', tone: 'success' })
  } catch (err) {
    createErrorMessage.value = err instanceof Error ? err.message : null
    if (!createErrorMessage.value) {
      createErrorKey.value = 'errors.organizations.create'
    }
    pushToast({ key: 'toast.orgCreateFailed', tone: 'error' })
  } finally {
    creating.value = false
  }
}

const startEdit = (org: Organization) => {
  editingOrgId.value = org.id
  editForm.name = org.name
  editForm.slug = org.slug
  editForm.isActive = org.isActive
  editForm.requireLast4ForQr = org.requireLast4ForQr
  editForm.requireLast4ForPortal = org.requireLast4ForPortal
}

const cancelEdit = () => {
  editingOrgId.value = null
}

const saveEdit = async (org: Organization) => {
  if (!editForm.name.trim()) {
    pushToast({ key: 'validation.orgNameRequired', tone: 'error' })
    return
  }

  if (!editForm.slug.trim()) {
    pushToast({ key: 'validation.orgSlugRequired', tone: 'error' })
    return
  }

  savingOrgId.value = org.id
  try {
    const payload = {
      name: editForm.name.trim(),
      slug: editForm.slug.trim(),
      isActive: editForm.isActive,
      requireLast4ForQr: editForm.requireLast4ForQr,
      requireLast4ForPortal: editForm.requireLast4ForPortal,
    }
    const updated = await apiPut<Organization>(`/api/organizations/${org.id}`, payload)
    orgs.value = orgs.value.map((item) => (item.id === org.id ? updated : item))
    if (!updated.isActive && selectedOrgId.value === org.id) {
      clearSelectedOrgId()
      selectedOrgId.value = null
    }
    editingOrgId.value = null
    pushToast({ key: 'toast.orgUpdated', tone: 'success' })
  } catch (err) {
    pushToast({ key: 'toast.orgUpdateFailed', tone: 'error' })
  } finally {
    savingOrgId.value = null
  }
}

const selectOrg = async (org: Organization) => {
  if (!org.isActive || org.isDeleted) {
    return
  }

  setSelectedOrgId(org.id)
  selectedOrgId.value = org.id
  await router.push('/admin/events')
}

onMounted(loadOrgs)
</script>

<template>
  <div class="space-y-6">
    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-2xl font-semibold">{{ t('admin.organizations.manage.title') }}</h1>
          <p class="mt-2 text-sm text-slate-600">{{ t('admin.organizations.manage.subtitle') }}</p>
        </div>
        <label class="flex items-center gap-2 text-sm text-slate-600">
          <input v-model="showArchived" type="checkbox" class="rounded border-slate-300" @change="loadOrgs" />
          {{ t('admin.organizations.manage.showArchived') }}
        </label>
      </div>
    </section>

    <section class="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 class="text-lg font-semibold">{{ t('admin.organizations.manage.createTitle') }}</h2>
      <form class="mt-4 grid gap-4 md:grid-cols-3" @submit.prevent="createOrg">
        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.organizations.manage.nameLabel') }}</span>
          <input
            v-model.trim="createForm.name"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            :placeholder="t('admin.organizations.manage.namePlaceholder')"
            type="text"
          />
        </label>
        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.organizations.manage.slugLabel') }}</span>
          <input
            v-model.trim="createForm.slug"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            :placeholder="t('admin.organizations.manage.slugPlaceholder')"
            type="text"
          />
        </label>
        <div class="grid gap-2 text-sm text-slate-600">
          <label class="flex items-center gap-2">
            <input v-model="createForm.requireLast4ForQr" type="checkbox" class="rounded border-slate-300" />
            {{ t('admin.organizations.manage.requireLast4ForQr') }}
          </label>
          <label class="flex items-center gap-2">
            <input v-model="createForm.requireLast4ForPortal" type="checkbox" class="rounded border-slate-300" />
            {{ t('admin.organizations.manage.requireLast4ForPortal') }}
          </label>
        </div>
        <div class="flex items-end">
          <button
            class="w-full rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
            :disabled="creating"
            type="submit"
          >
            {{ creating ? t('admin.organizations.manage.creating') : t('admin.organizations.manage.create') }}
          </button>
        </div>
      </form>

      <p v-if="createErrorKey || createErrorMessage" class="mt-3 text-sm text-rose-600">
        {{ createErrorKey ? t(createErrorKey) : createErrorMessage }}
      </p>
    </section>

    <LoadingState v-if="loading" message-key="admin.organizations.loading" />
    <ErrorState
      v-else-if="errorKey || errorMessage"
      :message="errorMessage ?? undefined"
      :message-key="errorKey ?? undefined"
      @retry="loadOrgs"
    />

    <div v-else class="space-y-4">
      <div v-if="orgs.length === 0" class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500">
        {{ t('admin.organizations.manage.empty') }}
      </div>

      <div
        v-for="org in orgs"
        :key="org.id"
        class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm"
      >
        <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div class="flex items-center gap-2">
              <div class="text-lg font-semibold">{{ org.name }}</div>
              <span
                v-if="!org.isActive"
                class="rounded-full border border-amber-200 bg-amber-50 px-2 py-0.5 text-xs text-amber-700"
              >
                {{ t('admin.organizations.manage.inactive') }}
              </span>
              <span
                v-if="org.isDeleted"
                class="rounded-full border border-rose-200 bg-rose-50 px-2 py-0.5 text-xs text-rose-700"
              >
                {{ t('common.archived') }}
              </span>
            </div>
            <div class="text-xs text-slate-500">{{ org.slug }}</div>
            <div v-if="selectedOrgId === org.id" class="mt-2 text-xs text-emerald-600">
              {{ t('admin.organizations.selected') }}
            </div>
          </div>

          <div class="flex flex-wrap gap-2">
            <button
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
              type="button"
              :disabled="!org.isActive || org.isDeleted"
              @click="selectOrg(org)"
            >
              {{ selectedOrgId === org.id ? t('admin.organizations.selected') : t('admin.organizations.select') }}
            </button>
            <button
              v-if="!org.isDeleted"
              class="rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800 shadow-sm hover:border-amber-300"
              type="button"
              :disabled="archivingOrgId === org.id"
              @click="archiveOrg(org)"
            >
              {{ archivingOrgId === org.id ? t('common.saving') : t('common.archive') }}
            </button>
            <button
              v-else
              class="rounded border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700 shadow-sm hover:border-emerald-300"
              type="button"
              :disabled="restoringOrgId === org.id"
              @click="restoreOrg(org)"
            >
              {{ restoringOrgId === org.id ? t('common.saving') : t('common.restore') }}
            </button>
            <button
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
              type="button"
              @click="startEdit(org)"
            >
              {{ t('common.edit') }}
            </button>
          </div>
        </div>

        <div v-if="editingOrgId === org.id" class="mt-4 grid gap-3 border-t border-slate-100 pt-4 md:grid-cols-3">
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.organizations.manage.nameLabel') }}</span>
            <input
              v-model.trim="editForm.name"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="text"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.organizations.manage.slugLabel') }}</span>
            <input
              v-model.trim="editForm.slug"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="text"
            />
          </label>
          <label class="flex items-center gap-2 text-sm text-slate-600">
            <input v-model="editForm.isActive" type="checkbox" class="rounded border-slate-300" />
            {{ t('admin.organizations.manage.activeLabel') }}
          </label>
          <label class="flex items-center gap-2 text-sm text-slate-600">
            <input v-model="editForm.requireLast4ForQr" type="checkbox" class="rounded border-slate-300" />
            {{ t('admin.organizations.manage.requireLast4ForQr') }}
          </label>
          <label class="flex items-center gap-2 text-sm text-slate-600">
            <input v-model="editForm.requireLast4ForPortal" type="checkbox" class="rounded border-slate-300" />
            {{ t('admin.organizations.manage.requireLast4ForPortal') }}
          </label>

          <div class="md:col-span-3 flex flex-wrap gap-2">
            <button
              class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
              :disabled="savingOrgId === org.id"
              type="button"
              @click="saveEdit(org)"
            >
              {{ savingOrgId === org.id ? t('admin.organizations.manage.saving') : t('admin.organizations.manage.save') }}
            </button>
            <button
              class="rounded border border-slate-200 bg-white px-4 py-2 text-sm text-slate-700 hover:border-slate-300"
              type="button"
              @click="cancelEdit"
            >
              {{ t('common.cancel') }}
            </button>
          </div>

          <div class="md:col-span-3 rounded-xl border border-rose-200 bg-rose-50 p-4">
            <div class="text-sm font-semibold text-rose-800">{{ t('common.dangerZone') }}</div>
            <p class="mt-1 text-xs text-rose-700">{{ t('admin.organizations.manage.purgeWarning') }}</p>
            <p v-if="!org.isDeleted" class="mt-2 text-xs text-rose-700">
              {{ t('admin.organizations.manage.purgeRequiresArchive') }}
            </p>
            <label class="mt-3 grid gap-1 text-xs text-rose-800">
              <span>{{ t('admin.organizations.manage.purgeConfirmLabel') }}</span>
              <input
                v-model.trim="purgeConfirm[org.id]"
                class="rounded border border-rose-200 bg-white px-3 py-2 text-xs focus:border-rose-400 focus:outline-none"
                :placeholder="t('admin.organizations.manage.purgeConfirmPlaceholder')"
                type="text"
              />
            </label>
            <p v-if="purgeErrorKey" class="mt-2 text-xs text-rose-700">{{ t(purgeErrorKey) }}</p>
            <button
              class="mt-3 rounded border border-rose-200 bg-rose-600 px-3 py-2 text-xs font-semibold text-white hover:bg-rose-700 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="purgingOrgId === org.id || !org.isDeleted"
              type="button"
              @click="purgeOrg(org)"
            >
              {{ purgingOrgId === org.id ? t('common.saving') : t('common.purge') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
