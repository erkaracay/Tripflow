<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter, type LocationQueryRaw } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPost } from '../../lib/api'
import { formatPhoneDisplay, normalizeCheckInCode } from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import QrScannerModal from '../../components/QrScannerModal.vue'
import type {
  CheckInResponse,
  CheckInSummary,
  CheckInUndoResponse,
  Participant,
  ParticipantResolve,
  Tour,
} from '../../types'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const tourId = computed(() => route.params.tourId as string)

const tour = ref<Tour | null>(null)
const participants = ref<Participant[]>([])
const summary = ref<CheckInSummary>({ arrivedCount: 0, totalCount: 0 })
const loading = ref(true)
const errorKey = ref<string | null>(null)
const errorMessage = ref<string | null>(null)

const searchTerm = ref('')
const debouncedSearchTerm = ref('')
const checkInCode = ref('')
const actionMessageKey = ref<string | null>(null)
const actionMessageText = ref<string | null>(null)
const actionErrorKey = ref<string | null>(null)
const actionErrorText = ref<string | null>(null)
const isCheckingIn = ref(false)
const dataLoaded = ref(false)
const lastAutoCode = ref('')
const lastResult = ref<CheckInResponse | null>(null)
const codeInput = ref<HTMLInputElement | null>(null)
const autoCheckInAfterScan = ref(false)
const autoCheckInStorageKey = 'tripflow:guide:autoCheckInAfterScan'
const scannerOpen = ref(false)
const scanErrorKey = ref<string | null>(null)
const scanErrorText = ref<string | null>(null)
const scanFoundCode = ref<string | null>(null)
const scanFoundParticipant = ref<ParticipantResolve | null>(null)
const scanResolving = ref(false)
const highlightParticipantId = ref<string | null>(null)
const undoingParticipantId = ref<string | null>(null)
const lastAction = ref<{ participantId: string; participantName: string } | null>(null)
let highlightTimer: number | undefined
let lastActionTimer: number | undefined
let searchDebounceTimer: number | undefined

const { pushToast } = useToast()

const filteredParticipants = computed(() => {
  const query = debouncedSearchTerm.value.trim().toLowerCase()
  if (!query) {
    return participants.value
  }

  return participants.value.filter((participant) => {
    const haystack = [participant.fullName, participant.email, participant.phone]
      .filter(Boolean)
      .join(' ')
      .toLowerCase()
    return haystack.includes(query)
  })
})

const hasData = computed(() => Boolean(tour.value))

const loadData = async () => {
  loading.value = true
  errorKey.value = null
  errorMessage.value = null

  try {
    const [tourData, participantsData, summaryData] = await Promise.all([
      apiGet<Tour>(`/api/tours/${tourId.value}`),
      apiGet<Participant[]>(`/api/guide/tours/${tourId.value}/participants`),
      apiGet<CheckInSummary>(`/api/guide/tours/${tourId.value}/checkins/summary`),
    ])

    tour.value = tourData
    participants.value = participantsData
    summary.value = summaryData
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : null
    if (!errorMessage.value) {
      errorKey.value = 'errors.checkIn.load'
    }
  } finally {
    loading.value = false
    dataLoaded.value = true
  }
}

const updateFromCheckIn = (response: CheckInResponse) => {
  summary.value = {
    arrivedCount: response.arrivedCount,
    totalCount: response.totalCount,
  }

  participants.value = participants.value.map((participant) =>
    participant.id === response.participantId
      ? { ...participant, arrived: true }
      : participant
  )
}

const setLastAction = (response: CheckInResponse) => {
  if (response.alreadyArrived) {
    return
  }

  lastAction.value = {
    participantId: response.participantId,
    participantName: response.participantName,
  }

  if (lastActionTimer) {
    globalThis.clearTimeout(lastActionTimer)
  }

  lastActionTimer = globalThis.setTimeout(() => {
    lastAction.value = null
    lastActionTimer = undefined
  }, 10000)
}

