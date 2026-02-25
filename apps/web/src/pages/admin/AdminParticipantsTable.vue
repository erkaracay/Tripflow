<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet } from '../../lib/api'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import ParticipantFlightsModal from '../../components/admin/ParticipantFlightsModal.vue'
import { formatDate } from '../../lib/formatters'
import {
  buildFlightSegmentsSheetRows,
  buildParticipantsSheetRows,
  FLIGHT_SEGMENTS_SHEET_HEADERS,
  PARTICIPANTS_SHEET_HEADERS,
} from '../../lib/participantsExportWorkbook'
import type {
  Event as EventDto,
  ParticipantProfile,
  ParticipantTableItem,
  ParticipantTableResponse,
} from '../../types'

const route = useRoute()
const { t } = useI18n()
const { pushToast } = useToast()

const eventId = computed(() => String(route.params.eventId ?? ''))

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const items = ref<ParticipantTableItem[]>([])
const total = ref(0)
const event = ref<EventDto | null>(null)
const eventError = ref<string | null>(null)
const page = ref(1)
const pageSize = ref(50)
const status = ref<'all' | 'arrived' | 'not_arrived'>('all')
const searchInput = ref('')
const searchQuery = ref('')
const sort = ref('fullName')
const dir = ref<'asc' | 'desc'>('asc')
const debounceHandle = ref<number | ReturnType<typeof setTimeout> | null>(null)

const totalPages = computed(() => Math.max(Math.ceil(total.value / pageSize.value), 1))
const canPrev = computed(() => page.value > 1)
const canNext = computed(() => page.value < totalPages.value)

const tableItems = computed(() => items.value)
const hasItems = computed(() => tableItems.value.length > 0)

const participantDisplayName = (participant: Pick<ParticipantTableItem, 'firstName' | 'lastName' | 'fullName'>) => {
  const first = participant.firstName?.trim() ?? ''
  const last = participant.lastName?.trim() ?? ''
  const combined = `${first} ${last}`.trim()
  return combined || participant.fullName
}

const loadEvent = async () => {
  eventError.value = null
  try {
    event.value = await apiGet<EventDto>(`/api/events/${eventId.value}`)
  } catch (err) {
    eventError.value = err instanceof Error ? err.message : t('errors.generic')
  }
}

const fetchTable = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    const response = await apiGet<ParticipantTableResponse>(
      `/api/events/${eventId.value}/participants/table?query=${encodeURIComponent(searchQuery.value)}&status=${status.value}&page=${page.value}&pageSize=${pageSize.value}&sort=${sort.value}&dir=${dir.value}`
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
    void fetchTable()
  }, 400)
}

const clearSearch = () => {
  searchInput.value = ''
  searchQuery.value = ''
  page.value = 1
  void fetchTable()
}

const onStatusChange = () => {
  page.value = 1
  void fetchTable()
}

const onPageSizeChange = () => {
  page.value = 1
  void fetchTable()
}

const goPrev = () => {
  if (!canPrev.value) return
  page.value -= 1
  void fetchTable()
}

const goNext = () => {
  if (!canNext.value) return
  page.value += 1
  void fetchTable()
}

const setSort = (col: string) => {
  const apiKey = col === 'roomNo' ? 'roomno' : col === 'agencyName' ? 'agencyname' : col === 'arrivedAt' ? 'arrivedAt' : 'fullName'
  if (sort.value === apiKey) {
    dir.value = dir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sort.value = apiKey
    dir.value = 'asc'
  }
  page.value = 1
  void fetchTable()
}

const sortIndicator = (col: string) => {
  const apiKey = col === 'roomNo' ? 'roomno' : col === 'agencyName' ? 'agencyname' : col === 'arrivedAt' ? 'arrivedAt' : 'fullName'
  if (sort.value !== apiKey) return ''
  return dir.value === 'asc' ? ' ↑' : ' ↓'
}

const fetchProfilesForExport = async (participantIds: string[]) => {
  const profilesById = new Map<string, ParticipantProfile | null>()
  const failedParticipantIds: string[] = []

  if (participantIds.length === 0) {
    return { profilesById, failedParticipantIds }
  }

  const ids = [...participantIds]
  const concurrency = Math.min(8, ids.length)
  let cursor = 0

  const workers = Array.from({ length: concurrency }, async () => {
    while (true) {
      const index = cursor
      cursor += 1
      if (index >= ids.length) {
        break
      }

      const participantId = ids[index]
      if (!participantId) {
        continue
      }
      try {
        const profile = await apiGet<ParticipantProfile>(
          `/api/events/${eventId.value}/participants/${participantId}`
        )
        profilesById.set(participantId, profile)
      } catch {
        profilesById.set(participantId, null)
        failedParticipantIds.push(participantId)
      }
    }
  })

  await Promise.all(workers)
  return { profilesById, failedParticipantIds }
}

