<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter, type LocationQueryRaw } from 'vue-router'
import { apiGet, apiPost } from '../../lib/api'
import { formatPhoneDisplay, normalizeCheckInCode } from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { CheckInResponse, CheckInSummary, CheckInUndoResponse, Participant, Tour } from '../../types'

const route = useRoute()
const router = useRouter()
const tourId = computed(() => route.params.tourId as string)

const tour = ref<Tour | null>(null)
const participants = ref<Participant[]>([])
const summary = ref<CheckInSummary>({ arrivedCount: 0, totalCount: 0 })
const loading = ref(true)
const error = ref<string | null>(null)

const searchTerm = ref('')
const checkInCode = ref('')
const actionMessage = ref<string | null>(null)
const actionError = ref<string | null>(null)
const isCheckingIn = ref(false)
const dataLoaded = ref(false)
const lastAutoCode = ref('')
const lastResult = ref<CheckInResponse | null>(null)
const codeInput = ref<HTMLInputElement | null>(null)
const undoingParticipantId = ref<string | null>(null)

const { pushToast } = useToast()

const filteredParticipants = computed(() => {
  const query = searchTerm.value.trim().toLowerCase()
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
  error.value = null

  try {
    const [tourData, participantsData, summaryData] = await Promise.all([
      apiGet<Tour>(`/api/tours/${tourId.value}`),
      apiGet<Participant[]>(`/api/tours/${tourId.value}/participants`),
      apiGet<CheckInSummary>(`/api/tours/${tourId.value}/checkins/summary`),
    ])

    tour.value = tourData
    participants.value = participantsData
    summary.value = summaryData
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load check-in data.'
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
      `/api/tours/${tourId.value}/checkins/undo`,
      { participantId }
    )
    updateFromUndo(response)
    const tone = response.alreadyUndone ? 'info' : 'success'
    pushToast(response.alreadyUndone ? 'Already undone' : 'Check-in undone', tone)
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Undo failed.'
    pushToast(message, 'error')
  } finally {
    undoingParticipantId.value = null
  }
}

const submitCheckIn = async (code: string) => {
  actionMessage.value = null
  actionError.value = null
  lastResult.value = null

  const normalized = code.trim().toUpperCase()
  if (!normalized) {
    actionError.value = 'Check-in code is required.'
    pushToast('Invalid code / not found', 'error')
    return false
  }

  isCheckingIn.value = true
  try {
    const response = await apiPost<CheckInResponse>(`/api/tours/${tourId.value}/checkins`, {
      checkInCode: normalized,
    })

    updateFromCheckIn(response)
    actionMessage.value = response.alreadyArrived ? 'Already checked in.' : 'Check-in successful.'
    if (response.alreadyArrived) {
      pushToast('Already arrived', 'info')
    } else {
      pushToast('Checked in', 'success', {
        timeout: 10000,
        action: {
          label: 'Undo',
          onClick: () => {
            void undoCheckIn(response.participantId)
          },
        },
      })
    }
    checkInCode.value = ''
    lastResult.value = response
    return true
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Check-in failed.'
    actionError.value = message
    pushToast('Invalid code / not found', 'error')
    return false
  } finally {
    isCheckingIn.value = false
  }
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
  actionMessage.value = null
  actionError.value = null

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    actionError.value = 'Copy not supported.'
    return
  }

  try {
    await clipboard.writeText(code)
    actionMessage.value = 'Code copied.'
  } catch {
    actionError.value = 'Copy failed.'
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
  const queryCode = typeof raw === 'string' ? raw.trim().toUpperCase() : ''
  if (!queryCode) {
    return
  }

  checkInCode.value = queryCode

  if (lastAutoCode.value === queryCode) {
    return
  }

  lastAutoCode.value = queryCode
  const success = await submitCheckIn(queryCode)
  if (success) {
    await clearCheckInQuery()
  }
}

const initialize = async () => {
  await loadData()
  await resolveQueryCode()
  await nextTick()
  codeInput.value?.focus()
}

watch(
  () => [route.query.code, route.query.checkInCode, dataLoaded.value],
  () => {
    void resolveQueryCode()
  }
)

onMounted(() => {
  void initialize()
})
</script>

