<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { apiGet, apiPost, apiPut } from '../../lib/api'
import {
  formatPhoneDisplay,
  normalizeEmail,
  normalizeName,
  normalizePhone,
  sanitizePhoneInput,
} from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { DayPlan, LinkInfo, Participant, Tour, TourPortalInfo, UserListItem } from '../../types'

const route = useRoute()
const tourId = computed(() => route.params.tourId as string)

const tour = ref<Tour | null>(null)
const participants = ref<Participant[]>([])
const portal = ref<TourPortalInfo | null>(null)
const portalDays = ref<DayPlan[]>([])
const loading = ref(true)
const submitting = ref(false)
const loadError = ref<string | null>(null)
const formError = ref<string | null>(null)
const portalSaving = ref(false)
const portalMessage = ref<string | null>(null)
const portalError = ref<string | null>(null)
const guideSaving = ref(false)
const guideError = ref<string | null>(null)
const guideLoading = ref(true)
const guides = ref<UserListItem[]>([])
const guideId = ref('')
const phoneError = ref<string | null>(null)
const nameInput = ref<HTMLInputElement | null>(null)

const { pushToast } = useToast()

const form = reactive({
  fullName: '',
  email: '',
  phone: '',
})

const handlePhoneInput = () => {
  phoneError.value = null
  const sanitized = sanitizePhoneInput(form.phone)
  form.phone = sanitized.slice(0, 15)
}

const handlePhoneBlur = () => {
  const { normalized, error } = normalizePhone(form.phone)
  if (error) {
    phoneError.value = error
    return
  }

  phoneError.value = null
  form.phone = normalized
}

const portalForm = reactive({
  meetingTime: '',
  meetingPlace: '',
  meetingMapsUrl: '',
  meetingNote: '',
  notesText: '',
  links: [
    { label: '', url: '' },
    { label: '', url: '' },
    { label: '', url: '' },
  ] as LinkInfo[],
})

const setPortalForm = (data: TourPortalInfo) => {
  portalForm.meetingTime = data.meeting.time
  portalForm.meetingPlace = data.meeting.place
  portalForm.meetingMapsUrl = data.meeting.mapsUrl
  portalForm.meetingNote = data.meeting.note
  portalForm.notesText = data.notes.join('\n')

  const normalizedLinks = Array.from({ length: 3 }, (_, index) => {
    return data.links[index] ?? { label: '', url: '' }
  })
  portalForm.links.splice(0, portalForm.links.length, ...normalizedLinks)
}

const loadTour = async () => {
  loading.value = true
  loadError.value = null
  portalMessage.value = null
  portalError.value = null
  formError.value = null
  guideError.value = null
  guideLoading.value = true

  try {
    const [tourData, participantsData, portalData] = await Promise.all([
      apiGet<Tour>(`/api/tours/${tourId.value}`),
      apiGet<Participant[]>(`/api/tours/${tourId.value}/participants`),
      apiGet<TourPortalInfo>(`/api/tours/${tourId.value}/portal`),
    ])

    tour.value = tourData
    participants.value = participantsData
    portal.value = portalData
    portalDays.value = portalData.days
    guideId.value = tourData.guideUserId ?? ''
    setPortalForm(portalData)
  } catch (err) {
    loadError.value = err instanceof Error ? err.message : 'Failed to load tour.'
    return
  } finally {
    loading.value = false
  }

  try {
    guides.value = await apiGet<UserListItem[]>('/api/users?role=Guide')
  } catch (err) {
    guideError.value = err instanceof Error ? err.message : 'Failed to load guides.'
  } finally {
    guideLoading.value = false
  }
}

const addParticipant = async () => {
  formError.value = null
  phoneError.value = null

  const fullName = normalizeName(form.fullName)
  if (fullName.length < 2) {
    formError.value = 'Full name is required.'
    return
  }

  const { normalized: normalizedPhone, error: phoneErr } = normalizePhone(form.phone)
  if (phoneErr) {
    phoneError.value = phoneErr
    return
  }

  submitting.value = true
  try {
    const created = await apiPost<Participant>(`/api/tours/${tourId.value}/participants`, {
      fullName,
      email: normalizeEmail(form.email) || undefined,
      phone: normalizedPhone || undefined,
    })

    participants.value = [...participants.value, created]
    form.fullName = ''
    form.email = ''
    form.phone = ''
    pushToast('Participant added', 'success')
    nameInput.value?.focus()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Failed to add participant.'
    pushToast(formError.value, 'error')
  } finally {
    submitting.value = false
  }
}