const exportExcel = async () => {
  if (tableItems.value.length === 0) return

  try {
    const participantIds = tableItems.value.map((row) => row.id)
    const { profilesById, failedParticipantIds } = await fetchProfilesForExport(participantIds)
    const participantRows = buildParticipantsSheetRows(tableItems.value)
    const flightSegmentRows = buildFlightSegmentsSheetRows(tableItems.value, profilesById)

    const { utils, writeFile } = await import('xlsx')
    const workbook = utils.book_new()
    const participantsSheet = utils.aoa_to_sheet([PARTICIPANTS_SHEET_HEADERS, ...participantRows])
    const segmentsSheet = utils.aoa_to_sheet([FLIGHT_SEGMENTS_SHEET_HEADERS, ...flightSegmentRows])

    utils.book_append_sheet(workbook, participantsSheet, 'participants')
    utils.book_append_sheet(workbook, segmentsSheet, 'flight_segments')

    const id = route.params.eventId ?? 'event'
    const timestamp = new Date()
    const stamp = `${timestamp.getFullYear()}${String(timestamp.getMonth() + 1).padStart(2, '0')}${String(timestamp.getDate()).padStart(2, '0')}_${String(timestamp.getHours()).padStart(2, '0')}${String(timestamp.getMinutes()).padStart(2, '0')}`
    writeFile(workbook, `participants_${id}_${stamp}.xlsx`)

    if (failedParticipantIds.length > 0) {
      pushToast({
        key: 'admin.participantsTable.exportSegmentsPartial',
        params: { count: failedParticipantIds.length },
        tone: 'info',
      })
    }
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  }
}

watch([status, pageSize], () => {
  page.value = 1
})

onMounted(fetchTable)
onMounted(loadEvent)
</script>