const updateFromUndo = (response: CheckInUndoResponse) => {
  summary.value = {
    arrivedCount: response.arrivedCount,
    totalCount: response.totalCount,
  }

  participants.value = participants.value.map((participant) =>
    participant.id === response.participantId
      ? { ...participant, arrived: false }
      : participant
  )
}

const undoCheckIn = async (participantId: string) => {
  undoingParticipantId.value = participantId
  try {
    const response = await apiPost<CheckInUndoResponse>(
      `/api/guide/tours/${tourId.value}/checkins/undo`,
      { participantId }
    )
    updateFromUndo(response)
    if (lastAction.value?.participantId === participantId) {
      lastAction.value = null
    }
    const tone = response.alreadyUndone ? 'info' : 'success'
    pushToast({
      key: response.alreadyUndone ? 'toast.alreadyUndone' : 'toast.checkInUndone',
      tone,
    })
  } catch (err) {
    pushToast({ key: 'toast.undoFailed', tone: 'error' })
  } finally {
    undoingParticipantId.value = null
  }
}

const submitCheckIn = async (code: string) => {
  actionMessageKey.value = null
  actionMessageText.value = null
  actionErrorKey.value = null
  actionErrorText.value = null
  lastResult.value = null

  const normalized = normalizeCheckInCode(code).slice(0, 8)
  if (!normalized) {
    actionErrorKey.value = 'validation.checkInCodeRequired'
    pushToast({ key: 'toast.invalidCode', tone: 'error' })
    return false
  }

  isCheckingIn.value = true
  try {
    const response = await apiPost<CheckInResponse>(`/api/guide/tours/${tourId.value}/checkins`, {
      checkInCode: normalized,
    })

    updateFromCheckIn(response)
    actionMessageKey.value = response.alreadyArrived
      ? 'guide.checkIn.alreadyCheckedIn'
      : 'guide.checkIn.success'
    if (response.alreadyArrived) {
      pushToast({ key: 'toast.alreadyArrived', tone: 'info' })
    } else {
      pushToast({
        key: 'toast.checkedIn',
        tone: 'success',
        timeout: 10000,
        action: {
          labelKey: 'common.undo',
          onClick: () => {
            void undoCheckIn(response.participantId)
          },
        },
      })
    }
    checkInCode.value = ''
    lastResult.value = response
    setLastAction(response)
    highlightParticipantId.value = response.participantId
    if (highlightTimer) {
      globalThis.clearTimeout(highlightTimer)
    }
    highlightTimer = globalThis.setTimeout(() => {
      highlightParticipantId.value = null
    }, 2000)
    await nextTick()
    codeInput.value?.focus()
    return true
  } catch (err) {
    actionErrorText.value = err instanceof Error ? err.message : null
    if (!actionErrorText.value) {
      actionErrorKey.value = 'errors.checkIn.failed'
    }
    pushToast({ key: 'toast.invalidCode', tone: 'error' })
    return false
  } finally {
    isCheckingIn.value = false
  }
}

const clearSearch = () => {
  searchTerm.value = ''
}

const normalizeQrCode = (raw: string) => {
  const trimmed = raw.trim()
  if (!trimmed) {
    return ''
  }

  let candidate = trimmed
  try {
    const url = new URL(trimmed)
    candidate = url.searchParams.get('code') ?? url.searchParams.get('checkInCode') ?? trimmed
  } catch {
    // Not a URL; keep raw string.
  }

  const normalized = candidate.toUpperCase().replace(/[^A-Z0-9]/g, '')
  return normalized.length === 8 ? normalized : ''
}

const resolveParticipantByCode = async (code: string) => {
  scanResolving.value = true
  try {
    const participant = await apiGet<ParticipantResolve>(
      `/api/guide/tours/${tourId.value}/participants/resolve?code=${encodeURIComponent(code)}`
    )
    scanFoundParticipant.value = participant
    return true
  } catch (err) {
    scanFoundParticipant.value = null
    return false
  } finally {
    scanResolving.value = false
  }
}

