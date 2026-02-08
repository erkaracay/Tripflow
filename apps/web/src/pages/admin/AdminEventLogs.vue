<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet } from '../../lib/api'
import { formatUtcDateTimeLocal } from '../../lib/formatters'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Event as EventDto, EventParticipantLogItem, EventParticipantLogListResponse } from '../../types'

const route = useRoute()
const { t } = useI18n()

const eventId = computed(() => String(route.params.eventId ?? ''))

const event = ref<EventDto | null>(null)
const eventError = ref<string | null>(null)

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const items = ref<EventParticipantLogItem[]>([])
const total = ref(0)

const page = ref(1)
const pageSize = ref(50)
const direction = ref<'all' | 'entry' | 'exit'>('all')
const method = ref<'all' | 'manual' | 'qrscan'>('all')
const result = ref<'all' | 'Success' | 'AlreadyArrived' | 'NotFound' | 'InvalidRequest' | 'Failed'>('all')
const from = ref('')
const to = ref('')

const searchInput = ref('')
const searchQuery = ref('')
const sort = ref('createdAt')
const dir = ref<'asc' | 'desc'>('desc')
const debounceHandle = ref<number | ReturnType<typeof setTimeout> | null>(null)

const totalPages = computed(() => Math.max(Math.ceil(total.value / pageSize.value), 1))
const canPrev = computed(() => page.value > 1)
const canNext = computed(() => page.value < totalPages.value)

const loadEvent = async () => {
  eventError.value = null
  try {
    event.value = await apiGet<EventDto>(`/api/events/${eventId.value}`)
  } catch (err) {
    eventError.value = err instanceof Error ? err.message : t('errors.generic')
  }
}

const fetchLogs = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    const response = await apiGet<EventParticipantLogListResponse>(
      `/api/events/${eventId.value}/logs?direction=${encodeURIComponent(direction.value)}&method=${encodeURIComponent(method.value)}&result=${encodeURIComponent(result.value)}&from=${encodeURIComponent(from.value)}&to=${encodeURIComponent(to.value)}&query=${encodeURIComponent(searchQuery.value)}&page=${page.value}&pageSize=${pageSize.value}&sort=${encodeURIComponent(sort.value)}&dir=${dir.value}`
    )
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

const handleSearchInput = () => {
  if (debounceHandle.value) {
    clearTimeout(debounceHandle.value)
  }
  debounceHandle.value = window.setTimeout(() => {
    searchQuery.value = searchInput.value.trim()
    page.value = 1
    void fetchLogs()
  }, 400)
}

const clearSearch = () => {
  searchInput.value = ''
  searchQuery.value = ''
  page.value = 1
  void fetchLogs()
}

const applyQuickFilter = (preset: 'exit' | 'notFound' | 'qr') => {
  if (preset === 'exit') {
    direction.value = direction.value === 'exit' ? 'all' : 'exit'
  } else if (preset === 'notFound') {
    result.value = result.value === 'NotFound' ? 'all' : 'NotFound'
  } else if (preset === 'qr') {
    method.value = method.value === 'qrscan' ? 'all' : 'qrscan'
  }
  page.value = 1
  void fetchLogs()
}

const onFiltersChange = () => {
  page.value = 1
  void fetchLogs()
}

