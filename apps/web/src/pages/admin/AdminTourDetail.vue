<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { apiDelete, apiGet, apiPost, apiPut } from '../../lib/api'
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
const tourForm = reactive({
  name: '',
  startDate: '',
  endDate: '',
})
const loading = ref(true)
const submitting = ref(false)
const loadError = ref<string | null>(null)
const formError = ref<string | null>(null)
const tourSaving = ref(false)
const tourMessage = ref<string | null>(null)
const tourError = ref<string | null>(null)
const dateHint = ref<string | null>(null)
const portalSaving = ref(false)
const portalMessage = ref<string | null>(null)
const portalError = ref<string | null>(null)
const guideSaving = ref(false)
const guideError = ref<string | null>(null)
const guideLoading = ref(true)
const guides = ref<UserListItem[]>([])
const guideId = ref('')
const phoneError = ref<string | null>(null)
const editPhoneError = ref<string | null>(null)
const nameInput = ref<HTMLInputElement | null>(null)
const editingParticipantId = ref<string | null>(null)
const editParticipantSaving = ref(false)
const editParticipantError = ref<string | null>(null)

const { pushToast } = useToast()

const form = reactive({
  fullName: '',
  email: '',
  phone: '',
})

const editForm = reactive({
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
  links: [] as LinkInfo[],
})

const normalizeDays = (days: DayPlan[]) =>
  days.map((day, index) => ({
    day: index + 1,
    title: day.title ?? '',
    items: day.items ?? [],
  }))

const setTourForm = (data: Tour) => {
  tourForm.name = data.name
  tourForm.startDate = data.startDate
  tourForm.endDate = data.endDate
}

const setPortalForm = (data: TourPortalInfo) => {
  portalForm.meetingTime = data.meeting.time
  portalForm.meetingPlace = data.meeting.place
  portalForm.meetingMapsUrl = data.meeting.mapsUrl
  portalForm.meetingNote = data.meeting.note
  portalForm.notesText = data.notes.join('\n')

  const normalizedLinks = data.links.length > 0 ? data.links : [{ label: '', url: '' }]
  portalForm.links.splice(0, portalForm.links.length, ...normalizedLinks)
  portalDays.value = normalizeDays(data.days)
}

const addPortalLink = () => {
  portalForm.links.push({ label: '', url: '' })
}

const removePortalLink = (index: number) => {
  portalForm.links.splice(index, 1)
  if (portalForm.links.length === 0) {
    portalForm.links.push({ label: '', url: '' })
  }
}

const updateDayItems = (index: number, value: string) => {
  const items = value.split(/\r?\n/)

  portalDays.value = portalDays.value.map((day, idx) =>
    idx === index ? { ...day, items } : day
  )
}

const addDay = () => {
  const next = [...portalDays.value, { day: portalDays.value.length + 1, title: '', items: [] }]
  portalDays.value = normalizeDays(next)
}

const removeDay = (index: number) => {
  portalDays.value = normalizeDays(portalDays.value.filter((_, idx) => idx !== index))
}

const moveDay = (index: number, direction: number) => {
  const next = [...portalDays.value]
  const target = index + direction
  if (target < 0 || target >= next.length) {
    return
  }

  const current = next[index]
  const swap = next[target]
  if (!current || !swap) {
    return
  }

  next[index] = swap
  next[target] = current
  portalDays.value = normalizeDays(next)
}