<template>
  <div class="space-y-6 sm:space-y-8">
    <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="flex items-start justify-between gap-4">
        <div>
          <RouterLink class="text-sm text-slate-600 underline" :to="`/admin/tours/${tourId}`">
            Back to tour
          </RouterLink>
          <h1 class="mt-2 text-2xl font-semibold">{{ tour?.name ?? 'Tour check-in' }}</h1>
          <p class="text-sm text-slate-500" v-if="tour">{{ tour.startDate }} to {{ tour.endDate }}</p>
        </div>
        <div class="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm">
          <div class="text-xs uppercase tracking-wide text-slate-400">Arrived</div>
          <div class="mt-1 text-xl font-semibold text-slate-800">
            {{ summary.arrivedCount }} / {{ summary.totalCount }}
          </div>
        </div>
      </div>
    </div>

    <LoadingState v-if="loading && !dataLoaded" message="Loading check-in data..." />
    <ErrorState v-else-if="error && !dataLoaded" :message="error" @retry="initialize" />

    <template v-else>
      <ErrorState v-if="error" :message="error" @retry="initialize" />
      <div v-if="!hasData" class="text-sm text-slate-500">No data available.</div>
      <template v-else>
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="text-lg font-semibold">Check-in</h2>
        <form class="mt-4 grid gap-3 sm:grid-cols-[1fr_auto]" @submit.prevent="handleCheckInSubmit">
          <input
            v-model.trim="checkInCode"
            ref="codeInput"
            autofocus
            class="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm uppercase tracking-wide focus:border-slate-400 focus:outline-none"
            placeholder="8-character code"
            type="text"
            maxlength="8"
            @input="handleCodeInput"
          />
          <button
            class="w-full rounded-xl bg-slate-900 px-4 py-3 text-sm font-medium text-white hover:bg-slate-800 sm:w-auto"
            :disabled="isCheckingIn"
            type="submit"
          >
            {{ isCheckingIn ? 'Checking in...' : 'Check-in' }}
          </button>
        </form>
        <div class="mt-3 flex flex-wrap items-center gap-3 text-xs">
          <span v-if="actionMessage" class="text-emerald-600">{{ actionMessage }}</span>
          <span v-if="actionError" class="text-rose-600">{{ actionError }}</span>
        </div>
        <div v-if="lastResult" class="mt-4 rounded-2xl border border-emerald-100 bg-emerald-50 p-4 text-sm">
          <div class="font-semibold text-emerald-800">
            {{ lastResult.alreadyArrived ? 'Already arrived' : 'Checked in' }}
          </div>
          <div class="mt-1 text-emerald-700">{{ lastResult.participantName }}</div>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex items-center justify-between">
          <h2 class="text-lg font-semibold">Participants</h2>
          <span class="text-xs text-slate-500">{{ filteredParticipants.length }} shown</span>
        </div>

        <div class="mt-4">
          <input
            v-model.trim="searchTerm"
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm focus:border-slate-400 focus:outline-none"
            placeholder="Search by name, email, or phone"
            type="text"
          />
        </div>

        <div
          v-if="filteredParticipants.length === 0"
          class="mt-4 rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500"
        >
          No participants found.
        </div>

        <ul v-else class="mt-4 space-y-3">
          <li
            v-for="participant in filteredParticipants"
            :key="participant.id"
            class="rounded-2xl border border-slate-200 bg-slate-50 p-4"
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
                    {{ participant.arrived ? 'Arrived' : 'Pending' }}
                  </span>
                  <button
                    v-if="!participant.arrived"
                    class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300"
                    type="button"
                    @click="markArrived(participant)"
                  >
                    Mark arrived
                  </button>
                  <button
                    v-else
                    class="rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300"
                    :disabled="undoingParticipantId === participant.id"
                    type="button"
                    @click="undoCheckIn(participant.id)"
                  >
                    {{ undoingParticipantId === participant.id ? 'Undoing...' : 'Undo' }}
                  </button>
                </div>
              </div>

              <div class="flex flex-col items-start gap-2 sm:items-end">
                <div class="text-xs uppercase tracking-wide text-slate-400">Check-in code</div>
                <div class="flex w-full items-center gap-2 sm:w-auto">
                  <span class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm font-mono text-slate-700">
                    {{ participant.checkInCode }}
                  </span>
                  <button
                    class="rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:border-slate-300"
                    type="button"
                    @click="copyCode(participant.checkInCode)"
                  >
                    Copy
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
</template>