const onPageSizeChange = () => {
  page.value = 1
  void fetchLogs()
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

const setSort = (col: string) => {
  if (sort.value === col) {
    dir.value = dir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sort.value = col
    dir.value = col === 'createdAt' ? 'desc' : 'asc'
  }
  page.value = 1
  void fetchLogs()
}

const sortIndicator = (col: string) => {
  if (sort.value !== col) return ''
  return dir.value === 'asc' ? ' ↑' : ' ↓'
}

const buildCsvValue = (value: string | number | null | undefined) =>
  `"${String(value ?? '').replace(/"/g, '""')}"`

const exportCsv = () => {
  if (items.value.length === 0) return

  const headers = [
    'createdAt',
    'direction',
    'method',
    'result',
    'participantName',
    'checkInCode',
    'participantTcNo',
    'participantPhone',
    'actorEmail',
    'actorRole',
    'ipAddress',
    'userAgent',
  ]

  const rows = items.value.map((row) => [
    formatUtcDateTimeLocal(row.createdAt),
    row.direction,
    row.method,
    row.result,
    row.participantName ?? '',
    row.checkInCode ?? '',
    row.participantTcNo ?? '',
    row.participantPhone ?? '',
    row.actorEmail ?? '',
    row.actorRole ?? '',
    row.ipAddress ?? '',
    row.userAgent ?? '',
  ])

  const csv = [headers, ...rows]
    .map((row) => row.map((value) => buildCsvValue(value)).join(','))
    .join('\n')

  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const link = document.createElement('a')
  const id = eventId.value || 'event'
  const timestamp = new Date()
  const stamp = `${timestamp.getFullYear()}${String(timestamp.getMonth() + 1).padStart(2, '0')}${String(timestamp.getDate()).padStart(2, '0')}_${String(timestamp.getHours()).padStart(2, '0')}${String(timestamp.getMinutes()).padStart(2, '0')}`
  link.href = URL.createObjectURL(blob)
  link.download = `logs_${id}_${stamp}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(link.href)
}

const formatDirection = (value: string) => {
  if (value === 'Entry') return t('common.entry')
  if (value === 'Exit') return t('common.exit')
  return value
}

const directionBadgeClass = (value: string) => {
  if (value === 'Entry') return 'bg-emerald-100 text-emerald-700'
  if (value === 'Exit') return 'bg-rose-100 text-rose-700'
  return 'bg-slate-100 text-slate-700'
}

const formatMethod = (value: string) => {
  if (value === 'Manual') return t('admin.logs.methods.manual')
  if (value === 'QrScan') return t('admin.logs.methods.qrScan')
  return value
}

const formatResult = (value: string) => {
  if (value === 'Success') return t('admin.logs.results.success')
  if (value === 'AlreadyArrived') return t('admin.logs.results.alreadyArrived')
  if (value === 'NotFound') return t('admin.logs.results.notFound')
  if (value === 'InvalidRequest') return t('admin.logs.results.invalidRequest')
  if (value === 'Failed') return t('admin.logs.results.failed')
  return value
}

const resultBadgeClass = (value: string) => {
  if (value === 'Success') return 'bg-emerald-100 text-emerald-700'
  if (value === 'AlreadyArrived') return 'bg-slate-100 text-slate-700'
  if (value === 'NotFound' || value === 'InvalidRequest') return 'bg-amber-100 text-amber-700'
  if (value === 'Failed') return 'bg-rose-100 text-rose-700'
  return 'bg-slate-100 text-slate-700'
}

onMounted(fetchLogs)
onMounted(loadEvent)
</script>

<template>
  <div class="mx-auto max-w-7xl space-y-6">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <RouterLink
          class="text-sm text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
          :to="`/admin/events/${eventId}`"
        >
          {{ t('nav.backToEvent') }}
        </RouterLink>
        <h1 class="mt-1 text-2xl font-semibold text-slate-900">
          {{ t('admin.logs.title') }}
        </h1>
        <p v-if="event?.name" class="mt-1 text-sm text-slate-500">
          {{ event.name }}
        </p>
        <p v-else-if="eventError" class="mt-1 text-sm text-rose-500">
          {{ eventError }}
        </p>
      </div>
      <div class="flex w-full flex-col items-start gap-1 sm:w-auto sm:items-end">
        <button
          class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300 sm:w-auto"
          type="button"
          @click="exportCsv"
        >
          {{ t('admin.logs.exportCsv') }}
        </button>
        <span class="text-xs text-slate-400 sm:text-right">{{ t('admin.logs.exportNote') }}</span>
      </div>
    </div>

    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="mb-4 flex flex-wrap items-center gap-2">
        <button
          class="rounded-full border px-3 py-1.5 text-xs font-semibold"
          type="button"
          :class="
            direction === 'exit'
              ? 'border-slate-900 bg-slate-900 text-white'
              : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
          "
          @click="applyQuickFilter('exit')"
        >
          {{ t('admin.logs.quickFilters.onlyExit') }}
        </button>
        <button
          class="rounded-full border px-3 py-1.5 text-xs font-semibold"
          type="button"
          :class="
            result === 'NotFound'
              ? 'border-slate-900 bg-slate-900 text-white'
              : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
          "
          @click="applyQuickFilter('notFound')"
        >
          {{ t('admin.logs.quickFilters.onlyNotFound') }}
        </button>
        <button
          class="rounded-full border px-3 py-1.5 text-xs font-semibold"
          type="button"
          :class="
            method === 'qrscan'
              ? 'border-slate-900 bg-slate-900 text-white'
              : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
          "
          @click="applyQuickFilter('qr')"
        >
          {{ t('admin.logs.quickFilters.onlyQr') }}
        </button>
      </div>

      <div class="grid gap-3 md:grid-cols-6">
        <div class="md:col-span-2">
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.logs.searchLabel') }}</label>
          <div class="mt-1 flex items-center gap-2 rounded border border-slate-200 bg-white px-2 py-1.5">
            <input
              v-model="searchInput"
              class="w-full text-sm text-slate-700 focus:outline-none"
              :placeholder="t('admin.logs.searchPlaceholder')"
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
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.logs.directionLabel') }}</label>
          <select
            v-model="direction"
            class="mt-1 w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
            @change="onFiltersChange"
          >
            <option value="all">{{ t('admin.logs.all') }}</option>
            <option value="entry">{{ t('common.entry') }}</option>
            <option value="exit">{{ t('common.exit') }}</option>
          </select>
        </div>

        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.logs.methodLabel') }}</label>
          <select
            v-model="method"
            class="mt-1 w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
            @change="onFiltersChange"
          >
            <option value="all">{{ t('admin.logs.all') }}</option>
            <option value="manual">{{ t('admin.logs.methods.manual') }}</option>
            <option value="qrscan">{{ t('admin.logs.methods.qrScan') }}</option>
          </select>
        </div>

        <div>
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.logs.resultLabel') }}</label>
          <select
            v-model="result"
            class="mt-1 w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
            @change="onFiltersChange"
          >
            <option value="all">{{ t('admin.logs.all') }}</option>
            <option value="Success">{{ t('admin.logs.results.success') }}</option>
            <option value="AlreadyArrived">{{ t('admin.logs.results.alreadyArrived') }}</option>
            <option value="NotFound">{{ t('admin.logs.results.notFound') }}</option>
            <option value="InvalidRequest">{{ t('admin.logs.results.invalidRequest') }}</option>
            <option value="Failed">{{ t('admin.logs.results.failed') }}</option>
          </select>
        </div>

        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 md:col-span-4">
          <div>
            <label class="text-xs font-semibold text-slate-500">{{ t('admin.logs.fromLabel') }}</label>
            <input
              v-model="from"
              class="mt-1 w-full min-w-[160px] rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
              type="date"
              @change="onFiltersChange"
            />
          </div>
          <div>
            <label class="text-xs font-semibold text-slate-500">{{ t('admin.logs.toLabel') }}</label>
            <input
              v-model="to"
              class="mt-1 w-full min-w-[160px] rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700"
              type="date"
              @change="onFiltersChange"
            />
          </div>
        </div>
      </div>

      <div class="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-slate-500">
        <div>{{ t('admin.logs.pagination', { page, totalPages, total }) }}</div>
        <div class="flex items-center gap-2">
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.logs.pageSizeLabel') }}</label>
          <select
            v-model.number="pageSize"
            class="rounded border border-slate-200 bg-white px-2 py-1 text-sm text-slate-700"
            @change="onPageSizeChange"
          >
            <option :value="25">25</option>
            <option :value="50">50</option>
            <option :value="100">100</option>
            <option :value="200">200</option>
          </select>
        </div>
      </div>
    </section>

    <LoadingState v-if="loading" message-key="common.loading" />
    <ErrorState v-if="!loading && errorMessage" :message="errorMessage" @retry="fetchLogs" />

    <section v-if="!loading" class="rounded-2xl border border-slate-200 bg-white shadow-sm">
      <div class="overflow-x-auto">
        <table class="min-w-[900px] w-full border-separate border-spacing-0 text-left text-sm">
          <thead class="sticky top-0 bg-white">
            <tr class="text-xs font-semibold uppercase tracking-wide text-slate-500">
              <th class="border-b border-slate-200 px-4 py-3">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-100"
                  @click="setSort('createdAt')"
                >
                  {{ t('admin.logs.columns.createdAt') }}{{ sortIndicator('createdAt') }}
                </button>
              </th>
              <th class="border-b border-slate-200 px-4 py-3">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-100"
                  @click="setSort('direction')"
                >
                  {{ t('admin.logs.columns.direction') }}{{ sortIndicator('direction') }}
                </button>
              </th>
              <th class="border-b border-slate-200 px-4 py-3">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-100"
                  @click="setSort('method')"
                >
                  {{ t('admin.logs.columns.method') }}{{ sortIndicator('method') }}
                </button>
              </th>
              <th class="border-b border-slate-200 px-4 py-3">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-100"
                  @click="setSort('result')"
                >
                  {{ t('admin.logs.columns.result') }}{{ sortIndicator('result') }}
                </button>
              </th>
              <th class="border-b border-slate-200 px-4 py-3">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-100"
                  @click="setSort('participantName')"
                >
                  {{ t('admin.logs.columns.participant') }}{{ sortIndicator('participantName') }}
                </button>
              </th>
              <th class="border-b border-slate-200 px-4 py-3">{{ t('admin.logs.columns.code') }}</th>
              <th class="border-b border-slate-200 px-4 py-3">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-100"
                  @click="setSort('actorEmail')"
                >
                  {{ t('admin.logs.columns.actor') }}{{ sortIndicator('actorEmail') }}
                </button>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="items.length === 0">
              <td class="px-4 py-6 text-sm text-slate-500" colspan="7">{{ t('admin.logs.empty') }}</td>
            </tr>
            <tr
              v-for="row in items"
              :key="row.id"
              class="border-b border-slate-100 last:border-b-0"
            >
              <td class="px-4 py-3 font-mono text-xs text-slate-600">{{ formatUtcDateTimeLocal(row.createdAt) }}</td>
              <td class="px-4 py-3">
                <span
                  class="inline-flex items-center whitespace-nowrap rounded-full px-3 py-1 text-xs font-semibold"
                  :class="directionBadgeClass(row.direction)"
                >
                  {{ formatDirection(row.direction) }}
                </span>
              </td>
              <td class="px-4 py-3">{{ formatMethod(row.method) }}</td>
              <td class="px-4 py-3">
                <span
                  class="inline-flex items-center whitespace-nowrap rounded-full px-3 py-1 text-xs font-semibold"
                  :class="resultBadgeClass(row.result)"
                >
                  {{ formatResult(row.result) }}
                </span>
              </td>
              <td class="px-4 py-3">
                <div class="font-medium text-slate-900">
                  <RouterLink
                    v-if="row.participantId"
                    class="underline-offset-2 hover:underline"
                    :to="`/admin/events/${eventId}/participants/${row.participantId}`"
                  >
                    {{ row.participantName ?? '—' }}
                  </RouterLink>
                  <span v-else>—</span>
                </div>
                <div
                  v-if="row.participantId && (row.participantTcNo || row.participantPhone)"
                  class="mt-0.5 text-xs text-slate-500"
                >
                  <span v-if="row.participantTcNo">{{ row.participantTcNo }}</span>
                  <span v-if="row.participantTcNo && row.participantPhone"> · </span>
                  <span v-if="row.participantPhone">{{ row.participantPhone }}</span>
                </div>
              </td>
              <td class="px-4 py-3 font-mono text-xs text-slate-700">{{ row.checkInCode ?? '—' }}</td>
              <td class="px-4 py-3">
                <div class="text-slate-900">{{ row.actorEmail ?? '—' }}</div>
                <div v-if="row.actorRole" class="mt-0.5 text-xs text-slate-500">{{ row.actorRole }}</div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="flex items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm">
        <div class="text-slate-500">
          {{ t('admin.logs.pagination', { page, totalPages, total }) }}
        </div>
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
  </div>
</template>
