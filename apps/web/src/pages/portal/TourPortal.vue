<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import * as QRCode from 'qrcode'
import { apiGet } from '../../lib/api'
import PortalTabBar from '../../components/portal/PortalTabBar.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Tour, TourPortalInfo } from '../../types'

type TabKey = 'days' | 'docs' | 'qr' | 'info'

const route = useRoute()
const tourId = computed(() => route.params.tourId as string)

const tour = ref<Tour | null>(null)
const portal = ref<TourPortalInfo | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const copyStatus = ref<string | null>(null)
const codeCopyStatus = ref<string | null>(null)
const linkCopyStatus = ref<string | null>(null)

const activeTab = ref<TabKey>('days')
const selectedDayIndex = ref(0)

const tabs: { id: TabKey; label: string }[] = [
  { id: 'days', label: 'Days' },
  { id: 'docs', label: 'Docs' },
  { id: 'qr', label: 'QR' },
  { id: 'info', label: 'Info' },
]

const days = computed(() => portal.value?.days ?? [])
const selectedDay = computed(() => days.value[selectedDayIndex.value] ?? null)

const checkInCode = ref('')
const manualCode = ref('')

const qrDataUrl = ref<string | null>(null)

const resolvePublicBase = () => {
  const envBase = (import.meta.env.VITE_PUBLIC_BASE_URL as string | undefined)?.trim()
  if (envBase) {
    return envBase.replace(/\/$/, '')
  }

  return globalThis.location?.origin ?? ''
}

const storageKey = computed(() => `tripflow.checkin.${tourId.value}`)

const persistCheckInCode = (code: string) => {
  if (!code) {
    return
  }

  globalThis.localStorage?.setItem(storageKey.value, code)
}

const clearStoredCode = () => {
  globalThis.localStorage?.removeItem(storageKey.value)
  checkInCode.value = ''
  codeCopyStatus.value = 'Code cleared.'
}

const buildCheckInLink = (code: string) => {
  const base = resolvePublicBase()
  if (!base) {
    return ''
  }

  return `${base}/guide/tours/${tourId.value}/checkin?code=${encodeURIComponent(code)}`
}

const loadPortal = async () => {
  loading.value = true
  error.value = null

  try {
    const [tourData, portalData] = await Promise.all([
      apiGet<Tour>(`/api/tours/${tourId.value}`),
      apiGet<TourPortalInfo>(`/api/tours/${tourId.value}/portal`),
    ])

    tour.value = tourData
    portal.value = portalData
    setDefaultDay()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load tour.'
  } finally {
    loading.value = false
  }
}

const parseDate = (value?: string | null) => {
  if (!value) {
    return null
  }

  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) {
    return null
  }

  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])

  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
    return null
  }

  return new Date(year, month - 1, day)
}

const toDateOnly = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate())

const diffDays = (start: Date, end: Date) => {
  const ms = toDateOnly(end).getTime() - toDateOnly(start).getTime()
  return Math.floor(ms / (24 * 60 * 60 * 1000))
}

const setDefaultDay = () => {
  const dayCount = days.value.length
  if (dayCount === 0) {
    selectedDayIndex.value = 0
    return
  }

  const startDate = parseDate(tour.value?.startDate)
  const endDate = parseDate(tour.value?.endDate)
  const today = toDateOnly(new Date())

  if (!startDate || !endDate) {
    selectedDayIndex.value = 0
    return
  }

  let index = 0
  if (today >= toDateOnly(startDate) && today <= toDateOnly(endDate)) {
    index = diffDays(startDate, today)
  }

  selectedDayIndex.value = Math.min(Math.max(index, 0), dayCount - 1)
}

const selectDay = (index: number) => {
  selectedDayIndex.value = index
}

const setActiveTab = (value: string) => {
  activeTab.value = value as TabKey
}

const resolveCheckInCode = () => {
  const raw = route.query.code ?? route.query.checkInCode
  const queryCode = typeof raw === 'string' ? raw.trim().toUpperCase() : ''

  if (queryCode) {
    checkInCode.value = queryCode
    persistCheckInCode(queryCode)
    return
  }

  const stored = globalThis.localStorage?.getItem(storageKey.value)
  checkInCode.value = stored ?? ''
}

const copyShareLink = async () => {
  copyStatus.value = null
  const url = globalThis.location?.href

  if (!url) {
    copyStatus.value = 'Unable to copy link.'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    copyStatus.value = 'Clipboard not supported.'
    return
  }

  try {
    await clipboard.writeText(url)
    copyStatus.value = 'Link copied.'
  } catch {
    copyStatus.value = 'Copy failed.'
  }
}