<template>
  <div class="mx-auto max-w-7xl space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <RouterLink
          class="text-sm text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
          :to="`/admin/events/${eventId}`"
        >
          {{ t('nav.backToEvent') }}
        </RouterLink>
        <h1 class="mt-1 text-2xl font-semibold text-slate-900">
          {{ t('admin.participantsTable.title') }}
        </h1>
        <p v-if="event?.name" class="mt-1 text-sm text-slate-500">
          {{ event.name }}
        </p>
        <p v-else-if="eventError" class="mt-1 text-sm text-rose-500">
          {{ eventError }}
        </p>
      </div>
      <button
        class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300"
        type="button"
        @click="exportExcel"
      >
        {{ t('admin.participantsTable.exportExcel') }}
      </button>
    </div>

    <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="flex flex-wrap items-center gap-3">
        <div class="flex-1 min-w-[220px]">
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.participantsTable.searchLabel') }}</label>
          <div class="mt-1 flex items-center gap-2 rounded border border-slate-200 bg-white px-2 py-1.5">
            <input
              v-model="searchInput"
              class="w-full text-sm text-slate-700 focus:outline-none"
              :placeholder="t('admin.participantsTable.searchPlaceholder')"
              type="text"
              @input="handleSearchInput"
            />
            <button
              v-if="searchInput"
              class="text-xs text-slate-500 hover:text-slate-700"
              type="button"
              @click="clearSearch"
            >
              {{ t('common.clearSearch') }}
            </button>
          </div>
        </div>

        <div class="min-w-[160px]">
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.participantsTable.statusLabel') }}</label>
          <select
            v-model="status"
            class="mt-1 w-full rounded border border-slate-200 bg-white px-2 py-1.5 text-sm"
            @change="onStatusChange"
          >
            <option value="all">{{ t('admin.participantsTable.statusAll') }}</option>
            <option value="arrived">{{ t('admin.participantsTable.statusArrived') }}</option>
            <option value="not_arrived">{{ t('admin.participantsTable.statusNotArrived') }}</option>
          </select>
        </div>

        <div class="min-w-[140px]">
          <label class="text-xs font-semibold text-slate-500">{{ t('admin.participantsTable.pageSizeLabel') }}</label>
          <select
            v-model.number="pageSize"
            class="mt-1 w-full rounded border border-slate-200 bg-white px-2 py-1.5 text-sm"
            @change="onPageSizeChange"
          >
            <option :value="25">25</option>
            <option :value="50">50</option>
            <option :value="100">100</option>
            <option :value="200">200</option>
          </select>
        </div>

        <div class="ml-auto text-xs text-slate-500">
          {{ t('admin.participantsTable.exportNote') }}
        </div>
      </div>
    </section>

    <LoadingState v-if="loading" message-key="common.loading" />
    <ErrorState v-else-if="errorMessage && !hasItems" :message="errorMessage" @retry="fetchTable" />
    <div
      v-else-if="errorMessage && hasItems"
      class="rounded border border-rose-200 bg-rose-50 px-4 py-2 text-sm text-rose-700"
    >
      {{ errorMessage }}
    </div>

    <section class="rounded-2xl border border-slate-200 bg-white shadow-sm">
      <div class="max-h-[70vh] overflow-auto">
        <table class="min-w-full text-sm">
          <thead class="sticky top-0 bg-slate-100 text-left text-xs uppercase tracking-wide text-slate-600">
            <tr>
              <th class="px-3 py-2">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-200/80"
                  @click="setSort('fullName')"
                >
                  {{ t('admin.participantsTable.columns.fullName') }}{{ sortIndicator('fullName') }}
                </button>
              </th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.tcNo') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.phone') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.email') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.gender') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.birthDate') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.arrived') }}</th>
              <th class="px-3 py-2">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-200/80"
                  @click="setSort('arrivedAt')"
                >
                  {{ t('admin.participantsTable.columns.arrivedAt') }}{{ sortIndicator('arrivedAt') }}
                </button>
              </th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.checkInCode') }}</th>
              <th class="px-3 py-2">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-200/80"
                  @click="setSort('roomNo')"
                >
                  {{ t('admin.participantsTable.columns.roomNo') }}{{ sortIndicator('roomNo') }}
                </button>
              </th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.roomType') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.personNo') }}</th>
              <th class="px-3 py-2">
                <button
                  type="button"
                  class="inline-flex cursor-pointer select-none items-center gap-0.5 rounded hover:bg-slate-200/80"
                  @click="setSort('agencyName')"
                >
                  {{ t('admin.participantsTable.columns.agencyName') }}{{ sortIndicator('agencyName') }}
                </button>
              </th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.city') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.flightCity') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.hotelCheckInDate') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.hotelCheckOutDate') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.ticketNo') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.attendanceStatus') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.flights') }}</th>
              <th class="px-3 py-2">{{ t('admin.participantsTable.columns.actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in tableItems" :key="row.id" class="odd:bg-white even:bg-slate-50">
              <td class="px-3 py-2 text-xs text-slate-700">{{ participantDisplayName(row) }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.tcNo }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.phone }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.email ?? '—' }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.gender }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ formatDate(row.birthDate) }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">
                <span
                  class="inline-flex rounded-full px-2 py-0.5 text-xs"
                  :class="row.arrived ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'"
                >
                  {{ row.arrived ? t('common.arrivedLabel') : t('common.pendingLabel') }}
                </span>
              </td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.arrivedAt ?? '—' }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.checkInCode }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.details?.roomNo ?? '—' }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.details?.roomType ?? '—' }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.details?.personNo ?? '—' }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.details?.agencyName ?? '—' }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.details?.city ?? '—' }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.details?.flightCity ?? '—' }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ formatDate(row.details?.hotelCheckInDate) }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ formatDate(row.details?.hotelCheckOutDate) }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">
                <div v-if="row.details?.arrivalTicketNo || row.details?.ticketNo || row.details?.returnTicketNo" class="flex flex-col gap-1">
                  <div v-if="row.details?.arrivalTicketNo || row.details?.ticketNo" class="flex items-baseline gap-1">
                    <span class="text-[10px] font-semibold text-slate-400">{{ t('admin.participantsTable.ticketOutboundShort') }}</span>
                    <span>{{ row.details?.arrivalTicketNo ?? row.details?.ticketNo }}</span>
                  </div>
                  <div v-if="row.details?.returnTicketNo" class="flex items-baseline gap-1">
                    <span class="text-[10px] font-semibold text-slate-400">{{ t('admin.participantsTable.ticketReturnShort') }}</span>
                    <span>{{ row.details?.returnTicketNo }}</span>
                  </div>
                </div>
                <span v-else>—</span>
              </td>
              <td class="px-3 py-2 text-xs text-slate-700">{{ row.details?.attendanceStatus ?? '—' }}</td>
              <td class="px-3 py-2 text-xs text-slate-700">
                <ParticipantFlightsModal
                  :event-id="eventId"
                  :participant-id="row.id"
                  :participant-name="participantDisplayName(row)"
                  :button-label="t('admin.participantsTable.flights.open')"
                  button-class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-slate-300"
                />
              </td>
              <td class="px-3 py-2 text-xs text-slate-700">
                <RouterLink
                  class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-slate-300"
                  :to="`/admin/events/${eventId}/participants/${row.id}`"
                >
                  {{ t('admin.participants.detailsButton') }}
                </RouterLink>
              </td>
            </tr>
            <tr v-if="tableItems.length === 0 && !loading">
              <td class="px-3 py-6 text-center text-sm text-slate-500" colspan="21">
                {{ t('admin.participantsTable.empty') }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm">
        <div class="text-slate-600">
            {{ t('admin.participantsTable.pagination', { page, totalPages, total }) }}
        </div>
        <div class="flex items-center gap-2">
          <button
            class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
            :disabled="!canPrev"
            type="button"
            @click="goPrev"
          >
            {{ t('common.prev') }}
          </button>
          <button
            class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
            :disabled="!canNext"
            type="button"
            @click="goNext"
          >
            {{ t('common.next') }}
          </button>
        </div>
      </div>
    </section>
  </div>
</template>
