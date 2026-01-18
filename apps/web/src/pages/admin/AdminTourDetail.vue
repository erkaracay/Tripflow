<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiDelete, apiGet, apiPost, apiPut } from '../../lib/api'
import { getToken, getTokenRole, isTokenExpired } from '../../lib/auth'
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
const { t } = useI18n()
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
const loadErrorKey = ref<string | null>(null)
const loadErrorMessage = ref<string | null>(null)
const formErrorKey = ref<string | null>(null)
const formErrorMessage = ref<string | null>(null)
const tourSaving = ref(false)
const tourMessageKey = ref<string | null>(null)
const tourErrorKey = ref<string | null>(null)
const tourErrorMessage = ref<string | null>(null)
const dateHintKey = ref<string | null>(null)
const portalSaving = ref(false)
const portalMessageKey = ref<string | null>(null)
const portalErrorKey = ref<string | null>(null)
const portalErrorMessage = ref<string | null>(null)
const guideSaving = ref(false)
const guideErrorKey = ref<string | null>(null)
const guideErrorMessage = ref<string | null>(null)
const guideLoading = ref(true)
const guides = ref<UserListItem[]>([])
const guideId = ref('')
const phoneErrorKey = ref<string | null>(null)
const editPhoneErrorKey = ref<string | null>(null)
const nameInput = ref<HTMLInputElement | null>(null)
const editingParticipantId = ref<string | null>(null)
const editParticipantSaving = ref(false)
const editParticipantErrorKey = ref<string | null>(null)
const editParticipantErrorMessage = ref<string | null>(null)

const { pushToast } = useToast()
const isSuperAdmin = computed(() => {
  const token = getToken()
  if (!token || isTokenExpired(token)) {
    return false
  }

  return getTokenRole(token) === 'SuperAdmin'
})

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
  phoneErrorKey.value = null
  const sanitized = sanitizePhoneInput(form.phone)
  form.phone = sanitized.slice(0, 15)
}

const handlePhoneBlur = () => {
  const { normalized, errorKey } = normalizePhone(form.phone)
  if (errorKey) {
    phoneErrorKey.value = errorKey
    return
  }

  phoneErrorKey.value = null
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
  loadErrorKey.value = null
  loadErrorMessage.value = null
  portalMessageKey.value = null
  portalErrorKey.value = null
  portalErrorMessage.value = null
  formErrorKey.value = null
  formErrorMessage.value = null
  tourMessageKey.value = null
  tourErrorKey.value = null
  tourErrorMessage.value = null
  dateHintKey.value = null
  editingParticipantId.value = null
  editParticipantErrorKey.value = null
  editParticipantErrorMessage.value = null
  editPhoneErrorKey.value = null
  guideErrorKey.value = null
  guideErrorMessage.value = null
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
    loadErrorMessage.value = err instanceof Error ? err.message : null
    if (!loadErrorMessage.value) {
      loadErrorKey.value = 'errors.tourDetail.load'
    }
    return
  } finally {
    loading.value = false
  }

  try {
    guides.value = await apiGet<UserListItem[]>('/api/users?role=Guide')
  } catch (err) {
    guideErrorMessage.value = err instanceof Error ? err.message : null
    if (!guideErrorMessage.value) {
      guideErrorKey.value = 'errors.guides.load'
    }
  } finally {
    guideLoading.value = false
  }
}

const saveTour = async () => {
  tourMessageKey.value = null
  tourErrorKey.value = null
  tourErrorMessage.value = null

  const name = tourForm.name.trim()
  if (!name) {
    tourErrorKey.value = 'validation.tourNameRequired'
    return
  }

  if (!tourForm.startDate || !tourForm.endDate) {
    tourErrorKey.value = 'validation.startEndRequired'
    return
  }

  if (tourForm.endDate < tourForm.startDate) {
    tourErrorKey.value = 'validation.endAfterStart'
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
    tourMessageKey.value = 'common.saved'
    pushToast({ key: 'toast.tourUpdated', tone: 'success' })
  } catch (err) {
    tourErrorMessage.value = err instanceof Error ? err.message : null
    if (!tourErrorMessage.value) {
      tourErrorKey.value = 'errors.tourDetail.update'
    }
    pushToast({ key: 'toast.tourUpdateFailed', tone: 'error' })
  } finally {
    tourSaving.value = false
  }
}