const copyCheckInCode = async () => {
  codeCopyStatus.value = null
  if (!checkInCode.value) {
    codeCopyStatus.value = 'No code available.'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    codeCopyStatus.value = 'Clipboard not supported.'
    return
  }

  try {
    await clipboard.writeText(checkInCode.value)
    codeCopyStatus.value = 'Code copied.'
  } catch {
    codeCopyStatus.value = 'Copy failed.'
  }
}

const applyManualCode = () => {
  const normalized = manualCode.value.trim().toUpperCase()
  if (!normalized) {
    codeCopyStatus.value = 'Enter a code first.'
    return
  }

  checkInCode.value = normalized
  persistCheckInCode(normalized)
  manualCode.value = ''
}

const copyCheckInLink = async () => {
  linkCopyStatus.value = null
  if (!checkInCode.value) {
    linkCopyStatus.value = 'Add a code to build the link.'
    return
  }

  const link = buildCheckInLink(checkInCode.value)
  if (!link) {
    linkCopyStatus.value = 'Unable to build link.'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    linkCopyStatus.value = 'Clipboard not supported.'
    return
  }

  try {
    await clipboard.writeText(link)
    linkCopyStatus.value = 'Link copied.'
  } catch {
    linkCopyStatus.value = 'Copy failed.'
  }
}

watch([checkInCode, () => tourId.value], async ([value]) => {
  codeCopyStatus.value = null
  linkCopyStatus.value = null
  if (!value) {
    qrDataUrl.value = null
    return
  }

  persistCheckInCode(value)

  try {
    const deepLink = buildCheckInLink(value)
    if (!deepLink) {
      qrDataUrl.value = null
      return
    }

    qrDataUrl.value = await QRCode.toDataURL(deepLink, {
      width: 200,
      margin: 1,
    })
  } catch {
    qrDataUrl.value = null
  }
}, { immediate: true })

watch(
  () => [route.query.code, route.query.checkInCode, tourId.value],
  resolveCheckInCode,
  { immediate: true }
)

onMounted(loadPortal)
</script>