const resetScanState = () => {
  scanErrorKey.value = null
  scanErrorText.value = null
  scanFoundCode.value = null
  scanFoundParticipant.value = null
}

const handleScanCheckIn = async () => {
  if (!scanFoundCode.value) {
    return
  }

  const success = await submitCheckIn(scanFoundCode.value)
  if (success) {
    resetScanState()
  }
}

const handleScanResult = async (raw: string) => {
  scannerOpen.value = false
  resetScanState()

  const code = normalizeQrCode(raw)
  if (!code) {
    scanErrorKey.value = 'guide.checkIn.invalidCode'
    return
  }

  scanFoundCode.value = code
  const resolved = await resolveParticipantByCode(code)
  if (!resolved) {
    scanFoundCode.value = null
    scanErrorKey.value = 'guide.checkIn.invalidCode'
    return
  }

  if (autoCheckInAfterScan.value) {
    const success = await submitCheckIn(code)
    if (success) {
      resetScanState()
    }
  }
}

const openScanner = () => {
  resetScanState()
  scannerOpen.value = true
}

const scanAgain = () => {
  resetScanState()
  scannerOpen.value = true
}

const focusManualCode = async () => {
  await nextTick()
  codeInput.value?.focus()
}

const handleCheckInSubmit = async () => {
  await submitCheckIn(checkInCode.value)
}

const handleCodeInput = (event: Event) => {
  const target = event.target as HTMLInputElement
  checkInCode.value = normalizeCheckInCode(target.value).slice(0, 8)
}

const markArrived = async (participant: Participant) => {
  if (participant.arrived) {
    return
  }

  await submitCheckIn(participant.checkInCode)
}

const copyCode = async (code: string) => {
  actionMessageKey.value = null
  actionMessageText.value = null
  actionErrorKey.value = null
  actionErrorText.value = null

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    actionErrorKey.value = 'errors.copyNotSupported'
    return
  }

  try {
    await clipboard.writeText(code)
    actionMessageKey.value = 'common.copySuccess'
  } catch {
    actionErrorKey.value = 'errors.copyFailed'
  }
}

const clearCheckInQuery = async () => {
  const nextQuery: LocationQueryRaw = { ...route.query }
  delete nextQuery.code
  delete nextQuery.checkInCode
  await router.replace({ query: nextQuery })
}

const resolveQueryCode = async () => {
  if (!dataLoaded.value || isCheckingIn.value) {
    return
  }

  const raw = route.query.code ?? route.query.checkInCode
  const queryCode = typeof raw === 'string' ? normalizeCheckInCode(raw).slice(0, 8) : ''
  if (!queryCode) {
    return
  }

  checkInCode.value = queryCode

  if (lastAutoCode.value === queryCode) {
    return
  }

  lastAutoCode.value = queryCode
  if (autoCheckInAfterScan.value) {
    const success = await submitCheckIn(queryCode)
    if (success) {
      await clearCheckInQuery()
    }
  }
}

const initialize = async () => {
  await loadData()
  await resolveQueryCode()
  await nextTick()
  codeInput.value?.focus()
}

const loadAutoCheckInPreference = () => {
  const stored = globalThis.localStorage?.getItem(autoCheckInStorageKey)
  if (stored === '1') {
    autoCheckInAfterScan.value = true
  } else if (stored === '0') {
    autoCheckInAfterScan.value = false
  }
}

watch(searchTerm, (value) => {
  if (searchDebounceTimer) {
    globalThis.clearTimeout(searchDebounceTimer)
  }
  searchDebounceTimer = globalThis.setTimeout(() => {
    debouncedSearchTerm.value = value
  }, 250)
}, { immediate: true })