const addParticipant = async () => {
  formErrorKey.value = null
  formErrorMessage.value = null
  phoneErrorKey.value = null

  const fullName = normalizeName(form.fullName)
  if (fullName.length < 2) {
    formErrorKey.value = 'validation.fullNameRequired'
    return
  }

  const { normalized: normalizedPhone, errorKey } = normalizePhone(form.phone)
  if (errorKey) {
    phoneErrorKey.value = errorKey
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
    pushToast({ key: 'toast.participantAdded', tone: 'success' })
    nameInput.value?.focus()
  } catch (err) {
    formErrorMessage.value = err instanceof Error ? err.message : null
    if (!formErrorMessage.value) {
      formErrorKey.value = 'errors.participant.create'
    }
    pushToast({ key: 'toast.participantAddFailed', tone: 'error' })
  } finally {
    submitting.value = false
  }
}

const startEditParticipant = (participant: Participant) => {
  editingParticipantId.value = participant.id
  editParticipantErrorKey.value = null
  editParticipantErrorMessage.value = null
  editPhoneErrorKey.value = null
  editForm.fullName = participant.fullName
  editForm.email = participant.email ?? ''
  editForm.phone = participant.phone ?? ''
}

const cancelEditParticipant = () => {
  editingParticipantId.value = null
  editParticipantErrorKey.value = null
  editParticipantErrorMessage.value = null
  editPhoneErrorKey.value = null
}

const handleEditPhoneInput = () => {
  editPhoneErrorKey.value = null
  const sanitized = sanitizePhoneInput(editForm.phone)
  editForm.phone = sanitized.slice(0, 15)
}

const handleEditPhoneBlur = () => {
  const { normalized, errorKey } = normalizePhone(editForm.phone)
  if (errorKey) {
    editPhoneErrorKey.value = errorKey
    return
  }

  editPhoneErrorKey.value = null
  editForm.phone = normalized
}

const saveParticipant = async (participant: Participant) => {
  editParticipantErrorKey.value = null
  editParticipantErrorMessage.value = null
  editPhoneErrorKey.value = null

  const fullName = normalizeName(editForm.fullName)
  if (fullName.length < 2) {
    editParticipantErrorKey.value = 'validation.fullNameRequired'
    return
  }

  const { normalized: normalizedPhone, errorKey } = normalizePhone(editForm.phone)
  if (errorKey) {
    editPhoneErrorKey.value = errorKey
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
    pushToast({ key: 'toast.participantUpdated', tone: 'success' })
  } catch (err) {
    editParticipantErrorMessage.value = err instanceof Error ? err.message : null
    if (!editParticipantErrorMessage.value) {
      editParticipantErrorKey.value = 'errors.participant.update'
    }
    pushToast({ key: 'toast.participantUpdateFailed', tone: 'error' })
  } finally {
    editParticipantSaving.value = false
  }
}

const deleteParticipant = async (participant: Participant) => {
  const confirmed = globalThis.confirm?.(
    t('admin.participants.deleteConfirm', { name: participant.fullName })
  )
  if (!confirmed) {
    return
  }

  try {
    await apiDelete(`/api/tours/${tourId.value}/participants/${participant.id}`)
    participants.value = participants.value.filter((item) => item.id !== participant.id)
    pushToast({ key: 'toast.participantRemoved', tone: 'success' })
  } catch (err) {
    pushToast({ key: 'toast.participantDeleteFailed', tone: 'error' })
  }
}