<template>
  <div class="relative">
    <div class="space-y-6 pb-[calc(96px+env(safe-area-inset-bottom))] sm:space-y-8">
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <p class="text-xs uppercase tracking-wide text-slate-500">Participant Portal</p>
            <h1 class="mt-3 text-2xl font-semibold">
              {{ tour?.name ?? 'Tour' }}
            </h1>
            <p class="text-sm text-slate-500" v-if="tour">
              {{ tour.startDate }} to {{ tour.endDate }}
            </p>
          </div>
          <div class="flex w-full flex-col items-start gap-2 sm:w-auto sm:items-end">
            <button
              class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
              type="button" @click="copyShareLink">
              Copy link
            </button>
            <p v-if="copyStatus" class="text-xs text-slate-500">{{ copyStatus }}</p>
          </div>
        </div>
      </section>

      <div class="sticky top-16 z-10 hidden items-center gap-6 border-b border-slate-200 bg-slate-50/90 px-1 backdrop-blur md:flex">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          class="border-b-2 pb-3 text-sm font-medium transition"
          :class="
            activeTab === tab.id
              ? 'border-slate-900 text-slate-900'
              : 'border-transparent text-slate-500 hover:text-slate-900'
          "
          type="button"
          @click="setActiveTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>

      <LoadingState v-if="loading" message="Loading tour details..." />
      <ErrorState v-else-if="error" :message="error" @retry="loadPortal" />

      <template v-else>
        <div v-if="portal" class="space-y-6 sm:space-y-8">
          <section v-if="activeTab === 'days'"
            class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
            <h2 class="text-lg font-semibold">Itinerary</h2>
            <div v-if="days.length === 0"
              class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
              Itinerary is not available yet.
            </div>
            <template v-else>
              <div class="mt-4 flex gap-2 overflow-x-auto pb-2">
                <button v-for="(day, index) in days" :key="day.day"
                  class="shrink-0 rounded-full border px-4 py-2 text-xs font-medium" :class="selectedDayIndex === index
                      ? 'border-slate-900 bg-slate-900 text-white'
                      : 'border-slate-200 bg-white text-slate-600'
                    " type="button" @click="selectDay(index)">
                  Day {{ day.day }}
                </button>
              </div>

              <div v-if="selectedDay" class="mt-4 rounded-2xl border border-slate-200 bg-slate-50 p-4 sm:p-5">
                <div class="text-sm font-semibold text-slate-800">
                  Day {{ selectedDay.day }} - {{ selectedDay.title }}
                </div>
                <ul class="mt-3 space-y-2 text-sm text-slate-600">
                  <li v-for="item in selectedDay.items" :key="item" class="flex items-start gap-2">
                    <span class="mt-1 h-1.5 w-1.5 rounded-full bg-slate-400"></span>
                    <span>{{ item }}</span>
                  </li>
                </ul>
              </div>
            </template>
          </section>

          <section v-if="activeTab === 'docs'"
            class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
            <h2 class="text-lg font-semibold">Documents</h2>
            <div v-if="portal.links.length === 0"
              class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
              No documents yet.
            </div>
            <ul v-else class="mt-4 space-y-3 text-sm">
              <li v-for="link in portal.links" :key="link.url"
                class="flex flex-col gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                <span class="font-medium text-slate-700">{{ link.label }}</span>
                <a class="inline-flex w-full items-center justify-center rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                  :href="link.url" rel="noreferrer" target="_blank">
                  Open
                </a>
              </li>
            </ul>
          </section>

          <section v-if="activeTab === 'qr'" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
            <h2 class="text-lg font-semibold">Your QR</h2>
            <div class="mt-4 space-y-4">
              <div class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
                <p class="text-xs uppercase tracking-wide text-slate-400">Check-in code</p>
                <div class="mt-2 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-lg font-mono text-slate-800">
                    {{ checkInCode || 'No code yet' }}
                  </div>
                  <div class="flex w-full flex-col gap-2 sm:w-auto sm:flex-row">
                    <button
                      class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                      type="button"
                      @click="copyCheckInCode"
                    >
                      Copy code
                    </button>
                    <button
                      v-if="checkInCode"
                      class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                      type="button"
                      @click="clearStoredCode"
                    >
                      Clear code
                    </button>
                  </div>
                </div>
                <p v-if="codeCopyStatus" class="mt-2 text-xs text-slate-500">{{ codeCopyStatus }}</p>
                <div v-if="!checkInCode" class="mt-4 space-y-2 text-sm text-slate-600">
                  <p>No code saved yet. Paste the code you received from your guide.</p>
                  <div class="flex flex-col gap-2 sm:flex-row">
                    <input
                      v-model.trim="manualCode"
                      class="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm uppercase tracking-wide focus:border-slate-400 focus:outline-none"
                      placeholder="Paste code"
                      type="text"
                      maxlength="8"
                    />
                    <button
                      class="w-full rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 sm:w-auto"
                      type="button"
                      @click="applyManualCode"
                    >
                      Save
                    </button>
                  </div>
                  <p class="text-xs text-slate-500">The QR opens the guide check-in screen with the code prefilled.</p>
                </div>
              </div>

              <div class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
                <p class="text-xs uppercase tracking-wide text-slate-400">QR</p>
                <div class="mt-4 flex items-center justify-center">
                  <div v-if="qrDataUrl"
                    class="flex items-center justify-center rounded-2xl border border-slate-200 bg-white p-4">
                    <img :src="qrDataUrl" alt="QR" class="h-40 w-40" />
                  </div>
                  <div v-else class="text-sm text-slate-500">
                    Add a code to generate the QR.
                  </div>
                </div>
                <div class="mt-4 flex flex-col items-start gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <button
                    class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                    type="button"
                    @click="copyCheckInLink"
                  >
                    Copy guide link
                  </button>
                  <p v-if="linkCopyStatus" class="text-xs text-slate-500">{{ linkCopyStatus }}</p>
                </div>
              </div>
            </div>
          </section>

          <section v-if="activeTab === 'info'" class="space-y-4">
            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">Meeting point</h2>
              <div class="mt-4 grid gap-3 text-sm text-slate-700 sm:grid-cols-2">
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <p class="text-xs uppercase tracking-wide text-slate-400">Time</p>
                  <p class="mt-1 font-medium">{{ portal.meeting.time }}</p>
                </div>
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <p class="text-xs uppercase tracking-wide text-slate-400">Place</p>
                  <p class="mt-1 font-medium">{{ portal.meeting.place }}</p>
                </div>
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3 sm:col-span-2">
                  <p class="text-xs uppercase tracking-wide text-slate-400">Maps</p>
                  <a class="mt-2 inline-flex w-full items-center justify-center rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                    :href="portal.meeting.mapsUrl" rel="noreferrer" target="_blank">
                    Open in Maps
                  </a>
                </div>
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3 sm:col-span-2">
                  <p class="text-xs uppercase tracking-wide text-slate-400">Note</p>
                  <p class="mt-2 text-sm text-slate-600">{{ portal.meeting.note }}</p>
                </div>
              </div>
            </div>

            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">Notes</h2>
              <div v-if="portal.notes.length === 0"
                class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
                No notes yet.
              </div>
              <ul v-else class="mt-4 space-y-2 text-sm text-slate-600">
                <li v-for="note in portal.notes" :key="note"
                  class="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
                  {{ note }}
                </li>
              </ul>
            </div>
          </section>
        </div>

        <div v-else class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500">
          Portal content is not available yet.
        </div>
      </template>
    </div>

    <PortalTabBar :tabs="tabs" :active="activeTab" @select="setActiveTab" />
  </div>
</template>