const loadTour = async () => {
  loading.value = true
  loadError.value = null
  portalMessage.value = null
  portalError.value = null
  formError.value = null
  tourMessage.value = null
  tourError.value = null
  dateHint.value = null
  editingParticipantId.value = null
  editParticipantError.value = null
  editPhoneError.value = null
  guideError.value = null
  guideLoading.value = true

  try {
    const [tourData, participantsData, portalData] = await Promise.all([
      apiGet<Tour>(`/api/tours/${tourId.value}`),
      apiGet<Participant[]>(`/api/tours/${tourId.value}/participants`),
      apiGet<TourPortalInfo>(`/api/tours/${tourId.value}/portal`),
    ])

    tour.value = tourData
    setTourForm(tourData)
    participants.value = participantsData
    portal.value = portalData
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

const saveTour = async () => {
  tourMessage.value = null
  tourError.value = null

  const name = tourForm.name.trim()
  if (!name) {
    tourError.value = 'Name is required.'
    return
  }

  if (!tourForm.startDate || !tourForm.endDate) {
    tourError.value = 'Start and end dates are required.'
    return
  }

  if (tourForm.endDate < tourForm.startDate) {
    tourError.value = 'End date must be on or after start date.'
    return
  }

  tourSaving.value = true
  try {
    const updated = await apiPut<Tour>(`/api/tours/${tourId.value}`, {
      name,
      startDate: tourForm.startDate,
      endDate: tourForm.endDate,
    })
    tour.value = updated
    setTourForm(updated)
    tourMessage.value = 'Saved.'
    pushToast('Tour updated', 'success')
  } catch (err) {
    tourError.value = err instanceof Error ? err.message : 'Failed to update tour.'
    pushToast(tourError.value, 'error')
  } finally {
    tourSaving.value = false
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

const startEditParticipant = (participant: Participant) => {
  editingParticipantId.value = participant.id
  editParticipantError.value = null
  editPhoneError.value = null
  editForm.fullName = participant.fullName
  editForm.email = participant.email ?? ''
  editForm.phone = participant.phone ?? ''
}

const cancelEditParticipant = () => {
  editingParticipantId.value = null
  editParticipantError.value = null
  editPhoneError.value = null
}

const handleEditPhoneInput = () => {
  editPhoneError.value = null
  const sanitized = sanitizePhoneInput(editForm.phone)
  editForm.phone = sanitized.slice(0, 15)
}

const handleEditPhoneBlur = () => {
  const { normalized, error } = normalizePhone(editForm.phone)
  if (error) {
    editPhoneError.value = error
    return
  }

  editPhoneError.value = null
  editForm.phone = normalized
}

const saveParticipant = async (participant: Participant) => {
  editParticipantError.value = null
  editPhoneError.value = null

  const fullName = normalizeName(editForm.fullName)
  if (fullName.length < 2) {
    editParticipantError.value = 'Full name is required.'
    return
  }

  const { normalized: normalizedPhone, error: phoneErr } = normalizePhone(editForm.phone)
  if (phoneErr) {
    editPhoneError.value = phoneErr
    return
  }

  editParticipantSaving.value = true
  try {
    const updated = await apiPut<Participant>(
      `/api/tours/${tourId.value}/participants/${participant.id}`,
      {
        fullName,
        email: normalizeEmail(editForm.email) || undefined,
        phone: normalizedPhone || undefined,
      }
    )

    participants.value = participants.value.map((item) =>
      item.id === participant.id ? updated : item
    )
    editingParticipantId.value = null
    pushToast('Participant updated', 'success')
  } catch (err) {
    editParticipantError.value =
      err instanceof Error ? err.message : 'Failed to update participant.'
    pushToast(editParticipantError.value, 'error')
  } finally {
    editParticipantSaving.value = false
  }
}

const deleteParticipant = async (participant: Participant) => {
  const confirmed = globalThis.confirm?.(`Delete ${participant.fullName}?`)
  if (!confirmed) {
    return
  }

  try {
    await apiDelete(`/api/tours/${tourId.value}/participants/${participant.id}`)
    participants.value = participants.value.filter((item) => item.id !== participant.id)
    pushToast('Participant removed', 'success')
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Failed to delete participant.'
    pushToast(message, 'error')
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

  const days = portalDays.value
    .map((day, index) => {
      const title = day.title.trim()
      const items = day.items.map((item) => item.trim()).filter(Boolean)
      if (!title && items.length === 0) {
        return null
      }
      return {
        day: index + 1,
        title: title || `Day ${index + 1}`,
        items,
      }
    })
    .filter((day): day is DayPlan => day !== null)

  const payload: TourPortalInfo = {
    meeting: {
      time: meetingTime,
      place: meetingPlace,
      mapsUrl: meetingMapsUrl,
      note: meetingNote,
    },
    links,
    days,
    notes,
  }

  portalSaving.value = true
  try {
    const saved = await apiPut<TourPortalInfo>(`/api/tours/${tourId.value}/portal`, payload)
    portal.value = saved
    portalDays.value = normalizeDays(saved.days)
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

watch(
  () => tourForm.startDate,
  (value) => {
    if (!value || !tourForm.endDate) {
      dateHint.value = null
      return
    }

    if (tourForm.endDate < value) {
      tourForm.endDate = value
      dateHint.value = 'End date was adjusted to match start date.'
      return
    }

    dateHint.value = null
  }
)

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
        <div class="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 class="text-lg font-semibold">Tour Details</h2>
            <p class="text-sm text-slate-500">Update the tour name and dates.</p>
          </div>
        </div>

        <form class="mt-4 grid gap-4 md:grid-cols-3" @submit.prevent="saveTour">
          <label class="grid gap-1 text-sm md:col-span-1">
            <span class="text-slate-600">Name</span>
            <input
              v-model.trim="tourForm.name"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              placeholder="Tour name"
              type="text"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">Start date</span>
            <input
              v-model="tourForm.startDate"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="date"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">End date</span>
            <input
              v-model="tourForm.endDate"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :min="tourForm.startDate"
              type="date"
            />
          </label>
          <div class="md:col-span-3 flex flex-wrap items-center gap-3">
            <button
              class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
              :disabled="tourSaving"
              type="submit"
            >
              {{ tourSaving ? 'Saving...' : 'Save tour' }}
            </button>
            <span v-if="tourMessage" class="text-xs text-emerald-600">{{ tourMessage }}</span>
            <span v-if="tourError" class="text-xs text-rose-600">{{ tourError }}</span>
            <span v-if="dateHint" class="text-xs text-slate-500">{{ dateHint }}</span>
          </div>
        </form>
      </section>

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
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-semibold text-slate-700">Links</h3>
              <button
                class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
                type="button"
                @click="addPortalLink"
              >
                Add link
              </button>
            </div>
            <div class="space-y-3">
              <div
                v-for="(link, index) in portalForm.links"
                :key="index"
                class="grid gap-3 md:grid-cols-[1fr_1fr_auto]"
              >
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
                <div class="flex items-end">
                  <button
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:border-slate-300"
                    type="button"
                    @click="removePortalLink(index)"
                  >
                    Remove
                  </button>
                </div>
              </div>
            </div>
          </div>

          <div class="space-y-3">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-semibold text-slate-700">Days</h3>
              <button
                class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
                type="button"
                @click="addDay"
              >
                Add day
              </button>
            </div>
            <div
              v-if="portalDays.length === 0"
              class="rounded border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500"
            >
              No days yet. Add the first day.
            </div>
            <div v-else class="space-y-4">
              <div
                v-for="(day, index) in portalDays"
                :key="day.day"
                class="rounded-lg border border-slate-200 bg-slate-50 p-4"
              >
                <div class="flex flex-wrap items-center justify-between gap-2">
                  <div class="text-sm font-semibold text-slate-700">Day {{ index + 1 }}</div>
                  <div class="flex flex-wrap gap-2">
                    <button
                      class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                      :disabled="index === 0"
                      type="button"
                      @click="moveDay(index, -1)"
                    >
                      Up
                    </button>
                    <button
                      class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                      :disabled="index === portalDays.length - 1"
                      type="button"
                      @click="moveDay(index, 1)"
                    >
                      Down
                    </button>
                    <button
                      class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                      type="button"
                      @click="removeDay(index)"
                    >
                      Remove
                    </button>
                  </div>
                </div>
                <div class="mt-3 grid gap-3 md:grid-cols-2">
                  <label class="grid gap-1 text-sm md:col-span-2">
                    <span class="text-slate-600">Title</span>
                    <input
                      v-model.trim="day.title"
                      class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                      placeholder="Day title"
                      type="text"
                    />
                  </label>
                  <label class="grid gap-1 text-sm md:col-span-2">
                    <span class="text-slate-600">Items (one per line)</span>
                    <textarea
                      class="min-h-24 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                      :value="day.items.join('\n')"
                      placeholder="Visit museum"
                      @input="updateDayItems(index, ($event.target as HTMLTextAreaElement).value)"
                    ></textarea>
                  </label>
                </div>
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
            <div v-if="editingParticipantId === participant.id" class="space-y-3">
              <div class="text-sm font-semibold text-slate-700">Edit participant</div>
              <div class="grid gap-3 md:grid-cols-3">
                <label class="grid gap-1 text-sm md:col-span-1">
                  <span class="text-slate-600">Full name</span>
                  <input
                    v-model.trim="editForm.fullName"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    type="text"
                  />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">Email</span>
                  <input
                    v-model.trim="editForm.email"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    type="email"
                  />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">Phone</span>
                  <input
                    v-model.trim="editForm.phone"
                    class="rounded border bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    :class="editPhoneError ? 'border-rose-300' : 'border-slate-200'"
                    placeholder="+90 555 123 45 67"
                    inputmode="tel"
                    maxlength="15"
                    type="tel"
                    @input="handleEditPhoneInput"
                    @blur="handleEditPhoneBlur"
                  />
                </label>
              </div>
              <div class="flex flex-wrap items-center gap-2">
                <button
                  class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
                  :disabled="editParticipantSaving"
                  type="button"
                  @click="saveParticipant(participant)"
                >
                  {{ editParticipantSaving ? 'Saving...' : 'Save' }}
                </button>
                <button
                  class="rounded border border-slate-200 bg-white px-4 py-2 text-sm text-slate-700 hover:border-slate-300"
                  type="button"
                  @click="cancelEditParticipant"
                >
                  Cancel
                </button>
                <span v-if="editPhoneError" class="text-xs text-rose-600">{{ editPhoneError }}</span>
                <span v-if="editParticipantError" class="text-xs text-rose-600">{{ editParticipantError }}</span>
              </div>
            </div>

            <div v-else class="space-y-2">
              <div class="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <div class="font-medium">{{ participant.fullName }}</div>
                  <div class="mt-1 text-xs text-slate-500" v-if="participant.email || participant.phone">
                    <span v-if="participant.email">{{ participant.email }}</span>
                    <span v-if="participant.email && participant.phone"> | </span>
                    <span v-if="participant.phone">{{ formatPhoneDisplay(participant.phone) }}</span>
                  </div>
                </div>
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
              </div>
              <div class="flex flex-wrap items-center gap-2 text-xs text-slate-500">
                <span class="font-mono">{{ participant.checkInCode }}</span>
              </div>
              <div class="flex flex-wrap items-center gap-2">
                <button
                  class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
                  type="button"
                  @click="startEditParticipant(participant)"
                >
                  Edit
                </button>
                <button
                  class="rounded border border-rose-200 bg-rose-50 px-3 py-1.5 text-xs font-medium text-rose-700 hover:border-rose-300"
                  type="button"
                  @click="deleteParticipant(participant)"
                >
                  Delete
                </button>
              </div>
            </div>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>