const savePortal = async () => {
  portalMessageKey.value = null
  portalErrorKey.value = null
  portalErrorMessage.value = null

  const meetingTime = portalForm.meetingTime.trim()
  const meetingPlace = portalForm.meetingPlace.trim()
  const meetingMapsUrl = portalForm.meetingMapsUrl.trim()
  const meetingNote = portalForm.meetingNote.trim()

  if (!meetingTime) {
    portalErrorKey.value = 'validation.meetingTimeRequired'
    return
  }

  if (!meetingPlace) {
    portalErrorKey.value = 'validation.meetingPlaceRequired'
    return
  }

  if (!meetingMapsUrl) {
    portalErrorKey.value = 'validation.meetingMapsRequired'
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
    portalMessageKey.value = 'common.saved'
    pushToast({ key: 'toast.portalSaved', tone: 'success' })
  } catch (err) {
    portalErrorMessage.value = err instanceof Error ? err.message : null
    if (!portalErrorMessage.value) {
      portalErrorKey.value = 'errors.portal.save'
    }
    pushToast({ key: 'toast.portalSaveFailed', tone: 'error' })
  } finally {
    portalSaving.value = false
  }
}

const saveGuide = async () => {
  guideErrorKey.value = null
  guideErrorMessage.value = null

  if (!guideId.value) {
    guideErrorKey.value = 'validation.guideRequired'
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
    pushToast({ key: 'toast.guideAssigned', tone: 'success' })
  } catch (err) {
    guideErrorMessage.value = err instanceof Error ? err.message : null
    if (!guideErrorMessage.value) {
      guideErrorKey.value = 'errors.guide.assign'
    }
    pushToast({ key: 'toast.guideAssignFailed', tone: 'error' })
  } finally {
    guideSaving.value = false
  }
}

watch(
  () => tourForm.startDate,
  (value) => {
    if (!value || !tourForm.endDate) {
      dateHintKey.value = null
      return
    }

    if (tourForm.endDate < value) {
      tourForm.endDate = value
      dateHintKey.value = 'validation.endDateAdjusted'
      return
    }

    dateHintKey.value = null
  }
)

onMounted(loadTour)
</script>

