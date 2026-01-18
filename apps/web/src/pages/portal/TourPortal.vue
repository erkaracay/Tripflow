<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import * as QRCode from 'qrcode'
import { useI18n } from 'vue-i18n'
import { apiGet } from '../../lib/api'
import PortalTabBar from '../../components/portal/PortalTabBar.vue'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Tour, TourPortalInfo } from '../../types'

type TabKey = 'days' | 'docs' | 'qr' | 'info'

const route = useRoute()
const { t } = useI18n()
const tourId = computed(() => route.params.tourId as string)

const tour = ref<Tour | null>(null)
const portal = ref<TourPortalInfo | null>(null)
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const copyStatusKey = ref<string | null>(null)
const codeCopyStatusKey = ref<string | null>(null)
const linkCopyStatusKey = ref<string | null>(null)

const activeTab = ref<TabKey>('days')
const selectedDayIndex = ref(0)

const tabs = computed<{ id: TabKey; label: string }[]>(() => [
  { id: 'days', label: t('portal.tabs.days') },
  { id: 'docs', label: t('portal.tabs.docs') },
  { id: 'qr', label: t('portal.tabs.qr') },
  { id: 'info', label: t('portal.tabs.info') },
])

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
  codeCopyStatusKey.value = 'portal.qr.codeCleared'
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
  errorKey.value = null
  errorMessage.value = null

  try {
    const [tourData, portalData] = await Promise.all([
      apiGet<Tour>(`/api/tours/${tourId.value}`),
      apiGet<TourPortalInfo>(`/api/tours/${tourId.value}/portal`),
    ])

    tour.value = tourData
    portal.value = portalData
    setDefaultDay()
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.portal.load'
    }
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
  copyStatusKey.value = null
  const url = globalThis.location?.href

  if (!url) {
    copyStatusKey.value = 'portal.actions.copyLinkFailed'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    copyStatusKey.value = 'errors.copyNotSupported'
    return
  }

  try {
    await clipboard.writeText(url)
    copyStatusKey.value = 'portal.actions.linkCopied'
  } catch {
    copyStatusKey.value = 'errors.copyFailed'
  }
}


const copyCheckInCode = async () => {
  codeCopyStatusKey.value = null
  if (!checkInCode.value) {
    codeCopyStatusKey.value = 'portal.qr.noCode'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    codeCopyStatusKey.value = 'errors.copyNotSupported'
    return
  }

  try {
    await clipboard.writeText(checkInCode.value)
    codeCopyStatusKey.value = 'common.copySuccess'
  } catch {
    codeCopyStatusKey.value = 'errors.copyFailed'
  }
}

const applyManualCode = () => {
  const normalized = manualCode.value.trim().toUpperCase()
  if (!normalized) {
    codeCopyStatusKey.value = 'portal.qr.enterCode'
    return
  }

  checkInCode.value = normalized
  persistCheckInCode(normalized)
  manualCode.value = ''
}

const copyCheckInLink = async () => {
  linkCopyStatusKey.value = null
  if (!checkInCode.value) {
    linkCopyStatusKey.value = 'portal.qr.linkNeedsCode'
    return
  }

  const link = buildCheckInLink(checkInCode.value)
  if (!link) {
    linkCopyStatusKey.value = 'portal.actions.copyLinkFailed'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    linkCopyStatusKey.value = 'errors.copyNotSupported'
    return
  }

  try {
    await clipboard.writeText(link)
    linkCopyStatusKey.value = 'portal.actions.linkCopied'
  } catch {
    linkCopyStatusKey.value = 'errors.copyFailed'
  }
}