watch(
  () => [route.query.code, route.query.checkInCode, dataLoaded.value],
  () => {
    void resolveQueryCode()
  }
)

watch(autoCheckInAfterScan, (value) => {
  globalThis.localStorage?.setItem(autoCheckInStorageKey, value ? '1' : '0')
})

onMounted(() => {
  loadAutoCheckInPreference()
  void initialize()
})

onUnmounted(() => {
  if (highlightTimer) {
    globalThis.clearTimeout(highlightTimer)
  }
  if (lastActionTimer) {
    globalThis.clearTimeout(lastActionTimer)
  }
  if (searchDebounceTimer) {
    globalThis.clearTimeout(searchDebounceTimer)
  }
})
</script>

<template>
  <div class="space-y-6 sm:space-y-8">
    <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="flex items-start justify-between gap-4">
        <div>
          <RouterLink class="text-sm text-slate-600 underline" to="/guide/tours">
            {{ t('nav.backToGuideTours') }}
          </RouterLink>
          <h1 class="mt-2 text-2xl font-semibold">{{ tour?.name ?? t('guide.checkIn.title') }}</h1>
          <p class="text-sm text-slate-500" v-if="tour">
            {{ t('common.dateRange', { start: tour.startDate, end: tour.endDate }) }}
          </p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
          <div class="text-xs uppercase tracking-wide text-slate-400">{{ t('common.arrivedLabel') }}</div>
          <div class="mt-1 text-xl font-semibold text-slate-800">
            {{ summary.arrivedCount }} / {{ summary.totalCount }}
          </div>
        </div>
      </div>
    </div>

    <LoadingState v-if="loading && !dataLoaded" message-key="guide.checkIn.loading" />
    <ErrorState
      v-else-if="(errorKey || errorMessage) && !dataLoaded"
      :message="errorMessage ?? undefined"
      :message-key="errorKey ?? undefined"
      @retry="initialize"
    />

    <template v-else>
      <ErrorState
        v-if="errorKey || errorMessage"
        :message="errorMessage ?? undefined"
        :message-key="errorKey ?? undefined"
        @retry="initialize"
      />
      <div v-if="!hasData" class="text-sm text-slate-500">{{ t('common.noData') }}</div>
      <template v-else>
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="text-lg font-semibold">{{ t('common.checkIn') }}</h2>
        <form class="mt-4 grid gap-3 sm:grid-cols-[1fr_auto]" @submit.prevent="handleCheckInSubmit">
          <input
            v-model.trim="checkInCode"
            ref="codeInput"
            autofocus
            class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm uppercase tracking-wide focus:border-slate-400 focus:outline-none"
            :placeholder="t('guide.checkIn.codePlaceholder')"
            type="text"
            maxlength="8"
            @input="handleCodeInput"
          />
          <button
            class="w-full rounded-xl bg-slate-900 px-4 py-3 text-sm font-medium text-white hover:bg-slate-800 sm:w-auto"
            :disabled="isCheckingIn"
            type="submit"
          >
            {{ isCheckingIn ? t('guide.checkIn.submitting') : t('common.checkIn') }}
          </button>
        </form>
        <div class="mt-3 flex flex-wrap items-center gap-2">
          <button
            class="rounded-xl border border-slate-200 bg-white px-4 py-2 text-xs font-medium text-slate-700 hover:border-slate-300"
            type="button"
            @click="openScanner"
          >
            {{ t('guide.checkIn.scanQr') }}
          </button>
        </div>
        <div class="mt-3 flex flex-wrap items-center gap-3 text-xs">
          <span v-if="actionMessageKey || actionMessageText" class="text-emerald-600">
            {{ actionMessageKey ? t(actionMessageKey) : actionMessageText }}
          </span>
          <span v-if="actionErrorKey || actionErrorText" class="text-rose-600">
            {{ actionErrorKey ? t(actionErrorKey) : actionErrorText }}
          </span>
        </div>
        <div
          v-if="scanErrorKey || scanErrorText"
          class="mt-4 rounded-2xl border border-rose-100 bg-rose-50 p-4 text-sm text-rose-700"
        >
          <div class="font-semibold">
            {{ scanErrorKey ? t(scanErrorKey) : scanErrorText }}
          </div>
          <div class="mt-3 flex flex-wrap gap-2">
            <button
              class="rounded-full border border-rose-200 bg-white px-3 py-1.5 text-xs font-semibold text-rose-700 hover:border-rose-300"
              type="button"
              @click="scanAgain"
            >
              {{ t('guide.checkIn.scanAgain') }}
            </button>
            <button
              class="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-300"
              type="button"
              @click="focusManualCode"
            >
              {{ t('guide.checkIn.useManualCode') }}
            </button>
          </div>
        </div>
        <div
          v-if="scanFoundCode"
          class="mt-4 rounded-2xl border border-slate-200 bg-slate-50 p-4 text-sm"
        >
          <div class="text-xs uppercase tracking-wide text-slate-400">
            {{ t('guide.checkIn.foundParticipant') }}
          </div>
          <div class="mt-2 flex flex-wrap items-center gap-2">
            <span class="font-semibold text-slate-800">
              {{ scanFoundParticipant?.fullName ?? scanFoundCode }}
            </span>
            <span v-if="scanResolving" class="text-xs text-slate-500">
              {{ t('common.loading') }}
            </span>
            <span
              v-if="scanFoundParticipant"
              class="rounded-full px-3 py-1 text-xs font-semibold"
              :class="
                scanFoundParticipant.arrived
                  ? 'bg-emerald-100 text-emerald-700'
                  : 'bg-amber-100 text-amber-700'
              "
            >
              {{ scanFoundParticipant.arrived ? t('common.arrivedLabel') : t('common.pendingLabel') }}
            </span>
          </div>
          <div class="mt-1 text-xs font-mono text-slate-500">{{ scanFoundCode }}</div>
          <div class="mt-3 flex flex-wrap gap-2">
            <button
              class="rounded-full bg-slate-900 px-3 py-1.5 text-xs font-semibold text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              type="button"
              :disabled="isCheckingIn"
              @click="handleScanCheckIn"
            >
              {{ isCheckingIn ? t('guide.checkIn.submitting') : t('guide.checkIn.checkInNow') }}
            </button>
            <button
              class="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-300"
              type="button"
              @click="scanAgain"
            >
              {{ t('guide.checkIn.scanAgain') }}
            </button>
            <button
              class="rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-300"
              type="button"
              @click="resetScanState"
            >
              {{ t('common.dismiss') }}
            </button>
          </div>
        </div>
        <label class="mt-4 inline-flex items-center gap-2 text-xs text-slate-600">
          <input v-model="autoCheckInAfterScan" type="checkbox" class="h-4 w-4 rounded border-slate-300" />
          {{ t('guide.checkIn.autoCheckInToggle') }}
        </label>
        <div v-if="lastResult" class="mt-4 rounded-2xl border border-emerald-100 bg-emerald-50 p-4 text-sm">
          <div class="font-semibold text-emerald-800">
            {{ lastResult.alreadyArrived ? t('common.arrivedAlready') : t('common.checkedIn') }}
          </div>
          <div class="mt-1 text-emerald-700">{{ lastResult.participantName }}</div>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex items-center justify-between">
          <h2 class="text-lg font-semibold">{{ t('guide.checkIn.participantsTitle') }}</h2>
          <span class="text-xs text-slate-500">
            {{ t('common.shown', { count: filteredParticipants.length }) }}
          </span>
        </div>

        <div
          v-if="lastAction"
          class="mt-4 flex flex-col gap-3 rounded-2xl border border-emerald-100 bg-emerald-50 p-4 text-sm text-emerald-900 sm:flex-row sm:items-center sm:justify-between"
        >
          <span>{{ t('guide.checkIn.lastAction', { name: lastAction.participantName }) }}</span>
          <button
            class="rounded-full border border-emerald-200 bg-white px-3 py-1.5 text-xs font-semibold text-emerald-700 hover:border-emerald-300 disabled:cursor-not-allowed disabled:opacity-60"
            type="button"
            :disabled="undoingParticipantId === lastAction.participantId"
            @click="undoCheckIn(lastAction.participantId)"
          >
            {{ undoingParticipantId === lastAction.participantId ? t('common.undoing') : t('common.undo') }}
          </button>
        </div>

        <div class="relative mt-4">
          <input
            v-model.trim="searchTerm"
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 pr-16 text-sm focus:border-slate-400 focus:outline-none"
            :placeholder="t('common.searchPlaceholder')"
            type="text"
          />
          <button
            v-if="searchTerm"
            class="absolute right-3 top-1/2 -translate-y-1/2 rounded-full border border-slate-200 bg-white px-2 py-1 text-xs font-semibold text-slate-600 hover:border-slate-300"
            type="button"
            @click="clearSearch"
          >
            {{ t('common.clearSearch') }}
          </button>
        </div>

        <div
          v-if="filteredParticipants.length === 0"
          class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500"
        >
          {{ t('guide.checkIn.empty') }}
        </div>

        <ul v-else class="mt-4 space-y-3">
          <li
            v-for="participant in filteredParticipants"
            :key="participant.id"
            class="rounded-2xl border border-slate-200 bg-slate-50 p-4 transition"
            :class="highlightParticipantId === participant.id ? 'ring-2 ring-emerald-300' : ''"
          >
            <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <div class="text-base font-medium text-slate-800">{{ participant.fullName }}</div>
                <div class="mt-1 text-xs text-slate-500" v-if="participant.email || participant.phone">
                  <span v-if="participant.email">{{ participant.email }}</span>
                  <span v-if="participant.email && participant.phone"> | </span>
                  <span v-if="participant.phone">{{ formatPhoneDisplay(participant.phone) }}</span>
                </div>
                <div class="mt-3 flex flex-wrap items-center gap-2">
                  <span
                    class="rounded-full px-3 py-1 text-xs font-semibold"
                    :class="
                      participant.arrived
                        ? 'bg-emerald-100 text-emerald-700'
                        : 'bg-amber-100 text-amber-700'
                    "
                  >
                    {{ participant.arrived ? t('common.arrivedLabel') : t('common.pendingLabel') }}
                  </span>
                  <button
                    v-if="!participant.arrived"
                    class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300"
                    type="button"
                    @click="markArrived(participant)"
                  >
                    {{ t('guide.checkIn.markArrived') }}
                  </button>
                  <button
                    v-else
                    class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300"
                    :disabled="undoingParticipantId === participant.id"
                    type="button"
                    @click="undoCheckIn(participant.id)"
                  >
                    {{ undoingParticipantId === participant.id ? t('common.undoing') : t('common.undo') }}
                  </button>
                </div>
              </div>

              <div class="flex flex-col items-start gap-2 sm:items-end">
                <div class="text-xs uppercase tracking-wide text-slate-400">{{ t('common.checkInCode') }}</div>
                <div class="flex w-full items-center gap-2 sm:w-auto">
                  <span class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-mono text-slate-700">
                    {{ participant.checkInCode }}
                  </span>
                  <button
                    class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:border-slate-300"
                    type="button"
                    @click="copyCode(participant.checkInCode)"
                  >
                    {{ t('common.copy') }}
                  </button>
                </div>
              </div>
            </div>
          </li>
        </ul>
      </section>
      </template>
    </template>
  </div>
  <QrScannerModal :open="scannerOpen" @close="scannerOpen = false" @result="handleScanResult" />
</template>
