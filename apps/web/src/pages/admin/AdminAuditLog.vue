<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import AppCombobox from '../../components/ui/AppCombobox.vue'
import AppDrawerShell from '../../components/ui/AppDrawerShell.vue'
import { apiGet } from '../../lib/api'
import {
  DELETE_ACTION_FILTER,
  FAILED_LOGIN_FILTER,
  getAuditLogActionLabel,
  getAuditLogCategoryFilter,
  getAuditLogCategoryOptions,
  getAuditLogResultClass,
  getAuditLogResultLabel,
  getAuditLogTargetLabel,
  getAuditLogTargetTypeLabel,
} from '../../lib/auditLogActions'
import {
  formatAuditLogExtra,
  getAuditLogParticipantName,
  getAuditLogParticipantTcNoMasked,
} from '../../lib/auditLogFormat'
import { formatDateRange, formatUtcDateTimeLocal } from '../../lib/formatters'
import type { AppComboboxOption, AuditLogItem, AuditLogListResponse, EventListItem } from '../../types'

const { t } = useI18n()

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const items = ref<AuditLogItem[]>([])
const total = ref(0)
const events = ref<EventListItem[]>([])

const page = ref(1)
const pageSize = ref(50)
const category = ref('all')
const actionFilter = ref('')
const selectedEventId = ref('all')
const result = ref<'all' | 'success' | 'fail' | 'blocked'>('all')
const from = ref('')
const to = ref('')
const searchInput = ref('')
const searchQuery = ref('')
const selectedItem = ref<AuditLogItem | null>(null)
const activeQuickFilter = ref<'failedLogins' | 'deletes' | 'last24h' | null>(null)
const debounceHandle = ref<number | ReturnType<typeof setTimeout> | null>(null)

const resultOptions = computed<AppComboboxOption[]>(() => [
  { value: 'all', label: t('admin.auditLog.filters.results.all') },
  { value: 'success', label: t('admin.auditLog.results.success') },
  { value: 'fail', label: t('admin.auditLog.results.fail') },
  { value: 'blocked', label: t('admin.auditLog.results.blocked') },
])

const pageSizeOptions: AppComboboxOption[] = [
  { value: 25, label: '25' },
  { value: 50, label: '50' },
  { value: 100, label: '100' },
  { value: 200, label: '200' },
]

const categoryOptions = computed(() => getAuditLogCategoryOptions(t))
const eventOptions = computed<AppComboboxOption[]>(() => [
  { value: 'all', label: t('admin.auditLog.filters.allEvents') },
  ...events.value.map((event) => ({
    value: event.id,
    label: `${event.name} (${formatDateRange(event.startDate, event.endDate)})`,
  })),
])
const totalPages = computed(() => Math.max(Math.ceil(total.value / pageSize.value), 1))
const canPrev = computed(() => page.value > 1)
const canNext = computed(() => page.value < totalPages.value)
const drawerDetails = computed(() => (selectedItem.value ? formatAuditLogExtra(selectedItem.value, t) : null))