watch([checkInCode, () => tourId.value], async ([value]) => {
  codeCopyStatusKey.value = null
  linkCopyStatusKey.value = null
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
            <p class="text-xs uppercase tracking-wide text-slate-500">{{ t('portal.header.kicker') }}</p>
            <h1 class="mt-3 text-2xl font-semibold">
              {{ tour?.name ?? t('common.tour') }}
            </h1>
            <p class="text-sm text-slate-500" v-if="tour">
              {{ t('common.dateRange', { start: tour.startDate, end: tour.endDate }) }}
            </p>
          </div>
          <div class="flex w-full flex-col items-start gap-2 sm:w-auto sm:items-end">
            <button
              class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
              type="button" @click="copyShareLink">
              {{ t('portal.actions.copyPageLink') }}
            </button>
            <p v-if="copyStatusKey" class="text-xs text-slate-500">{{ t(copyStatusKey) }}</p>
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

      <LoadingState v-if="loading" message-key="portal.loading" />
      <ErrorState
        v-else-if="errorKey || errorMessage"
        :message="errorMessage ?? undefined"
        :message-key="errorKey ?? undefined"
        @retry="loadPortal"
      />

      <template v-else>
        <div v-if="portal" class="space-y-6 sm:space-y-8">
          <section v-if="activeTab === 'days'"
            class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
            <h2 class="text-lg font-semibold">{{ t('portal.days.title') }}</h2>
            <div v-if="days.length === 0"
              class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
              {{ t('portal.days.empty') }}
            </div>
            <template v-else>
              <div class="mt-4 flex gap-2 overflow-x-auto pb-2">
                <button v-for="(day, index) in days" :key="day.day"
                  class="shrink-0 rounded-full border px-4 py-2 text-xs font-medium" :class="selectedDayIndex === index
                      ? 'border-slate-900 bg-slate-900 text-white'
                      : 'border-slate-200 bg-white text-slate-600'
                    " type="button" @click="selectDay(index)">
                  {{ t('portal.days.dayLabel', { day: day.day }) }}
                </button>
              </div>

              <div v-if="selectedDay" class="mt-4 rounded-2xl border border-slate-200 bg-slate-50 p-4 sm:p-5">
                <div class="text-sm font-semibold text-slate-800">
                  {{ t('portal.days.dayTitle', { day: selectedDay.day, title: selectedDay.title }) }}
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
            <h2 class="text-lg font-semibold">{{ t('portal.docs.title') }}</h2>
            <div v-if="portal.links.length === 0"
              class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
              {{ t('portal.docs.empty') }}
            </div>
            <ul v-else class="mt-4 space-y-3 text-sm">
              <li v-for="link in portal.links" :key="link.url"
                class="flex flex-col gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
                <span class="font-medium text-slate-700">{{ link.label }}</span>
                <a class="inline-flex w-full items-center justify-center rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                  :href="link.url" rel="noreferrer" target="_blank">
                  {{ t('common.open') }}
                </a>
              </li>
            </ul>
          </section>

          <section v-if="activeTab === 'qr'" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
            <h2 class="text-lg font-semibold">{{ t('portal.qr.title') }}</h2>
            <div class="mt-4 space-y-4">
              <div class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
                <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.qr.codeLabel') }}</p>
                <div class="mt-2 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-lg font-mono text-slate-800">
                    {{ checkInCode || t('portal.qr.noCode') }}
                  </div>
                  <div class="flex w-full flex-col gap-2 sm:w-auto sm:flex-row">
                    <button
                      class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                      type="button"
                      @click="copyCheckInCode"
                    >
                      {{ t('portal.qr.copyCode') }}
                    </button>
                    <button
                      v-if="checkInCode"
                      class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                      type="button"
                      @click="clearStoredCode"
                    >
                      {{ t('portal.qr.clearCode') }}
                    </button>
                  </div>
                </div>
                <p v-if="codeCopyStatusKey" class="mt-2 text-xs text-slate-500">{{ t(codeCopyStatusKey) }}</p>
                <div v-if="!checkInCode" class="mt-4 space-y-2 text-sm text-slate-600">
                  <p>{{ t('portal.qr.emptyHint') }}</p>
                  <div class="flex flex-col gap-2 sm:flex-row">
                    <input
                      v-model.trim="manualCode"
                      class="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm uppercase tracking-wide focus:border-slate-400 focus:outline-none"
                      :placeholder="t('portal.qr.pastePlaceholder')"
                      type="text"
                      maxlength="8"
                    />
                    <button
                      class="w-full rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 sm:w-auto"
                      type="button"
                      @click="applyManualCode"
                    >
                      {{ t('common.save') }}
                    </button>
                  </div>
                  <p class="text-xs text-slate-500">{{ t('portal.qr.helper') }}</p>
                </div>
              </div>

              <div class="rounded-2xl border border-slate-200 bg-slate-50 p-4">
                <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.qr.title') }}</p>
                <div class="mt-4 flex items-center justify-center">
                  <div v-if="qrDataUrl"
                    class="flex items-center justify-center rounded-2xl border border-slate-200 bg-white p-4">
                    <img :src="qrDataUrl" :alt="t('portal.qr.imageAlt')" class="h-40 w-40" />
                  </div>
                  <div v-else class="text-sm text-slate-500">
                    {{ t('portal.qr.empty') }}
                  </div>
                </div>
                <div class="mt-4 flex flex-col items-start gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <button
                    class="w-full rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                    type="button"
                    @click="copyCheckInLink"
                  >
                    {{ t('portal.qr.copyGuideLink') }}
                  </button>
                  <p v-if="linkCopyStatusKey" class="text-xs text-slate-500">{{ t(linkCopyStatusKey) }}</p>
                </div>
              </div>
            </div>
          </section>

          <section v-if="activeTab === 'info'" class="space-y-4">
            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.info.meetingTitle') }}</h2>
              <div class="mt-4 grid gap-3 text-sm text-slate-700 sm:grid-cols-2">
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.info.time') }}</p>
                  <p class="mt-1 font-medium">{{ portal.meeting.time }}</p>
                </div>
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.info.place') }}</p>
                  <p class="mt-1 font-medium">{{ portal.meeting.place }}</p>
                </div>
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3 sm:col-span-2">
                  <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.info.maps') }}</p>
                  <a class="mt-2 inline-flex w-full items-center justify-center rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                    :href="portal.meeting.mapsUrl" rel="noreferrer" target="_blank">
                    {{ t('portal.info.openMaps') }}
                  </a>
                </div>
                <div class="rounded-xl border border-slate-200 bg-slate-50 p-3 sm:col-span-2">
                  <p class="text-xs uppercase tracking-wide text-slate-400">{{ t('portal.info.note') }}</p>
                  <p class="mt-2 text-sm text-slate-600">{{ portal.meeting.note }}</p>
                </div>
              </div>
            </div>

            <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
              <h2 class="text-lg font-semibold">{{ t('portal.info.notesTitle') }}</h2>
              <div v-if="portal.notes.length === 0"
                class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">
                {{ t('portal.info.notesEmpty') }}
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
          {{ t('portal.empty') }}
        </div>
      </template>
    </div>

    <PortalTabBar :tabs="tabs" :active="activeTab" @select="setActiveTab" />
  </div>
</template>