const savePortal = async () => {
  portalMessage.value = null
  portalError.value = null

  const meetingTime = portalForm.meetingTime.trim()
  const meetingPlace = portalForm.meetingPlace.trim()
  const meetingMapsUrl = portalForm.meetingMapsUrl.trim()
  const meetingNote = portalForm.meetingNote.trim()

  if (!meetingTime) {
    portalError.value = 'Meeting time is required.'
    return
  }

  if (!meetingPlace) {
    portalError.value = 'Meeting place is required.'
    return
  }

  if (!meetingMapsUrl) {
    portalError.value = 'Meeting maps URL is required.'
    return
  }

  const notes = portalForm.notesText
    .split('\n')
    .map((note) => note.trim())
    .filter(Boolean)

  const links = portalForm.links
    .map((link) => ({ label: link.label.trim(), url: link.url.trim() }))
    .filter((link) => link.label || link.url)

  const payload: TourPortalInfo = {
    meeting: {
      time: meetingTime,
      place: meetingPlace,
      mapsUrl: meetingMapsUrl,
      note: meetingNote,
    },
    links,
    days: portalDays.value,
    notes,
  }

  portalSaving.value = true
  try {
    const saved = await apiPut<TourPortalInfo>(`/api/tours/${tourId.value}/portal`, payload)
    portal.value = saved
    portalDays.value = saved.days
    portalMessage.value = 'Saved.'
    pushToast('Portal Saved', 'success')
  } catch (err) {
    portalError.value = err instanceof Error ? err.message : 'Failed to save portal.'
    pushToast(portalError.value, 'error')
  } finally {
    portalSaving.value = false
  }
}

const saveGuide = async () => {
  guideError.value = null

  if (!guideId.value) {
    guideError.value = 'Select a guide.'
    return
  }

  guideSaving.value = true
  try {
    await apiPut(`/api/tours/${tourId.value}/guide`, {
      guideUserId: guideId.value,
    })
    if (tour.value) {
      tour.value = { ...tour.value, guideUserId: guideId.value }
    }
    pushToast('Guide assigned', 'success')
  } catch (err) {
    guideError.value = err instanceof Error ? err.message : 'Failed to assign guide.'
    pushToast(guideError.value, 'error')
  } finally {
    guideSaving.value = false
  }
}

onMounted(loadTour)
</script>

