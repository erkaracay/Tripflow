<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { apiGet, apiPost, apiPut } from '../../lib/api'
import type { DayPlan, LinkInfo, Participant, Tour, TourPortalInfo } from '../../types'

const route = useRoute()
const tourId = computed(() => route.params.tourId as string)

const tour = ref<Tour | null>(null)
const participants = ref<Participant[]>([])
const portal = ref<TourPortalInfo | null>(null)
const portalDays = ref<DayPlan[]>([])
const loading = ref(true)
const submitting = ref(false)
const error = ref<string | null>(null)
const portalSaving = ref(false)
const portalMessage = ref<string | null>(null)
const portalError = ref<string | null>(null)

const form = reactive({
  fullName: '',
  email: '',
  phone: '',
})

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
  error.value = null
  portalMessage.value = null
  portalError.value = null

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
    setPortalForm(portalData)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load tour.'
  } finally {
    loading.value = false
  }
}

const addParticipant = async () => {
  error.value = null

  if (!form.fullName.trim()) {
    error.value = 'Full name is required.'
    return
  }

  submitting.value = true
  try {
    const created = await apiPost<Participant>(`/api/tours/${tourId.value}/participants`, {
      fullName: form.fullName,
      email: form.email || undefined,
      phone: form.phone || undefined,
    })

    participants.value = [...participants.value, created]
    form.fullName = ''
    form.email = ''
    form.phone = ''
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to add participant.'
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
  } catch (err) {
    portalError.value = err instanceof Error ? err.message : 'Failed to save portal.'
  } finally {
    portalSaving.value = false
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

    <p v-if="error" class="text-sm text-rose-600">{{ error }}</p>

    <div v-if="loading" class="rounded border border-dashed border-slate-200 bg-white p-6 text-sm text-slate-500">
      Loading tour details...
    </div>

    <template v-else>
      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h2 class="text-lg font-semibold">Add participant</h2>
        <form class="mt-4 grid gap-4 md:grid-cols-3" @submit.prevent="addParticipant">
          <label class="grid gap-1 text-sm md:col-span-1">
            <span class="text-slate-600">Full name</span>
            <input
              v-model.trim="form.fullName"
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
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              placeholder="+90 555 123 45 67"
              type="tel"
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
              <span v-if="participant.phone">{{ participant.phone }}</span>
            </div>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>