const toDateInputValue = (date: Date) => {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

const applyLast7Days = () => {
  const end = new Date()
  const start = new Date(end)
  start.setDate(start.getDate() - 6)
  from.value = toDateInputValue(start)
  to.value = toDateInputValue(end)
}

const applyLast24Hours = () => {
  const end = new Date()
  const start = new Date(end)
  start.setDate(start.getDate() - 1)
  from.value = toDateInputValue(start)
  to.value = toDateInputValue(end)
}

const fetchLogs = async () => {
  loading.value = true
  errorMessage.value = null

  try {
    const params = new URLSearchParams()
    if (actionFilter.value) params.set('action', actionFilter.value)
    if (selectedEventId.value !== 'all') params.set('eventId', selectedEventId.value)
    if (result.value !== 'all') params.set('result', result.value)
    if (from.value) params.set('from', from.value)
    if (to.value) params.set('to', to.value)
    if (searchQuery.value) params.set('query', searchQuery.value)
    params.set('page', String(page.value))
    params.set('pageSize', String(pageSize.value))
    params.set('sort', 'createdAt')
    params.set('dir', 'desc')

    const response = await apiGet<AuditLogListResponse>(`/api/audit-logs?${params.toString()}`)
    items.value = response.items
    total.value = response.total
    page.value = response.page
    pageSize.value = response.pageSize
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : t('errors.generic')
  } finally {
    loading.value = false
  }
}

const fetchEvents = async () => {
  try {
    events.value = await apiGet<EventListItem[]>('/api/events?includeArchived=true')
  } catch {
    events.value = []
  }
}

const resetFilters = () => {
  category.value = 'all'
  actionFilter.value = ''
  selectedEventId.value = 'all'
  result.value = 'all'
  searchInput.value = ''
  searchQuery.value = ''
  activeQuickFilter.value = null
  applyLast7Days()
  page.value = 1
  void fetchLogs()
}

const setCategory = (value: string) => {
  category.value = value
  actionFilter.value = getAuditLogCategoryFilter(value)
  activeQuickFilter.value = null
  page.value = 1
  void fetchLogs()
}

const onManualFilterChange = () => {
  activeQuickFilter.value = null
  page.value = 1
  void fetchLogs()
}

const handleSearchInput = () => {
  if (debounceHandle.value) clearTimeout(debounceHandle.value)
  debounceHandle.value = window.setTimeout(() => {
    searchQuery.value = searchInput.value.trim()
    activeQuickFilter.value = null
    page.value = 1
    void fetchLogs()
  }, 400)
}

const clearSearch = () => {
  searchInput.value = ''
  searchQuery.value = ''
  activeQuickFilter.value = null
  page.value = 1
  void fetchLogs()
}

const applyQuickFilter = (kind: 'failedLogins' | 'deletes' | 'last24h') => {
  if (activeQuickFilter.value === kind) {
    resetFilters()
    return
  }

  category.value = 'all'
  result.value = 'all'
  actionFilter.value = ''
  if (kind === 'failedLogins') {
    actionFilter.value = FAILED_LOGIN_FILTER
    result.value = 'fail'
  } else if (kind === 'deletes') {
    actionFilter.value = DELETE_ACTION_FILTER
  } else if (kind === 'last24h') {
    applyLast24Hours()
  }

  activeQuickFilter.value = kind
  page.value = 1
  void fetchLogs()
}

const openDrawer = (item: AuditLogItem) => {
  selectedItem.value = item
}

const closeDrawer = () => {
  selectedItem.value = null
}

const goPrev = () => {
  if (!canPrev.value) return
  page.value -= 1
  void fetchLogs()
}

const goNext = () => {
  if (!canNext.value) return
  page.value += 1
  void fetchLogs()
}

const formatActor = (item: AuditLogItem) =>
  getAuditLogParticipantName(item) || item.userFullName || item.userEmail || t('admin.auditLog.systemActor')

const formatActorSecondary = (item: AuditLogItem) =>
  item.userEmail || getAuditLogParticipantTcNoMasked(item)

const formatTarget = (item: AuditLogItem) => {
  const participantName = getAuditLogParticipantName(item)
  if (item.targetType === 'participant' && participantName) {
    const typeLabel = getAuditLogTargetTypeLabel(item.targetType, t)
    return item.targetId ? `${typeLabel} · ${participantName} · ${item.targetId}` : `${typeLabel} · ${participantName}`
  }

  return getAuditLogTargetLabel(item.targetType, item.targetId, t)
}

onMounted(() => {
  applyLast7Days()
  void fetchEvents()
  void fetchLogs()
})
</script>

<template>
  <div class="mx-auto max-w-7xl space-y-6">
    <section class="space-y-2">
      <h1 class="text-2xl font-semibold text-slate-900">{{ t('admin.auditLog.title') }}</h1>
      <p class="text-sm text-slate-500">{{ t('admin.auditLog.description') }}</p>
    </section>

    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="mb-4 flex flex-wrap items-center gap-2">
        <button
          class="rounded-full border px-3 py-1.5 text-xs font-semibold"
          type="button"
          :class="activeQuickFilter === 'failedLogins' ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'"
          @click="applyQuickFilter('failedLogins')"
        >
          {{ t('admin.auditLog.quickFilters.failedLogins') }}
        </button>
        <button
          class="rounded-full border px-3 py-1.5 text-xs font-semibold"
          type="button"
          :class="activeQuickFilter === 'deletes' ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'"
          @click="applyQuickFilter('deletes')"
        >
          {{ t('admin.auditLog.quickFilters.deletes') }}
        </button>
        <button
          class="rounded-full border px-3 py-1.5 text-xs font-semibold"
          type="button"
          :class="activeQuickFilter === 'last24h' ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'"
          @click="applyQuickFilter('last24h')"
        >
          {{ t('admin.auditLog.quickFilters.last24h') }}
        </button>
      </div>

      <div class="grid gap-3 lg:grid-cols-7">
        <div class="lg:col-span-2">
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.auditLog.filters.search') }}</label>
          <div class="mt-1 flex items-center gap-2 rounded border border-slate-200 bg-white px-2 py-1.5">
            <input
              v-model="searchInput"
              class="w-full text-sm text-slate-700 focus:outline-none"
              :placeholder="t('admin.auditLog.filters.searchPlaceholder')"
              type="text"
              @input="handleSearchInput"
            />
            <button
              v-if="searchInput"
              class="rounded px-2 py-1 text-xs font-semibold text-slate-500 hover:bg-slate-50"
              type="button"
              @click="clearSearch"
            >
              {{ t('common.clearSearch') }}
            </button>
          </div>
        </div>

        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.auditLog.filters.action') }}</label>
          <AppCombobox
            :model-value="category"
            class="mt-1"
            :options="categoryOptions"
            :placeholder="t('admin.auditLog.filters.action')"
            :aria-label="t('admin.auditLog.filters.action')"
            :searchable="false"
            compact
            @update:model-value="setCategory(String($event))"
          />
        </div>

        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.auditLog.filters.result') }}</label>
          <AppCombobox
            v-model="result"
            class="mt-1"
            :options="resultOptions"
            :placeholder="t('admin.auditLog.filters.result')"
            :aria-label="t('admin.auditLog.filters.result')"
            :searchable="false"
            compact
            @update:model-value="onManualFilterChange"
          />
        </div>

        <div class="lg:col-span-2">
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.auditLog.filters.event') }}</label>
          <AppCombobox
            v-model="selectedEventId"
            class="mt-1"
            :options="eventOptions"
            :placeholder="t('admin.auditLog.filters.event')"
            :aria-label="t('admin.auditLog.filters.event')"
            compact
            @update:model-value="onManualFilterChange"
          />
        </div>

        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.auditLog.filters.from') }}</label>
          <input
            v-model="from"
            class="mt-1 w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
            type="date"
            @change="onManualFilterChange"
          />
        </div>

        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.auditLog.filters.to') }}</label>
          <input
            v-model="to"
            class="mt-1 w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
            type="date"
            @change="onManualFilterChange"
          />
        </div>
      </div>

      <div class="mt-4 flex flex-wrap items-center justify-between gap-3 text-sm text-slate-500">
        <div>{{ t('admin.auditLog.pagination', { page, totalPages, total }) }}</div>
        <div class="flex items-center gap-2">
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.auditLog.filters.pageSize') }}</label>
          <AppCombobox
            v-model="pageSize"
            class="w-24"
            :options="pageSizeOptions"
            :placeholder="t('admin.auditLog.filters.pageSize')"
            :aria-label="t('admin.auditLog.filters.pageSize')"
            :searchable="false"
            compact
            @update:model-value="onManualFilterChange"
          />
          <button
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300"
            type="button"
            @click="resetFilters"
          >
            {{ t('admin.auditLog.filters.reset') }}
          </button>
        </div>
      </div>
    </section>

    <LoadingState v-if="loading" message-key="common.loading" />
    <ErrorState v-else-if="errorMessage" :message="errorMessage" @retry="fetchLogs" />

    <template v-else>
      <section v-if="items.length === 0" class="rounded-2xl border border-dashed border-slate-200 bg-white p-8 text-center shadow-sm">
        <h2 class="text-base font-semibold text-slate-900">{{ t('admin.auditLog.empty.title') }}</h2>
        <p class="mt-2 text-sm text-slate-500">{{ t('admin.auditLog.empty.hint') }}</p>
        <button
          class="mt-4 rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300"
          type="button"
          @click="resetFilters"
        >
          {{ t('admin.auditLog.filters.reset') }}
        </button>
      </section>

      <section v-else class="rounded-2xl border border-slate-200 bg-white shadow-sm">
        <div class="hidden overflow-x-auto lg:block">
          <table class="min-w-full border-separate border-spacing-0 text-left text-sm">
            <thead class="bg-white">
              <tr class="text-xs font-semibold uppercase tracking-wide text-slate-500">
                <th class="border-b border-slate-200 px-4 py-3">{{ t('admin.auditLog.columns.time') }}</th>
                <th class="border-b border-slate-200 px-4 py-3">{{ t('admin.auditLog.columns.user') }}</th>
                <th class="border-b border-slate-200 px-4 py-3">{{ t('admin.auditLog.columns.action') }}</th>
                <th class="border-b border-slate-200 px-4 py-3">{{ t('admin.auditLog.columns.target') }}</th>
                <th class="border-b border-slate-200 px-4 py-3">{{ t('admin.auditLog.columns.result') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="item in items"
                :key="item.id"
                class="cursor-pointer border-b border-slate-100 transition hover:bg-slate-50"
                @click="openDrawer(item)"
              >
                <td class="px-4 py-3 font-mono text-xs text-slate-600">{{ formatUtcDateTimeLocal(item.createdAt) }}</td>
                <td class="px-4 py-3">
                  <div class="font-medium text-slate-900">{{ formatActor(item) }}</div>
                  <div v-if="formatActorSecondary(item)" class="mt-0.5 text-xs text-slate-500">{{ formatActorSecondary(item) }}</div>
                </td>
                <td class="px-4 py-3">
                  <div class="font-medium text-slate-900" :title="item.action">
                    {{ getAuditLogActionLabel(item.action, t) }}
                  </div>
                  <div class="mt-0.5 text-xs text-slate-500">{{ getAuditLogTargetTypeLabel(item.targetType, t) }}</div>
                </td>
                <td class="px-4 py-3 text-slate-700">{{ formatTarget(item) }}</td>
                <td class="px-4 py-3">
                  <span class="inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold" :class="getAuditLogResultClass(item.result)">
                    {{ getAuditLogResultLabel(item.result, t) }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="space-y-3 p-4 lg:hidden">
          <button
            v-for="item in items"
            :key="item.id"
            class="w-full rounded-2xl border border-slate-200 bg-white p-4 text-left shadow-sm transition hover:border-slate-300"
            type="button"
            @click="openDrawer(item)"
          >
            <div class="flex items-start justify-between gap-3">
              <div>
                <div class="text-xs font-mono text-slate-500">{{ formatUtcDateTimeLocal(item.createdAt) }}</div>
                <div class="mt-1 text-sm font-semibold text-slate-900">{{ formatActor(item) }}</div>
                <div v-if="formatActorSecondary(item)" class="mt-0.5 text-xs text-slate-500">{{ formatActorSecondary(item) }}</div>
              </div>
              <span class="inline-flex shrink-0 items-center rounded-full px-3 py-1 text-xs font-semibold" :class="getAuditLogResultClass(item.result)">
                {{ getAuditLogResultLabel(item.result, t) }}
              </span>
            </div>
            <div class="mt-3 text-sm font-medium text-slate-900" :title="item.action">
              {{ getAuditLogActionLabel(item.action, t) }}
            </div>
            <div class="mt-1 text-sm text-slate-600">{{ formatTarget(item) }}</div>
          </button>
        </div>

        <div class="flex items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm">
          <div class="text-slate-500">{{ t('admin.auditLog.pagination', { page, totalPages, total }) }}</div>
          <div class="flex items-center gap-2">
            <button
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300 disabled:opacity-50"
              type="button"
              :disabled="!canPrev"
              @click="goPrev"
            >
              {{ t('common.prev') }}
            </button>
            <button
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300 disabled:opacity-50"
              type="button"
              :disabled="!canNext"
              @click="goNext"
            >
              {{ t('common.next') }}
            </button>
          </div>
        </div>
      </section>
    </template>
  </div>

  <AppDrawerShell
    :open="Boolean(selectedItem)"
    desktop-width="lg"
    content-class="z-[60]"
    labelled-by="audit-log-drawer-title"
    @close="closeDrawer"
  >
    <template #default="{ panelClass, labelledBy }">
      <section
        v-if="selectedItem"
        :class="[panelClass, 'overflow-hidden']"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="labelledBy"
      >
        <div class="border-b border-slate-200 px-4 py-4 sm:px-6">
          <div class="flex items-start justify-between gap-4">
            <div>
              <div class="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{{ t('admin.auditLog.drawer.title') }}</div>
              <h2 id="audit-log-drawer-title" class="mt-1 text-xl font-semibold text-slate-900">
                {{ getAuditLogActionLabel(selectedItem.action, t) }}
              </h2>
              <p class="mt-1 text-sm text-slate-500">{{ formatUtcDateTimeLocal(selectedItem.createdAt) }}</p>
            </div>
            <button
              class="rounded-full border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50"
              type="button"
              @click="closeDrawer"
            >
              {{ t('common.dismiss') }}
            </button>
          </div>
        </div>

        <div class="min-h-0 flex-1 space-y-4 overflow-y-auto px-4 py-4 sm:px-6">
          <section class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
            <h3 class="text-sm font-semibold text-slate-900">{{ t('admin.auditLog.drawer.summary') }}</h3>
            <dl class="mt-3 grid gap-3 text-sm sm:grid-cols-2">
              <div>
                <dt class="text-slate-500">{{ t('admin.auditLog.columns.time') }}</dt>
                <dd class="font-medium text-slate-900">{{ formatUtcDateTimeLocal(selectedItem.createdAt) }}</dd>
              </div>
              <div>
                <dt class="text-slate-500">{{ t('admin.auditLog.columns.result') }}</dt>
                <dd>
                  <span class="inline-flex items-center rounded-full px-3 py-1 text-xs font-semibold" :class="getAuditLogResultClass(selectedItem.result)">
                    {{ getAuditLogResultLabel(selectedItem.result, t) }}
                  </span>
                </dd>
              </div>
              <div>
                <dt class="text-slate-500">{{ t('admin.auditLog.columns.user') }}</dt>
                <dd class="font-medium text-slate-900">{{ formatActor(selectedItem) }}</dd>
                <dd v-if="formatActorSecondary(selectedItem)" class="text-xs text-slate-500">{{ formatActorSecondary(selectedItem) }}</dd>
              </div>
              <div>
                <dt class="text-slate-500">{{ t('admin.auditLog.fields.role') }}</dt>
                <dd class="font-medium text-slate-900">{{ selectedItem.role || '—' }}</dd>
              </div>
              <div class="sm:col-span-2">
                <dt class="text-slate-500">{{ t('admin.auditLog.columns.target') }}</dt>
                <dd class="font-medium text-slate-900">{{ formatTarget(selectedItem) }}</dd>
              </div>
            </dl>
          </section>

          <section class="rounded-2xl border border-slate-200 bg-white p-4">
            <h3 class="text-sm font-semibold text-slate-900">{{ t('admin.auditLog.drawer.details') }}</h3>
            <div v-if="drawerDetails && (drawerDetails.items.length > 0 || drawerDetails.changes.length > 0)" class="mt-3 space-y-4">
              <dl v-if="drawerDetails.items.length > 0" class="grid gap-3 text-sm sm:grid-cols-2">
                <div v-for="detail in drawerDetails.items" :key="`detail-${detail.label}`">
                  <dt class="text-slate-500">{{ detail.label }}</dt>
                  <dd class="font-medium text-slate-900">{{ detail.value }}</dd>
                </div>
              </dl>

              <div v-if="drawerDetails.changes.length > 0" class="space-y-3">
                <div class="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{{ t('admin.auditLog.drawer.changes') }}</div>
                <div class="space-y-2">
                  <div
                    v-for="change in drawerDetails.changes"
                    :key="`change-${change.label}`"
                    class="rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm"
                  >
                    <div class="font-medium text-slate-900">{{ change.label }}</div>
                    <div class="mt-1 whitespace-pre-wrap break-words text-slate-600">{{ change.value }}</div>
                  </div>
                </div>
              </div>
            </div>
            <p v-else class="mt-3 text-sm text-slate-500">{{ t('admin.auditLog.drawer.noDetails') }}</p>
          </section>

          <details class="rounded-2xl border border-slate-200 bg-white p-4">
            <summary class="cursor-pointer text-sm font-semibold text-slate-900">{{ t('admin.auditLog.drawer.technical') }}</summary>
            <div class="mt-4 space-y-4">
              <dl class="grid gap-3 text-sm sm:grid-cols-2">
                <div>
                  <dt class="text-slate-500">{{ t('admin.auditLog.fields.rawAction') }}</dt>
                  <dd class="font-mono text-xs text-slate-900">{{ selectedItem.action }}</dd>
                </div>
                <div>
                  <dt class="text-slate-500">{{ t('admin.auditLog.fields.ipAddress') }}</dt>
                  <dd class="font-mono text-xs text-slate-900">{{ selectedItem.ipAddress || '—' }}</dd>
                </div>
                <div>
                  <dt class="text-slate-500">{{ t('admin.auditLog.fields.userId') }}</dt>
                  <dd class="font-mono text-xs text-slate-900">{{ selectedItem.userId || '—' }}</dd>
                </div>
                <div>
                  <dt class="text-slate-500">{{ t('admin.auditLog.fields.organizationId') }}</dt>
                  <dd class="font-mono text-xs text-slate-900">{{ selectedItem.organizationId || '—' }}</dd>
                </div>
                <div class="sm:col-span-2">
                  <dt class="text-slate-500">{{ t('admin.auditLog.fields.targetId') }}</dt>
                  <dd class="font-mono text-xs text-slate-900">{{ selectedItem.targetId || '—' }}</dd>
                </div>
              </dl>

              <div v-if="drawerDetails?.prettyJson" class="space-y-2">
                <div class="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{{ t('admin.auditLog.fields.rawExtraJson') }}</div>
                <pre class="overflow-x-auto rounded-2xl border border-slate-200 bg-slate-50 p-3 text-xs text-slate-700">{{ drawerDetails.prettyJson }}</pre>
              </div>
            </div>
          </details>
        </div>
      </section>
    </template>
  </AppDrawerShell>
</template>