<template>
  <div class="space-y-8">
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div>
        <RouterLink class="text-sm text-slate-600 underline" to="/admin/tours">Back to tours</RouterLink>
        <h1 class="mt-2 text-2xl font-semibold">{{ tour?.name ?? 'Tour' }}</h1>
        <p class="text-sm text-slate-500" v-if="tour">{{ tour.startDate }} to {{ tour.endDate }}</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <RouterLink
          class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
          :to="`/admin/tours/${tourId}/checkin`"
        >
          Check-in
        </RouterLink>
        <a
          class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
          :href="`/t/${tourId}`"
          rel="noreferrer"
          target="_blank"
        >
          Open portal
        </a>
      </div>
    </div>

    <LoadingState v-if="loading" message="Loading tour details..." />
    <ErrorState v-else-if="loadError" :message="loadError" @retry="loadTour" />

    <template v-else>
      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 class="text-lg font-semibold">Guide Assignment</h2>
            <p class="text-sm text-slate-500">Assign a guide to run check-ins for this tour.</p>
          </div>
        </div>

        <div v-if="guideLoading" class="mt-4 text-sm text-slate-500">Loading guides...</div>
        <div v-else-if="guides.length === 0" class="mt-4 text-sm text-slate-500">
          No guide users found.
        </div>
        <form v-else class="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center" @submit.prevent="saveGuide">
          <select
            v-model="guideId"
            class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none sm:w-auto"
          >
            <option value="" disabled>Select a guide</option>
            <option v-for="guide in guides" :key="guide.id" :value="guide.id">
              {{ guide.fullName || guide.email }}
            </option>
          </select>
          <button
            class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
            :disabled="guideSaving"
            type="submit"
          >
            {{ guideSaving ? 'Saving...' : 'Save guide' }}
          </button>
          <span v-if="guideError" class="text-xs text-rose-600">{{ guideError }}</span>
        </form>
      </section>

      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h2 class="text-lg font-semibold">Add participant</h2>
        <form class="mt-4 grid gap-4 md:grid-cols-3" @submit.prevent="addParticipant">
          <label class="grid gap-1 text-sm md:col-span-1">
            <span class="text-slate-600">Full name</span>
            <input
              v-model.trim="form.fullName"
              ref="nameInput"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              placeholder="Ayse Kaya"
              type="text"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">Email (optional)</span>
            <input
              v-model.trim="form.email"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              placeholder="ayse@example.com"
              type="email"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">Phone (optional)</span>
            <input
              v-model.trim="form.phone"
              class="rounded border bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :class="phoneError ? 'border-rose-300' : 'border-slate-200'"
              placeholder="+90 555 123 45 67"
              inputmode="tel"
              maxlength="15"
              type="tel"
              @input="handlePhoneInput"
              @blur="handlePhoneBlur"
            />
          </label>
          <div class="md:col-span-3">
            <button
              class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
              :disabled="submitting"
              type="submit"
            >
              {{ submitting ? 'Adding...' : 'Add participant' }}
            </button>
          </div>
        </form>
        <p v-if="phoneError" class="mt-2 text-sm text-rose-600">{{ phoneError }}</p>
        <p v-if="formError" class="mt-3 text-sm text-rose-600">{{ formError }}</p>
      </section>

      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div class="flex items-center justify-between">
          <h2 class="text-lg font-semibold">Portal Content</h2>
          <span class="text-xs text-slate-500">Participant view</span>
        </div>

        <form class="mt-4 space-y-6" @submit.prevent="savePortal">
          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-slate-700">Meeting</h3>
            <div class="grid gap-4 md:grid-cols-3">
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">Time</span>
                <input
                  v-model.trim="portalForm.meetingTime"
                  class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  placeholder="08:30"
                  type="text"
                />
              </label>
              <label class="grid gap-1 text-sm md:col-span-2">
                <span class="text-slate-600">Place</span>
                <input
                  v-model.trim="portalForm.meetingPlace"
                  class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  placeholder="Hotel lobby"
                  type="text"
                />
              </label>
              <label class="grid gap-1 text-sm md:col-span-2">
                <span class="text-slate-600">Maps URL</span>
                <input
                  v-model.trim="portalForm.meetingMapsUrl"
                  class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  placeholder="https://maps.google.com/?q=..."
                  type="url"
                />
              </label>
              <label class="grid gap-1 text-sm md:col-span-3">
                <span class="text-slate-600">Note</span>
                <textarea
                  v-model.trim="portalForm.meetingNote"
                  class="min-h-22.5 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  placeholder="Arrive 15 minutes early."
                ></textarea>
              </label>
            </div>
          </div>

          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-slate-700">Links</h3>
            <div class="space-y-3">
              <div v-for="(link, index) in portalForm.links" :key="index" class="grid gap-3 md:grid-cols-2">
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">Label</span>
                  <input
                    v-model.trim="link.label"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    placeholder="Tour info pack"
                    type="text"
                  />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">URL</span>
                  <input
                    v-model.trim="link.url"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    placeholder="https://example.com"
                    type="url"
                  />
                </label>
              </div>
            </div>
          </div>

          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-slate-700">Notes</h3>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">One note per line</span>
              <textarea
                v-model="portalForm.notesText"
                class="min-h-30 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                placeholder="Bring a reusable water bottle."
              ></textarea>
            </label>
          </div>

          <div class="flex flex-wrap items-center gap-3">
            <button
              class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
              :disabled="portalSaving"
              type="submit"
            >
              {{ portalSaving ? 'Saving...' : 'Save portal' }}
            </button>
            <span v-if="portalMessage" class="text-xs text-emerald-600">{{ portalMessage }}</span>
            <span v-if="portalError" class="text-xs text-rose-600">{{ portalError }}</span>
          </div>
        </form>
      </section>

      <section class="space-y-4">
        <div class="flex items-center justify-between">
          <h2 class="text-lg font-semibold">Participants</h2>
          <span class="text-xs text-slate-500">{{ participants.length }} total</span>
        </div>

        <div
          v-if="participants.length === 0"
          class="rounded border border-dashed border-slate-200 bg-white p-6 text-sm text-slate-500"
        >
          No participants yet. Add someone above.
        </div>

        <ul v-else class="space-y-3">
          <li
            v-for="participant in participants"
            :key="participant.id"
            class="rounded-lg border border-slate-200 bg-white p-4 shadow-sm"
          >
            <div class="font-medium">{{ participant.fullName }}</div>
            <div class="mt-1 text-xs text-slate-500" v-if="participant.email || participant.phone">
              <span v-if="participant.email">{{ participant.email }}</span>
              <span v-if="participant.email && participant.phone"> | </span>
              <span v-if="participant.phone">{{ formatPhoneDisplay(participant.phone) }}</span>
            </div>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>