<template>
  <div class="space-y-8">
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div>
        <div class="flex flex-wrap items-center gap-2">
          <RouterLink
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
            to="/admin/tours"
          >
            {{ t('nav.backToTours') }}
          </RouterLink>
          <RouterLink
            v-if="isSuperAdmin"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
            to="/admin/orgs"
          >
            {{ t('nav.backToOrganizations') }}
          </RouterLink>
        </div>
        <h1 class="mt-2 text-2xl font-semibold">{{ tour?.name ?? t('common.tour') }}</h1>
        <p class="text-sm text-slate-500" v-if="tour">
          {{ t('common.dateRange', { start: tour.startDate, end: tour.endDate }) }}
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <RouterLink
          class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
          :to="`/admin/tours/${tourId}/checkin`"
        >
          {{ t('common.checkIn') }}
        </RouterLink>
        <a
          class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
          :href="`/t/${tourId}`"
          rel="noreferrer"
          target="_blank"
        >
          {{ t('admin.tourDetail.openPortal') }}
        </a>
      </div>
    </div>

    <LoadingState v-if="loading" message-key="admin.tourDetail.loading" />
    <ErrorState
      v-else-if="loadErrorKey || loadErrorMessage"
      :message="loadErrorMessage ?? undefined"
      :message-key="loadErrorKey ?? undefined"
      @retry="loadTour"
    />

    <template v-else>
      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div class="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 class="text-lg font-semibold">{{ t('admin.tourDetail.detailsTitle') }}</h2>
            <p class="text-sm text-slate-500">{{ t('admin.tourDetail.detailsSubtitle') }}</p>
          </div>
        </div>

        <form class="mt-4 grid gap-4 md:grid-cols-3" @submit.prevent="saveTour">
          <label class="grid gap-1 text-sm md:col-span-1">
            <span class="text-slate-600">{{ t('admin.tourDetail.form.nameLabel') }}</span>
            <input
              v-model.trim="tourForm.name"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :placeholder="t('admin.tourDetail.form.namePlaceholder')"
              type="text"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.tourDetail.form.startDateLabel') }}</span>
            <input
              v-model="tourForm.startDate"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="date"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.tourDetail.form.endDateLabel') }}</span>
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
              {{ tourSaving ? t('common.saving') : t('admin.tourDetail.form.save') }}
            </button>
            <span v-if="tourMessageKey" class="text-xs text-emerald-600">{{ t(tourMessageKey) }}</span>
            <span v-if="tourErrorKey || tourErrorMessage" class="text-xs text-rose-600">
              {{ tourErrorKey ? t(tourErrorKey) : tourErrorMessage }}
            </span>
            <span v-if="dateHintKey" class="text-xs text-slate-500">{{ t(dateHintKey) }}</span>
          </div>
        </form>
      </section>

      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 class="text-lg font-semibold">{{ t('admin.tourDetail.guide.title') }}</h2>
            <p class="text-sm text-slate-500">{{ t('admin.tourDetail.guide.subtitle') }}</p>
          </div>
        </div>

        <div v-if="guideLoading" class="mt-4 text-sm text-slate-500">{{ t('admin.tourDetail.guide.loading') }}</div>
        <div v-else-if="guides.length === 0" class="mt-4 text-sm text-slate-500">
          {{ t('admin.tourDetail.guide.empty') }}
        </div>
        <form v-else class="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center" @submit.prevent="saveGuide">
          <select
            v-model="guideId"
            class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none sm:w-auto"
          >
            <option value="" disabled>{{ t('admin.tourDetail.guide.selectPlaceholder') }}</option>
            <option v-for="guide in guides" :key="guide.id" :value="guide.id">
              {{ guide.fullName || guide.email }}
            </option>
          </select>
          <button
            class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
            :disabled="guideSaving"
            type="submit"
          >
            {{ guideSaving ? t('common.saving') : t('admin.tourDetail.guide.save') }}
          </button>
          <span v-if="guideErrorKey || guideErrorMessage" class="text-xs text-rose-600">
            {{ guideErrorKey ? t(guideErrorKey) : guideErrorMessage }}
          </span>
        </form>
      </section>

      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h2 class="text-lg font-semibold">{{ t('admin.participants.addTitle') }}</h2>
        <form class="mt-4 grid gap-4 md:grid-cols-3" @submit.prevent="addParticipant">
          <label class="grid gap-1 text-sm md:col-span-1">
            <span class="text-slate-600">{{ t('admin.participants.form.fullName') }}</span>
            <input
              v-model.trim="form.fullName"
              ref="nameInput"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :placeholder="t('admin.participants.form.fullNamePlaceholder')"
              type="text"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.participants.form.email') }}</span>
            <input
              v-model.trim="form.email"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :placeholder="t('admin.participants.form.emailPlaceholder')"
              type="email"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.participants.form.phone') }}</span>
            <input
              v-model.trim="form.phone"
              class="rounded border bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :class="phoneErrorKey ? 'border-rose-300' : 'border-slate-200'"
              :placeholder="t('admin.participants.form.phonePlaceholder')"
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
              {{ submitting ? t('admin.participants.form.adding') : t('admin.participants.form.add') }}
            </button>
          </div>
        </form>
        <p v-if="phoneErrorKey" class="mt-2 text-sm text-rose-600">{{ t(phoneErrorKey) }}</p>
        <p v-if="formErrorKey || formErrorMessage" class="mt-3 text-sm text-rose-600">
          {{ formErrorKey ? t(formErrorKey) : formErrorMessage }}
        </p>
      </section>

      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div class="flex items-center justify-between">
          <h2 class="text-lg font-semibold">{{ t('admin.portal.title') }}</h2>
          <span class="text-xs text-slate-500">{{ t('admin.portal.subtitle') }}</span>
        </div>

        <form class="mt-4 space-y-6" @submit.prevent="savePortal">
          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-slate-700">{{ t('admin.portal.meeting.title') }}</h3>
            <div class="grid gap-4 md:grid-cols-3">
              <label class="grid gap-1 text-sm">
                <span class="text-slate-600">{{ t('admin.portal.meeting.time') }}</span>
                <input
                  v-model.trim="portalForm.meetingTime"
                  class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  :placeholder="t('admin.portal.meeting.timePlaceholder')"
                  type="text"
                />
              </label>
              <label class="grid gap-1 text-sm md:col-span-2">
                <span class="text-slate-600">{{ t('admin.portal.meeting.place') }}</span>
                <input
                  v-model.trim="portalForm.meetingPlace"
                  class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  :placeholder="t('admin.portal.meeting.placePlaceholder')"
                  type="text"
                />
              </label>
              <label class="grid gap-1 text-sm md:col-span-2">
                <span class="text-slate-600">{{ t('admin.portal.meeting.maps') }}</span>
                <input
                  v-model.trim="portalForm.meetingMapsUrl"
                  class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  :placeholder="t('admin.portal.meeting.mapsPlaceholder')"
                  type="url"
                />
              </label>
              <label class="grid gap-1 text-sm md:col-span-3">
                <span class="text-slate-600">{{ t('admin.portal.meeting.note') }}</span>
                <textarea
                  v-model.trim="portalForm.meetingNote"
                  class="min-h-22.5 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  :placeholder="t('admin.portal.meeting.notePlaceholder')"
                ></textarea>
              </label>
            </div>
          </div>

          <div class="space-y-3">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-semibold text-slate-700">{{ t('admin.portal.links.title') }}</h3>
              <button
                class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
                type="button"
                @click="addPortalLink"
              >
                {{ t('admin.portal.links.add') }}
              </button>
            </div>
            <div class="space-y-3">
              <div
                v-for="(link, index) in portalForm.links"
                :key="index"
                class="grid gap-3 md:grid-cols-[1fr_1fr_auto]"
              >
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.portal.links.label') }}</span>
                  <input
                    v-model.trim="link.label"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    :placeholder="t('admin.portal.links.labelPlaceholder')"
                    type="text"
                  />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.portal.links.url') }}</span>
                  <input
                    v-model.trim="link.url"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    :placeholder="t('admin.portal.links.urlPlaceholder')"
                    type="url"
                  />
                </label>
                <div class="flex items-end">
                  <button
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 hover:border-slate-300"
                    type="button"
                    @click="removePortalLink(index)"
                  >
                    {{ t('common.remove') }}
                  </button>
                </div>
              </div>
            </div>
          </div>

          <div class="space-y-3">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-semibold text-slate-700">{{ t('admin.portal.days.title') }}</h3>
              <button
                class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
                type="button"
                @click="addDay"
              >
                {{ t('admin.portal.days.add') }}
              </button>
            </div>
            <div
              v-if="portalDays.length === 0"
              class="rounded border border-dashed border-slate-200 bg-slate-50 p-4 text-sm text-slate-500"
            >
              {{ t('admin.portal.days.empty') }}
            </div>
            <div v-else class="space-y-4">
              <div
                v-for="(day, index) in portalDays"
                :key="day.day"
                class="rounded-lg border border-slate-200 bg-slate-50 p-4"
              >
                <div class="flex flex-wrap items-center justify-between gap-2">
                  <div class="text-sm font-semibold text-slate-700">
                    {{ t('admin.portal.days.dayLabel', { day: index + 1 }) }}
                  </div>
                  <div class="flex flex-wrap gap-2">
                    <button
                      class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                      :disabled="index === 0"
                      type="button"
                      @click="moveDay(index, -1)"
                    >
                      {{ t('common.moveUp') }}
                    </button>
                    <button
                      class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                      :disabled="index === portalDays.length - 1"
                      type="button"
                      @click="moveDay(index, 1)"
                    >
                      {{ t('common.moveDown') }}
                    </button>
                    <button
                      class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                      type="button"
                      @click="removeDay(index)"
                    >
                      {{ t('common.remove') }}
                    </button>
                  </div>
                </div>
                <div class="mt-3 grid gap-3 md:grid-cols-2">
                  <label class="grid gap-1 text-sm md:col-span-2">
                    <span class="text-slate-600">{{ t('admin.portal.days.titleLabel') }}</span>
                    <input
                      v-model.trim="day.title"
                      class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                      :placeholder="t('admin.portal.days.titlePlaceholder')"
                      type="text"
                    />
                  </label>
                  <label class="grid gap-1 text-sm md:col-span-2">
                    <span class="text-slate-600">{{ t('admin.portal.days.itemsLabel') }}</span>
                    <textarea
                      class="min-h-24 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                      :value="day.items.join('\n')"
                      :placeholder="t('admin.portal.days.itemsPlaceholder')"
                      @input="updateDayItems(index, ($event.target as HTMLTextAreaElement).value)"
                    ></textarea>
                  </label>
                </div>
              </div>
            </div>
          </div>

          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-slate-700">{{ t('admin.portal.notes.title') }}</h3>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.portal.notes.label') }}</span>
              <textarea
                v-model="portalForm.notesText"
                class="min-h-30 rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :placeholder="t('admin.portal.notes.placeholder')"
              ></textarea>
            </label>
          </div>

          <div class="flex flex-wrap items-center gap-3">
            <button
              class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
              :disabled="portalSaving"
              type="submit"
            >
              {{ portalSaving ? t('common.saving') : t('admin.portal.save') }}
            </button>
            <span v-if="portalMessageKey" class="text-xs text-emerald-600">{{ t(portalMessageKey) }}</span>
            <span v-if="portalErrorKey || portalErrorMessage" class="text-xs text-rose-600">
              {{ portalErrorKey ? t(portalErrorKey) : portalErrorMessage }}
            </span>
          </div>
        </form>
      </section>

      <section class="space-y-4">
        <div class="flex items-center justify-between">
          <h2 class="text-lg font-semibold">{{ t('admin.participants.title') }}</h2>
          <span class="text-xs text-slate-500">{{ participants.length }} {{ t('common.total') }}</span>
        </div>

        <div
          v-if="participants.length === 0"
          class="rounded border border-dashed border-slate-200 bg-white p-6 text-sm text-slate-500"
        >
          {{ t('admin.participants.empty') }}
        </div>

        <ul v-else class="space-y-3">
          <li
            v-for="participant in participants"
            :key="participant.id"
            class="rounded-lg border border-slate-200 bg-white p-4 shadow-sm"
          >
            <div v-if="editingParticipantId === participant.id" class="space-y-3">
              <div class="text-sm font-semibold text-slate-700">{{ t('admin.participants.editTitle') }}</div>
              <div class="grid gap-3 md:grid-cols-3">
                <label class="grid gap-1 text-sm md:col-span-1">
                  <span class="text-slate-600">{{ t('admin.participants.form.fullName') }}</span>
                  <input
                    v-model.trim="editForm.fullName"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    type="text"
                  />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participants.form.emailShort') }}</span>
                  <input
                    v-model.trim="editForm.email"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    type="email"
                  />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participants.form.phoneShort') }}</span>
                  <input
                    v-model.trim="editForm.phone"
                    class="rounded border bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    :class="editPhoneErrorKey ? 'border-rose-300' : 'border-slate-200'"
                    :placeholder="t('admin.participants.form.phonePlaceholder')"
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
                  {{ editParticipantSaving ? t('common.saving') : t('common.save') }}
                </button>
                <button
                  class="rounded border border-slate-200 bg-white px-4 py-2 text-sm text-slate-700 hover:border-slate-300"
                  type="button"
                  @click="cancelEditParticipant"
                >
                  {{ t('common.cancel') }}
                </button>
                <span v-if="editPhoneErrorKey" class="text-xs text-rose-600">
                  {{ t(editPhoneErrorKey) }}
                </span>
                <span v-if="editParticipantErrorKey || editParticipantErrorMessage" class="text-xs text-rose-600">
                  {{ editParticipantErrorKey ? t(editParticipantErrorKey) : editParticipantErrorMessage }}
                </span>
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
                  {{ participant.arrived ? t('common.arrivedLabel') : t('common.pendingLabel') }}
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
                  {{ t('common.edit') }}
                </button>
                <button
                  class="rounded border border-rose-200 bg-rose-50 px-3 py-1.5 text-xs font-medium text-rose-700 hover:border-rose-300"
                  type="button"
                  @click="deleteParticipant(participant)"
                >
                  {{ t('common.delete') }}
                </button>
              </div>
            </div>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>
