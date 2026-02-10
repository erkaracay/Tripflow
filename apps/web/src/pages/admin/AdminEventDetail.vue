<script setup lang="ts">
import { computed, nextTick, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiDelete, apiGet, apiPost, apiPostWithHeaders, apiPut, apiPutWithHeaders } from '../../lib/api'
import { getSelectedOrgId, getToken, getTokenRole, isTokenExpired } from '../../lib/auth'
import { sanitizeEventAccessCode, isValidEventCodeLength } from '../../lib/eventAccessCode'
import { formatBaggage } from '../../lib/formatters'
import {
  formatPhoneDisplay,
  normalizeEmail,
  normalizeName,
  normalizePhone,
  sanitizePhoneInput,
} from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import { buildWhatsAppUrl } from '../../lib/whatsapp'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import ConfirmDialog from '../../components/ui/ConfirmDialog.vue'
import WhatsAppIcon from '../../components/icons/WhatsAppIcon.vue'
import type {
  EventAccessCodeResponse,
  LinkInfo,
  Participant,
  Event as EventDto,
  EventPortalInfo,
  UserListItem,
} from '../../types'

const props = defineProps<{ eventId: string }>()
const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const eventId = computed(() => (props.eventId ?? route.params.eventId) as string)

const event = ref<EventDto | null>(null)
const participants = ref<Participant[]>([])
const portal = ref<EventPortalInfo | null>(null)
const eventForm = reactive({
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
const eventSaving = ref(false)
const eventSavedTimer = ref<ReturnType<typeof setTimeout> | null>(null)
const eventMessageKey = ref<string | null>(null)
const eventErrorKey = ref<string | null>(null)
const eventErrorMessage = ref<string | null>(null)
const dateHintKey = ref<string | null>(null)
const portalSaving = ref(false)
const portalSavedTimer = ref<ReturnType<typeof setTimeout> | null>(null)
const portalMessageKey = ref<string | null>(null)
const portalErrorKey = ref<string | null>(null)
const portalErrorMessage = ref<string | null>(null)
const guideSaving = ref(false)
const guideErrorKey = ref<string | null>(null)
const guideErrorMessage = ref<string | null>(null)
const guideLoading = ref(true)
const guides = ref<UserListItem[]>([])
const guideIds = ref<string[]>([])
const selectedGuideToAdd = ref('')
const phoneErrorKey = ref<string | null>(null)
const editPhoneErrorKey = ref<string | null>(null)
const tcNoErrorKey = ref<string | null>(null)
const editTcNoErrorKey = ref<string | null>(null)
const nameInput = ref<HTMLInputElement | null>(null)
const editingParticipantId = ref<string | null>(null)
const editParticipantSaving = ref(false)
const editParticipantErrorKey = ref<string | null>(null)
const editParticipantErrorMessage = ref<string | null>(null)
const missingPhoneParticipantId = ref<string | null>(null)
const editPhoneInput = ref<HTMLInputElement | null>(null)
const accessCodeLoading = ref(false)
const accessCodeMessageKey = ref<string | null>(null)
const accessCodeErrorKey = ref<string | null>(null)
const editCodeModalOpen = ref(false)
const editCodeValue = ref('')
const editCodeSaving = ref(false)
const editCodeErrorKey = ref<string | null>(null)

const tcNoWarnings = ref<Record<string, string>>({})
const archivingEvent = ref(false)
const restoringEvent = ref(false)
const purgingEvent = ref(false)
const purgeConfirmText = ref('')
const purgeErrorKey = ref<string | null>(null)
const resettingAllCheckIns = ref(false)
const confirmOpen = ref(false)
const confirmTone = ref<'default' | 'danger'>('default')
const confirmMessageKey = ref<string | null>(null)
const confirmAction = ref<'resetCheckIns' | 'deleteAll' | null>(null)
const deletingAllParticipants = ref(false)

const { pushToast } = useToast()
const isSuperAdmin = computed(() => {
  const token = getToken()
  if (!token || isTokenExpired(token)) {
    return false
  }

  return getTokenRole(token) === 'SuperAdmin'
})

const purgeConfirmValid = computed(() => {
  if (!event.value) {
    return false
  }

  const value = purgeConfirmText.value.trim()
  if (!value) {
    return false
  }

  return value.toLowerCase() === 'sil' || value === event.value.name
})

const form = reactive({
  fullName: '',
  tcNo: '',
  birthDate: '',
  gender: '',
  email: '',
  phone: '',
})

const editForm = reactive({
  fullName: '',
  tcNo: '',
  birthDate: '',
  gender: '',
  email: '',
  phone: '',
})

const editDetails = reactive({
  roomNo: '',
  roomType: '',
  personNo: '',
  agencyName: '',
  city: '',
  flightCity: '',
  hotelCheckInDate: '',
  hotelCheckOutDate: '',
  arrivalTicketNo: '',
  returnTicketNo: '',
  attendanceStatus: '',
  arrivalAirline: '',
  arrivalDepartureAirport: '',
  arrivalArrivalAirport: '',
  arrivalFlightCode: '',
  arrivalDepartureTime: '',
  arrivalArrivalTime: '',
  arrivalPnr: '',
  arrivalBaggageAllowance: '',
  arrivalCabinBaggage: '',
  returnAirline: '',
  returnDepartureAirport: '',
  returnArrivalAirport: '',
  returnFlightCode: '',
  returnDepartureTime: '',
  returnArrivalTime: '',
  returnPnr: '',
  returnBaggageAllowance: '',
  returnCabinBaggage: '',
  arrivalTransferPickupTime: '',
  arrivalTransferPickupPlace: '',
  arrivalTransferDropoffPlace: '',
  arrivalTransferVehicle: '',
  arrivalTransferPlate: '',
  arrivalTransferDriverInfo: '',
  arrivalTransferNote: '',
  returnTransferPickupTime: '',
  returnTransferPickupPlace: '',
  returnTransferDropoffPlace: '',
  returnTransferVehicle: '',
  returnTransferPlate: '',
  returnTransferDriverInfo: '',
  returnTransferNote: '',
})

const genderOptions = [
  { value: 'Female', label: 'common.genderFemale' },
  { value: 'Male', label: 'common.genderMale' },
  { value: 'Other', label: 'common.genderOther' },
]

const sanitizeTcNoInput = (value: string) => value.replace(/\D/g, '').slice(0, 11)

const handleTcNoInput = () => {
  tcNoErrorKey.value = null
  form.tcNo = sanitizeTcNoInput(form.tcNo)
}

const handleEditTcNoInput = () => {
  editTcNoErrorKey.value = null
  editForm.tcNo = sanitizeTcNoInput(editForm.tcNo)
}

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

const setEventForm = (data: EventDto) => {
  eventForm.name = data.name
  eventForm.startDate = data.startDate
  eventForm.endDate = data.endDate
}

const setPortalForm = (data: EventPortalInfo) => {
  portalForm.meetingTime = data.meeting.time
  portalForm.meetingPlace = data.meeting.place
  portalForm.meetingMapsUrl = data.meeting.mapsUrl
  portalForm.meetingNote = data.meeting.note
  portalForm.notesText = data.notes.join('\n')

  const normalizedLinks = data.links.length > 0 ? data.links : [{ label: '', url: '' }]
  portalForm.links.splice(0, portalForm.links.length, ...normalizedLinks)
}

const showEventSaved = () => {
  eventMessageKey.value = 'common.saved'
  if (eventSavedTimer.value) {
    globalThis.clearTimeout(eventSavedTimer.value)
  }
  eventSavedTimer.value = globalThis.setTimeout(() => {
    eventMessageKey.value = null
    eventSavedTimer.value = null
  }, 3000)
}

const showPortalSaved = () => {
  portalMessageKey.value = 'common.saved'
  if (portalSavedTimer.value) {
    globalThis.clearTimeout(portalSavedTimer.value)
  }
  portalSavedTimer.value = globalThis.setTimeout(() => {
    portalMessageKey.value = null
    portalSavedTimer.value = null
  }, 3000)
}

const archiveEvent = async () => {
  if (!event.value || archivingEvent.value) {
    return
  }

  archivingEvent.value = true
  try {
    const updated = await apiPost<EventDto>(`/api/events/${eventId.value}/archive`, {})
    event.value = updated
    pushToast({ key: 'toast.eventArchived', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.eventArchiveFailed', tone: 'error' })
  } finally {
    archivingEvent.value = false
  }
}

const restoreEvent = async () => {
  if (!event.value || restoringEvent.value) {
    return
  }

  restoringEvent.value = true
  try {
    const updated = await apiPost<EventDto>(`/api/events/${eventId.value}/restore`, {})
    event.value = updated
    pushToast({ key: 'toast.eventRestored', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.eventRestoreFailed', tone: 'error' })
  } finally {
    restoringEvent.value = false
  }
}

const purgeEvent = async () => {
  purgeErrorKey.value = null
  if (!event.value || purgingEvent.value) {
    return
  }

  if (!event.value.isDeleted) {
    purgeErrorKey.value = 'admin.eventDetail.purgeRequiresArchive'
    return
  }

  if (!purgeConfirmValid.value) {
    purgeErrorKey.value = 'admin.eventDetail.purgeConfirmMismatch'
    return
  }

  purgingEvent.value = true
  try {
    await apiDelete(`/api/events/${eventId.value}/purge`)
    pushToast({ key: 'toast.eventPurged', tone: 'success' })
    globalThis.location?.assign('/admin/events')
  } catch {
    pushToast({ key: 'toast.eventPurgeFailed', tone: 'error' })
  } finally {
    purgingEvent.value = false
  }
}

const resolvePublicBase = () => {
  const envBase = (import.meta.env.VITE_PUBLIC_BASE_URL as string | undefined)?.trim()
  if (envBase) {
    return envBase.replace(/\/$/, '')
  }

  return globalThis.location?.origin ?? ''
}

const buildPortalLoginLink = (code?: string) => {
  const base = resolvePublicBase()
  if (!base) {
    return ''
  }

  if (code) {
    return `${base}/e/login?code=${encodeURIComponent(code)}`
  }

  return `${base}/e/login`
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

const loadEvent = async () => {
  loading.value = true
  loadErrorKey.value = null
  loadErrorMessage.value = null
  portalMessageKey.value = null
  portalErrorKey.value = null
  portalErrorMessage.value = null
  formErrorKey.value = null
  formErrorMessage.value = null
  eventMessageKey.value = null
  eventErrorKey.value = null
  eventErrorMessage.value = null
  accessCodeMessageKey.value = null
  accessCodeErrorKey.value = null
  dateHintKey.value = null
  editingParticipantId.value = null
  editParticipantErrorKey.value = null
  editParticipantErrorMessage.value = null
  editPhoneErrorKey.value = null
  editTcNoErrorKey.value = null
  tcNoErrorKey.value = null
  guideErrorKey.value = null
  guideErrorMessage.value = null
  guideLoading.value = true

  try {
    const [eventData, participantsData, portalData] = await Promise.all([
      apiGet<EventDto>(`/api/events/${eventId.value}`),
      apiGet<Participant[]>(`/api/events/${eventId.value}/participants`),
      apiGet<EventPortalInfo>(`/api/events/${eventId.value}/portal`),
    ])

    event.value = eventData
    setEventForm(eventData)
    participants.value = participantsData
    portal.value = portalData
    guideIds.value = eventData.guideUserIds ?? []
    setPortalForm(portalData)
  } catch (err) {
    loadErrorMessage.value = err instanceof Error ? err.message : null
    if (!loadErrorMessage.value) {
      loadErrorKey.value = 'errors.eventDetail.load'
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

const saveEvent = async () => {
  eventMessageKey.value = null
  eventErrorKey.value = null
  eventErrorMessage.value = null

  const name = eventForm.name.trim()
  if (!name) {
    eventErrorKey.value = 'validation.eventNameRequired'
    return
  }

  if (!eventForm.startDate || !eventForm.endDate) {
    eventErrorKey.value = 'validation.startEndRequired'
    return
  }

  if (eventForm.endDate < eventForm.startDate) {
    eventErrorKey.value = 'validation.endAfterStart'
    return
  }

  eventSaving.value = true
  try {
    const updated = await apiPut<EventDto>(`/api/events/${eventId.value}`, {
      name,
      startDate: eventForm.startDate,
      endDate: eventForm.endDate,
    })
    event.value = updated
    setEventForm(updated)
    showEventSaved()
    pushToast({ key: 'toast.eventUpdated', tone: 'success' })
  } catch (err) {
    eventErrorMessage.value = err instanceof Error ? err.message : null
    if (!eventErrorMessage.value) {
      eventErrorKey.value = 'errors.eventDetail.update'
    }
    pushToast({ key: 'toast.eventUpdateFailed', tone: 'error' })
  } finally {
    eventSaving.value = false
  }
}

const addParticipant = async () => {
  formErrorKey.value = null
  formErrorMessage.value = null
  phoneErrorKey.value = null
  tcNoErrorKey.value = null

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
  if (!normalizedPhone) {
    phoneErrorKey.value = 'validation.phone.required'
    return
  }

  const tcNo = form.tcNo.trim()
  if (!tcNo) {
    tcNoErrorKey.value = 'validation.tcNoRequired'
    return
  }

  if (!form.birthDate) {
    formErrorKey.value = 'validation.birthDateRequired'
    return
  }

  if (!form.gender) {
    formErrorKey.value = 'validation.genderRequired'
    return
  }

  submitting.value = true
  try {
    const { data: created, headers } = await apiPostWithHeaders<Participant>(
      `/api/events/${eventId.value}/participants`,
      {
        fullName,
        phone: normalizedPhone || undefined,
        email: normalizeEmail(form.email) || undefined,
        tcNo,
        birthDate: form.birthDate,
        gender: form.gender,
      }
    )

    participants.value = [...participants.value, created]
    const warning = headers.get('X-Warning') || headers.get('X-Tripflow-Warn')
    if (warning) {
      tcNoWarnings.value = { ...tcNoWarnings.value, [created.id]: warning }
      pushToast({ key: 'warnings.tcNoDuplicate', tone: 'info' })
    }
    form.fullName = ''
    form.tcNo = ''
    form.birthDate = ''
    form.gender = ''
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
  editTcNoErrorKey.value = null
  editForm.fullName = participant.fullName
  editForm.tcNo = participant.tcNo
  editForm.birthDate = participant.birthDate
  editForm.gender = participant.gender
  editForm.email = participant.email ?? ''
  editForm.phone = participant.phone

  editDetails.roomNo = participant.details?.roomNo ?? ''
  editDetails.roomType = participant.details?.roomType ?? ''
  editDetails.personNo = participant.details?.personNo ?? ''
  editDetails.agencyName = participant.details?.agencyName ?? ''
  editDetails.city = participant.details?.city ?? ''
  editDetails.flightCity = participant.details?.flightCity ?? ''
  editDetails.hotelCheckInDate = participant.details?.hotelCheckInDate ?? ''
  editDetails.hotelCheckOutDate = participant.details?.hotelCheckOutDate ?? ''
  editDetails.arrivalTicketNo = participant.details?.arrivalTicketNo ?? participant.details?.ticketNo ?? ''
  editDetails.returnTicketNo = participant.details?.returnTicketNo ?? ''
  editDetails.attendanceStatus = participant.details?.attendanceStatus ?? ''
  editDetails.arrivalAirline = participant.details?.arrivalAirline ?? ''
  editDetails.arrivalDepartureAirport = participant.details?.arrivalDepartureAirport ?? ''
  editDetails.arrivalArrivalAirport = participant.details?.arrivalArrivalAirport ?? ''
  editDetails.arrivalFlightCode = participant.details?.arrivalFlightCode ?? ''
  editDetails.arrivalDepartureTime = participant.details?.arrivalDepartureTime ?? ''
  editDetails.arrivalArrivalTime = participant.details?.arrivalArrivalTime ?? ''
  editDetails.arrivalPnr = participant.details?.arrivalPnr ?? ''
  // Prefill with formatted baggage (pieces / kg / allowance) so it matches the details view like "20 kg"
  {
    const formattedArrivalBaggage = formatBaggage(
      participant.details?.arrivalBaggagePieces ?? null,
      participant.details?.arrivalBaggageTotalKg ?? null,
      participant.details?.arrivalBaggageAllowance ?? null
    )
    editDetails.arrivalBaggageAllowance = formattedArrivalBaggage === '—' ? '' : formattedArrivalBaggage
  }
  editDetails.arrivalCabinBaggage = participant.details?.arrivalCabinBaggage ?? ''
  editDetails.returnAirline = participant.details?.returnAirline ?? ''
  editDetails.returnDepartureAirport = participant.details?.returnDepartureAirport ?? ''
  editDetails.returnArrivalAirport = participant.details?.returnArrivalAirport ?? ''
  editDetails.returnFlightCode = participant.details?.returnFlightCode ?? ''
  editDetails.returnDepartureTime = participant.details?.returnDepartureTime ?? ''
  editDetails.returnArrivalTime = participant.details?.returnArrivalTime ?? ''
  editDetails.returnPnr = participant.details?.returnPnr ?? ''
  {
    const formattedReturnBaggage = formatBaggage(
      participant.details?.returnBaggagePieces ?? null,
      participant.details?.returnBaggageTotalKg ?? null,
      participant.details?.returnBaggageAllowance ?? null
    )
    editDetails.returnBaggageAllowance = formattedReturnBaggage === '—' ? '' : formattedReturnBaggage
  }
  editDetails.returnCabinBaggage = participant.details?.returnCabinBaggage ?? ''
  editDetails.arrivalTransferPickupTime = participant.details?.arrivalTransferPickupTime ?? ''
  editDetails.arrivalTransferPickupPlace = participant.details?.arrivalTransferPickupPlace ?? ''
  editDetails.arrivalTransferDropoffPlace = participant.details?.arrivalTransferDropoffPlace ?? ''
  editDetails.arrivalTransferVehicle = participant.details?.arrivalTransferVehicle ?? ''
  editDetails.arrivalTransferPlate = participant.details?.arrivalTransferPlate ?? ''
  editDetails.arrivalTransferDriverInfo = participant.details?.arrivalTransferDriverInfo ?? ''
  editDetails.arrivalTransferNote = participant.details?.arrivalTransferNote ?? ''
  editDetails.returnTransferPickupTime = participant.details?.returnTransferPickupTime ?? ''
  editDetails.returnTransferPickupPlace = participant.details?.returnTransferPickupPlace ?? ''
  editDetails.returnTransferDropoffPlace = participant.details?.returnTransferDropoffPlace ?? ''
  editDetails.returnTransferVehicle = participant.details?.returnTransferVehicle ?? ''
  editDetails.returnTransferPlate = participant.details?.returnTransferPlate ?? ''
  editDetails.returnTransferDriverInfo = participant.details?.returnTransferDriverInfo ?? ''
  editDetails.returnTransferNote = participant.details?.returnTransferNote ?? ''
}

const cancelEditParticipant = () => {
  editingParticipantId.value = null
  editParticipantErrorKey.value = null
  editParticipantErrorMessage.value = null
  editPhoneErrorKey.value = null
  editTcNoErrorKey.value = null
  missingPhoneParticipantId.value = null
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
  editTcNoErrorKey.value = null

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
  if (!normalizedPhone) {
    editPhoneErrorKey.value = 'validation.phone.required'
    return
  }

  const tcNo = editForm.tcNo.trim()
  if (!tcNo) {
    editTcNoErrorKey.value = 'validation.tcNoRequired'
    return
  }

  if (!editForm.birthDate) {
    editParticipantErrorKey.value = 'validation.birthDateRequired'
    return
  }

  if (!editForm.gender) {
    editParticipantErrorKey.value = 'validation.genderRequired'
    return
  }

  editParticipantSaving.value = true
  try {
    const { data: updated, headers } = await apiPutWithHeaders<Participant>(
      `/api/events/${eventId.value}/participants/${participant.id}`,
      {
        fullName,
        phone: normalizedPhone || undefined,
        email: normalizeEmail(editForm.email) || undefined,
        tcNo,
        birthDate: editForm.birthDate,
        gender: editForm.gender,
        details: {
          roomNo: editDetails.roomNo || undefined,
          roomType: editDetails.roomType || undefined,
          personNo: editDetails.personNo || undefined,
          agencyName: editDetails.agencyName || undefined,
          city: editDetails.city || undefined,
          flightCity: editDetails.flightCity || undefined,
          hotelCheckInDate: editDetails.hotelCheckInDate || undefined,
          hotelCheckOutDate: editDetails.hotelCheckOutDate || undefined,
          arrivalTicketNo: editDetails.arrivalTicketNo || undefined,
          returnTicketNo: editDetails.returnTicketNo || undefined,
          attendanceStatus: editDetails.attendanceStatus || undefined,
          arrivalAirline: editDetails.arrivalAirline || undefined,
          arrivalDepartureAirport: editDetails.arrivalDepartureAirport || undefined,
          arrivalArrivalAirport: editDetails.arrivalArrivalAirport || undefined,
          arrivalFlightCode: editDetails.arrivalFlightCode || undefined,
          arrivalDepartureTime: editDetails.arrivalDepartureTime || undefined,
          arrivalArrivalTime: editDetails.arrivalArrivalTime || undefined,
          arrivalPnr: editDetails.arrivalPnr || undefined,
          arrivalBaggageAllowance: editDetails.arrivalBaggageAllowance || undefined,
          arrivalCabinBaggage: editDetails.arrivalCabinBaggage || undefined,
          returnAirline: editDetails.returnAirline || undefined,
          returnDepartureAirport: editDetails.returnDepartureAirport || undefined,
          returnArrivalAirport: editDetails.returnArrivalAirport || undefined,
          returnFlightCode: editDetails.returnFlightCode || undefined,
          returnDepartureTime: editDetails.returnDepartureTime || undefined,
          returnArrivalTime: editDetails.returnArrivalTime || undefined,
          returnPnr: editDetails.returnPnr || undefined,
          returnBaggageAllowance: editDetails.returnBaggageAllowance || undefined,
          returnCabinBaggage: editDetails.returnCabinBaggage || undefined,
          arrivalTransferPickupTime: editDetails.arrivalTransferPickupTime || undefined,
          arrivalTransferPickupPlace: editDetails.arrivalTransferPickupPlace || undefined,
          arrivalTransferDropoffPlace: editDetails.arrivalTransferDropoffPlace || undefined,
          arrivalTransferVehicle: editDetails.arrivalTransferVehicle || undefined,
          arrivalTransferPlate: editDetails.arrivalTransferPlate || undefined,
          arrivalTransferDriverInfo: editDetails.arrivalTransferDriverInfo || undefined,
          arrivalTransferNote: editDetails.arrivalTransferNote || undefined,
          returnTransferPickupTime: editDetails.returnTransferPickupTime || undefined,
          returnTransferPickupPlace: editDetails.returnTransferPickupPlace || undefined,
          returnTransferDropoffPlace: editDetails.returnTransferDropoffPlace || undefined,
          returnTransferVehicle: editDetails.returnTransferVehicle || undefined,
          returnTransferPlate: editDetails.returnTransferPlate || undefined,
          returnTransferDriverInfo: editDetails.returnTransferDriverInfo || undefined,
          returnTransferNote: editDetails.returnTransferNote || undefined,
        },
      }
    )

    participants.value = participants.value.map((item) =>
      item.id === participant.id ? updated : item
    )
    const warning = headers.get('X-Warning') || headers.get('X-Tripflow-Warn')
    if (warning) {
      tcNoWarnings.value = { ...tcNoWarnings.value, [participant.id]: warning }
      pushToast({ key: 'warnings.tcNoDuplicate', tone: 'info' })
    } else if (tcNoWarnings.value[participant.id]) {
      const next = { ...tcNoWarnings.value }
      delete next[participant.id]
      tcNoWarnings.value = next
    }
    editingParticipantId.value = null
    if (missingPhoneParticipantId.value === participant.id) {
      missingPhoneParticipantId.value = null
    }
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
    await apiDelete(`/api/events/${eventId.value}/participants/${participant.id}`)
    participants.value = participants.value.filter((item) => item.id !== participant.id)
    pushToast({ key: 'toast.participantRemoved', tone: 'success' })
  } catch (err) {
    pushToast({ key: 'toast.participantDeleteFailed', tone: 'error' })
  }
}

const requestResetAllCheckIns = () => {
  confirmAction.value = 'resetCheckIns'
  confirmTone.value = 'default'
  confirmMessageKey.value = 'admin.participants.resetAllConfirm'
  confirmOpen.value = true
}

const resetAllCheckIns = async () => {
  if (resettingAllCheckIns.value) {
    return
  }

  resettingAllCheckIns.value = true
  try {
    await apiPost(`/api/events/${eventId.value}/checkins/reset-all`, {})
    participants.value = participants.value.map((item) => ({ ...item, arrived: false }))
    pushToast({ key: 'toast.checkInsResetAll', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.checkInsResetAllFailed', tone: 'error' })
  } finally {
    resettingAllCheckIns.value = false
  }
}

const requestDeleteAllParticipants = () => {
  confirmAction.value = 'deleteAll'
  confirmTone.value = 'danger'
  confirmMessageKey.value = 'admin.participants.deleteAllConfirm'
  confirmOpen.value = true
}

const deleteAllParticipants = async () => {
  if (deletingAllParticipants.value) {
    return
  }

  deletingAllParticipants.value = true
  try {
    await apiDelete(`/api/events/${eventId.value}/participants`)
    participants.value = []
    pushToast({ key: 'toast.participantsDeletedAll', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.participantsDeleteAllFailed', tone: 'error' })
  } finally {
    deletingAllParticipants.value = false
  }
}

const handleConfirm = async () => {
  if (confirmAction.value === 'resetCheckIns') {
    await resetAllCheckIns()
  } else if (confirmAction.value === 'deleteAll') {
    await deleteAllParticipants()
  }
  confirmAction.value = null
}

const copyToClipboard = async (value: string, successKey: string, errorKey: string) => {
  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    pushToast({ key: 'errors.copyNotSupported', tone: 'error' })
    return
  }

  try {
    await clipboard.writeText(value)
    pushToast({ key: successKey, tone: 'success' })
  } catch {
    pushToast({ key: errorKey, tone: 'error' })
  }
}
const copyPortalLoginLink = async () => {
  const url = buildPortalLoginLink(event.value?.eventAccessCode ?? undefined)
  if (!url) {
    pushToast({ key: 'toast.portalLinkCopyFailed', tone: 'error' })
    return
  }
  await copyToClipboard(url, 'toast.portalLinkCopied', 'toast.portalLinkCopyFailed')
}

const copyEventAccessCode = async () => {
  const code = event.value?.eventAccessCode ?? ''
  if (!code) {
    pushToast({ key: 'toast.eventAccessCodeCopyFailed', tone: 'error' })
    return
  }

  await copyToClipboard(code, 'toast.eventAccessCodeCopied', 'toast.eventAccessCodeCopyFailed')
}

const regenerateEventAccessCode = async () => {
  if (accessCodeLoading.value) {
    return
  }

  accessCodeLoading.value = true
  accessCodeMessageKey.value = null
  accessCodeErrorKey.value = null
  try {
    const response = await apiPost<EventAccessCodeResponse>(
      `/api/events/${eventId.value}/access-code/regenerate`,
      {}
    )
    if (event.value) {
      event.value.eventAccessCode = response.eventAccessCode
    }
    accessCodeMessageKey.value = 'admin.eventAccess.regenerated'
    pushToast({ key: 'toast.eventAccessCodeRegenerated', tone: 'success' })
  } catch {
    accessCodeErrorKey.value = 'errors.eventAccess.regenerate'
    pushToast({ key: 'toast.eventAccessCodeRegenerateFailed', tone: 'error' })
  } finally {
    accessCodeLoading.value = false
  }
}

const openEditCodeModal = () => {
  editCodeValue.value = event.value?.eventAccessCode ?? ''
  editCodeErrorKey.value = null
  editCodeModalOpen.value = true
}

const closeEditCodeModal = () => {
  if (!editCodeSaving.value) editCodeModalOpen.value = false
}

const saveEditCode = async () => {
  editCodeErrorKey.value = null
  const code = sanitizeEventAccessCode(editCodeValue.value)
  if (!isValidEventCodeLength(code)) {
    editCodeErrorKey.value = 'admin.eventAccess.editCodeInvalid'
    return
  }
  editCodeSaving.value = true
  try {
    const response = await apiPut<EventAccessCodeResponse>(
      `/api/events/${eventId.value}/access-code`,
      { eventAccessCode: code }
    )
    if (event.value) event.value.eventAccessCode = response.eventAccessCode
    editCodeModalOpen.value = false
    pushToast({ key: 'toast.eventAccessCodeRegenerated', tone: 'success' })
  } catch (err: unknown) {
    const payload = err && typeof err === 'object' && 'payload' in err ? (err as { payload?: { code?: string } }).payload : undefined
    if (payload?.code === 'event_access_code_taken') {
      editCodeErrorKey.value = 'admin.eventAccess.editCodeTaken'
    } else {
      editCodeErrorKey.value = 'admin.eventAccess.editCodeInvalid'
    }
  } finally {
    editCodeSaving.value = false
  }
}

const handleMissingWhatsAppPhone = async (participant: Participant) => {
  missingPhoneParticipantId.value = participant.id
  startEditParticipant(participant)
  editPhoneErrorKey.value = 'validation.phone.required'
  pushToast({ key: 'warnings.phoneRequiredForWhatsapp', tone: 'error' })
  await nextTick()
  editPhoneInput.value?.focus()
}

const openWhatsApp = async (participant: Participant) => {
  if (!participant.phone) {
    await handleMissingWhatsAppPhone(participant)
    return
  }

  missingPhoneParticipantId.value = null

  try {
    const portalUrl = buildPortalLoginLink(event.value?.eventAccessCode ?? undefined)
    if (!portalUrl) {
      pushToast({ key: 'toast.portalLinkCopyFailed', tone: 'error' })
      return
    }
    const code = event.value?.eventAccessCode ?? ''
    if (!code) {
      pushToast({ key: 'toast.eventAccessCodeCopyFailed', tone: 'error' })
      return
    }
    const message = t('admin.eventAccess.whatsappTemplate', {
      name: participant.fullName,
      eventName: event.value?.name ?? t('admin.eventAccess.eventNameFallback'),
      code,
      url: portalUrl,
    })
    const waUrl = buildWhatsAppUrl(participant.phone ?? '', message)
    if (!waUrl) {
      pushToast({ key: 'toast.portalLinkCopyFailed', tone: 'error' })
      return
    }

    globalThis.open?.(waUrl, '_blank', 'noopener,noreferrer')
  } catch {
    pushToast({ key: 'toast.portalLinkCopyFailed', tone: 'error' })
  }
}

const buildParticipantsExport = () => {
  const headers = [
    'room_no',
    'room_type',
    'board_type',
    'person_no',
    'agency_name',
    'city',
    'full_name',
    'birth_date',
    'tc_no',
    'gender',
    'phone',
    'email',
    'flight_city',
    'hotel_check_in_date',
    'hotel_check_out_date',
    'arrival_ticket_no',
    'return_ticket_no',
    'insurance_company_name',
    'insurance_policy_no',
    'insurance_start_date',
    'insurance_end_date',
    'arrival_airline',
    'arrival_departure_airport',
    'arrival_arrival_airport',
    'arrival_flight_code',
    'arrival_departure_time',
    'arrival_arrival_time',
    'arrival_pnr',
    'arrival_baggage_pieces',
    'arrival_baggage_total_kg',
    'return_airline',
    'return_departure_airport',
    'return_arrival_airport',
    'return_flight_code',
    'return_departure_time',
    'return_arrival_time',
    'return_pnr',
    'return_baggage_pieces',
    'return_baggage_total_kg',
    'arrival_transfer_pickup_time',
    'arrival_transfer_pickup_place',
    'arrival_transfer_dropoff_place',
    'arrival_transfer_vehicle',
    'arrival_transfer_plate',
    'arrival_transfer_driver_info',
    'arrival_transfer_note',
    'return_transfer_pickup_time',
    'return_transfer_pickup_place',
    'return_transfer_dropoff_place',
    'return_transfer_vehicle',
    'return_transfer_plate',
    'return_transfer_driver_info',
    'return_transfer_note',
  ]

  const rows = participants.value.map((participant) => {
    const details = participant.details ?? {}
    return [
      details.roomNo ?? '',
      details.roomType ?? '',
      details.boardType ?? '',
      details.personNo ?? '',
      details.agencyName ?? '',
      details.city ?? '',
      participant.fullName,
      participant.birthDate,
      participant.tcNo,
      participant.gender,
      participant.phone ?? '',
      participant.email ?? '',
      details.flightCity ?? '',
      details.hotelCheckInDate ?? '',
      details.hotelCheckOutDate ?? '',
      details.arrivalTicketNo ?? details.ticketNo ?? '',
      details.returnTicketNo ?? '',
      details.insuranceCompanyName ?? '',
      details.insurancePolicyNo ?? '',
      details.insuranceStartDate ?? '',
      details.insuranceEndDate ?? '',
      details.arrivalAirline ?? '',
      details.arrivalDepartureAirport ?? '',
      details.arrivalArrivalAirport ?? '',
      details.arrivalFlightCode ?? '',
      details.arrivalDepartureTime ?? '',
      details.arrivalArrivalTime ?? '',
      details.arrivalPnr ?? '',
      details.arrivalBaggagePieces ?? '',
      details.arrivalBaggageTotalKg ?? '',
      details.returnAirline ?? '',
      details.returnDepartureAirport ?? '',
      details.returnArrivalAirport ?? '',
      details.returnFlightCode ?? '',
      details.returnDepartureTime ?? '',
      details.returnArrivalTime ?? '',
      details.returnPnr ?? '',
      details.returnBaggagePieces ?? '',
      details.returnBaggageTotalKg ?? '',
      details.arrivalTransferPickupTime ?? '',
      details.arrivalTransferPickupPlace ?? '',
      details.arrivalTransferDropoffPlace ?? '',
      details.arrivalTransferVehicle ?? '',
      details.arrivalTransferPlate ?? '',
      details.arrivalTransferDriverInfo ?? '',
      details.arrivalTransferNote ?? '',
      details.returnTransferPickupTime ?? '',
      details.returnTransferPickupPlace ?? '',
      details.returnTransferDropoffPlace ?? '',
      details.returnTransferVehicle ?? '',
      details.returnTransferPlate ?? '',
      details.returnTransferDriverInfo ?? '',
      details.returnTransferNote ?? '',
    ]
  })

  return { headers, rows }
}

const downloadParticipantsExcel = async () => {
  if (participants.value.length === 0) {
    return
  }

  const { headers, rows } = buildParticipantsExport()
  const { utils, writeFile } = await import('xlsx')
  const worksheet = utils.aoa_to_sheet([headers, ...rows])
  const workbook = utils.book_new()
  utils.book_append_sheet(workbook, worksheet, 'Participants')

  const name = event.value?.name?.trim().replace(/\s+/g, '-') || 'participants'
  writeFile(workbook, `${name}-participants.xlsx`)
}

const downloadBadgesPdf = async () => {
  try {
    // Build URL using the same helper as other API calls
    const apiBase = import.meta.env.VITE_API_BASE_URL?.trim()
    const baseUrl = apiBase
      ? apiBase.replace(/\/$/, '')
      : window.location.origin
    const url = `${baseUrl}/api/events/${eventId.value}/badges.pdf`

    // Fetch with authentication headers
    const token = getToken()
    const headers: Record<string, string> = {
      Accept: 'application/pdf',
    }
    if (token && !isTokenExpired(token)) {
      headers.Authorization = `Bearer ${token}`
      const role = getTokenRole(token)
      if (role === 'SuperAdmin') {
        const orgId = getSelectedOrgId()
        if (orgId) {
          headers['X-Org-Id'] = orgId
        }
      }
    }

    const response = await fetch(url, { headers })
    
    if (!response.ok) {
      if (response.status === 401) {
        pushToast({ key: 'toast.sessionExpired', tone: 'error' })
        return
      }
      throw new Error(`Failed to download PDF: ${response.statusText}`)
    }

    // Create blob and open in new tab
    const blob = await response.blob()
    const blobUrl = URL.createObjectURL(blob)
    window.open(blobUrl, '_blank')
    
    // Clean up blob URL after a delay
    setTimeout(() => URL.revokeObjectURL(blobUrl), 1000)
  } catch (error) {
    console.error('Failed to download badges PDF:', error)
    pushToast({ key: 'admin.participants.badgesError', tone: 'error' })
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

  const payload: EventPortalInfo = {
    meeting: {
      time: meetingTime,
      place: meetingPlace,
      mapsUrl: meetingMapsUrl,
      note: meetingNote,
    },
    links,
    days: [],
    notes,
  }

  portalSaving.value = true
  try {
    const saved = await apiPut<EventPortalInfo>(`/api/events/${eventId.value}/portal`, payload)
    portal.value = saved
    showPortalSaved()
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

const addGuide = () => {
  if (!selectedGuideToAdd.value || guideIds.value.includes(selectedGuideToAdd.value)) {
    selectedGuideToAdd.value = ''
    return
  }
  guideIds.value = [...guideIds.value, selectedGuideToAdd.value]
  selectedGuideToAdd.value = ''
}

const removeGuide = (guideId: string) => {
  guideIds.value = guideIds.value.filter(id => id !== guideId)
}

const getGuideName = (guideId: string) => {
  const guide = guides.value.find(g => g.id === guideId)
  return guide ? (guide.fullName || guide.email) : guideId
}

const availableGuides = computed(() => {
  return guides.value.filter(guide => !guideIds.value.includes(guide.id))
})

const saveGuides = async () => {
  guideErrorKey.value = null
  guideErrorMessage.value = null

  guideSaving.value = true
  try {
    await apiPut(`/api/events/${eventId.value}/guides`, {
      guideUserIds: guideIds.value,
    })
    if (event.value) {
      event.value = { ...event.value, guideUserIds: [...guideIds.value] }
    }
    pushToast({ key: 'toast.guidesAssigned', tone: 'success' })
  } catch (err) {
    guideErrorMessage.value = err instanceof Error ? err.message : null
    if (!guideErrorMessage.value) {
      guideErrorKey.value = 'errors.guides.assign'
    }
    pushToast({ key: 'toast.guidesAssignFailed', tone: 'error' })
  } finally {
    guideSaving.value = false
  }
}

watch(
  () => eventForm.startDate,
  (value) => {
    if (!value || !eventForm.endDate) {
      dateHintKey.value = null
      return
    }

    if (eventForm.endDate < value) {
      eventForm.endDate = value
      dateHintKey.value = 'validation.endDateAdjusted'
      return
    }

    dateHintKey.value = null
  }
)

watch(
  () => route.query.imported,
  async (value) => {
    if (!value) {
      return
    }

    await loadEvent()
    const nextQuery = { ...route.query }
    delete nextQuery.imported
    await router.replace({ path: route.path, query: nextQuery })
  }
)

onMounted(loadEvent)
</script>

<template>
  <div class="space-y-8">
    <div class="flex flex-col gap-4">
      <div class="flex flex-wrap items-center gap-2">
        <RouterLink
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-slate-50"
          to="/admin/events"
        >
          {{ t('nav.backToEvents') }}
        </RouterLink>
        <RouterLink
          v-if="isSuperAdmin"
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-slate-50"
          to="/admin/orgs"
        >
          {{ t('nav.backToOrganizations') }}
        </RouterLink>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <h1 class="text-2xl font-semibold">{{ event?.name ?? t('common.event') }}</h1>
        <span
          v-if="event?.isDeleted"
          class="rounded-full border border-rose-200 bg-rose-50 px-2 py-0.5 text-xs text-rose-700"
        >
          {{ t('common.archived') }}
        </span>
      </div>
      <p v-if="event" class="whitespace-nowrap text-sm text-slate-500">
        {{ t('common.dateRange', { start: event.startDate, end: event.endDate }) }}
      </p>
      <nav class="flex flex-wrap items-center gap-2" aria-label="Event sections">
        <RouterLink
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-slate-50"
          :to="`/admin/events/${eventId}/checkin`"
        >
          {{ t('common.checkIn') }}
        </RouterLink>
        <RouterLink
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-slate-50"
          :to="`/admin/events/${eventId}/program`"
        >
          {{ t('admin.eventDetail.openProgram') }}
        </RouterLink>
        <RouterLink
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-slate-50"
          :to="`/admin/events/${eventId}/activities/checkin`"
        >
          {{ t('admin.eventDetail.activityCheckIn') }}
        </RouterLink>
        <RouterLink
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm transition-colors hover:border-slate-300 hover:bg-slate-50"
          :to="`/admin/events/${eventId}/equipment`"
        >
          {{ t('admin.eventDetail.equipment') }}
        </RouterLink>
        <button
          v-if="event && !event.isDeleted"
          class="whitespace-nowrap rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800 shadow-sm hover:border-amber-300"
          :disabled="archivingEvent"
          type="button"
          @click="archiveEvent"
        >
          {{ archivingEvent ? t('common.saving') : t('common.archive') }}
        </button>
        <button
          v-else-if="event"
          class="whitespace-nowrap rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700 shadow-sm hover:border-emerald-300"
          :disabled="restoringEvent"
          type="button"
          @click="restoreEvent"
        >
          {{ restoringEvent ? t('common.saving') : t('common.restore') }}
        </button>
        <a
          class="whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
          :href="buildPortalLoginLink(event?.eventAccessCode ?? undefined) || '/e/login'"
          rel="noreferrer"
          target="_blank"
        >
          {{ t('admin.eventDetail.openPortal') }}
        </a>
      </nav>
    </div>

    <LoadingState v-if="loading" message-key="admin.eventDetail.loading" />
    <ErrorState
      v-else-if="loadErrorKey || loadErrorMessage"
      :message="loadErrorMessage ?? undefined"
      :message-key="loadErrorKey ?? undefined"
      @retry="loadEvent"
    />

    <template v-else>
      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div class="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 class="text-lg font-semibold">{{ t('admin.eventDetail.detailsTitle') }}</h2>
            <p class="text-sm text-slate-500">{{ t('admin.eventDetail.detailsSubtitle') }}</p>
          </div>
        </div>

        <form class="mt-4 space-y-4" @submit.prevent="saveEvent">
          <fieldset class="grid gap-4 md:grid-cols-3" :disabled="eventSaving">
            <label class="grid gap-1 text-sm md:col-span-1">
              <span class="text-slate-600">{{ t('admin.eventDetail.form.nameLabel') }}</span>
              <input
                v-model.trim="eventForm.name"
                class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :placeholder="t('admin.eventDetail.form.namePlaceholder')"
                type="text"
              />
            </label>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.eventDetail.form.startDateLabel') }}</span>
              <input
                v-model="eventForm.startDate"
                class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                type="date"
              />
            </label>
            <label class="grid gap-1 text-sm">
              <span class="text-slate-600">{{ t('admin.eventDetail.form.endDateLabel') }}</span>
              <input
                v-model="eventForm.endDate"
                class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                :min="eventForm.startDate"
                type="date"
              />
            </label>
          </fieldset>
          <div class="flex flex-wrap items-center gap-3">
            <button
              class="inline-flex items-center gap-2 rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="eventSaving"
              type="submit"
            >
              <span v-if="eventSaving" class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"></span>
              {{ eventSaving ? t('common.saving') : t('admin.eventDetail.form.save') }}
            </button>
            <span v-if="eventMessageKey" class="text-xs text-emerald-600">{{ t(eventMessageKey) }}</span>
            <span v-if="eventErrorKey || eventErrorMessage" class="text-xs text-rose-600">
              {{ eventErrorKey ? t(eventErrorKey) : eventErrorMessage }}
            </span>
            <span v-if="dateHintKey" class="text-xs text-slate-500">{{ t(dateHintKey) }}</span>
          </div>
        </form>
      </section>

      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div class="flex flex-col gap-2">
          <h2 class="text-lg font-semibold">{{ t('admin.eventAccess.title') }}</h2>
          <p class="text-sm text-slate-500">{{ t('admin.eventAccess.subtitle') }}</p>
        </div>

        <div class="mt-4 grid gap-3">
          <div class="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
            <div class="text-xs uppercase tracking-[0.2em] text-slate-400">{{ t('admin.eventAccess.codeLabel') }}</div>
            <div class="mt-1 font-mono text-lg tracking-[0.2em] text-slate-800">
              {{ event?.eventAccessCode || t('admin.eventAccess.codeMissing') }}
            </div>
          </div>

          <div class="flex flex-wrap items-center gap-2">
            <button
              class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
              type="button"
              :disabled="!event?.eventAccessCode"
              @click="copyEventAccessCode"
            >
              {{ t('admin.eventAccess.copyCode') }}
            </button>
            <button
              class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
              type="button"
              @click="copyPortalLoginLink"
            >
              {{ t('admin.eventAccess.copyLoginLink') }}
            </button>
            <button
              class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
              type="button"
              @click="openEditCodeModal"
            >
              {{ t('admin.eventAccess.editCode') }}
            </button>
            <button
              class="rounded border border-amber-200 bg-amber-50 px-3 py-1.5 text-xs font-medium text-amber-700 hover:border-amber-300 disabled:cursor-not-allowed disabled:opacity-50"
              type="button"
              :disabled="accessCodeLoading"
              @click="regenerateEventAccessCode"
            >
              {{ accessCodeLoading ? t('common.saving') : t('admin.eventAccess.regenerate') }}
            </button>
          </div>

          <p v-if="accessCodeMessageKey" class="text-xs text-emerald-600">{{ t(accessCodeMessageKey) }}</p>
          <p v-if="accessCodeErrorKey" class="text-xs text-rose-600">{{ t(accessCodeErrorKey) }}</p>
        </div>
      </section>

      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <h2 class="text-lg font-semibold">{{ t('admin.eventDetail.guide.title') }}</h2>
            <p class="text-sm text-slate-500">{{ t('admin.eventDetail.guide.subtitle') }}</p>
          </div>
        </div>

        <div v-if="guideLoading" class="mt-4 text-sm text-slate-500">{{ t('admin.eventDetail.guide.loading') }}</div>
        <div v-else-if="guides.length === 0" class="mt-4 text-sm text-slate-500">
          {{ t('admin.eventDetail.guide.empty') }}
        </div>
        <div v-else class="mt-4 space-y-4">
          <!-- Selected guides as chips -->
          <div v-if="guideIds.length > 0" class="flex flex-wrap gap-2">
            <div
              v-for="guideId in guideIds"
              :key="guideId"
              class="group inline-flex items-center gap-2 rounded-full bg-slate-100 px-3 py-1.5 text-sm text-slate-700 transition-colors hover:bg-slate-200"
            >
              <span class="font-medium">{{ getGuideName(guideId) }}</span>
              <button
                type="button"
                @click="removeGuide(guideId)"
                class="flex h-5 w-5 items-center justify-center rounded-full text-slate-500 transition-colors hover:bg-slate-300 hover:text-slate-700 focus:outline-none focus:ring-2 focus:ring-slate-400 focus:ring-offset-1"
                :aria-label="t('admin.eventDetail.guide.removeGuide')"
              >
                <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
          </div>

          <!-- Add guide dropdown -->
          <div class="flex flex-col gap-3 sm:flex-row sm:items-start">
            <select
              v-model="selectedGuideToAdd"
              @change="addGuide"
              class="w-full rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none focus:ring-2 focus:ring-slate-400 sm:w-auto"
            >
              <option value="">{{ t('admin.eventDetail.guide.selectPlaceholder') }}</option>
              <option v-for="guide in availableGuides" :key="guide.id" :value="guide.id">
                {{ guide.fullName || guide.email }}
              </option>
            </select>
            <button
              type="button"
              @click="saveGuides"
              class="inline-flex items-center justify-center rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
              :disabled="guideSaving"
            >
              <span v-if="guideSaving" class="mr-2 h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"></span>
              {{ guideSaving ? t('common.saving') : t('admin.eventDetail.guide.save') }}
            </button>
          </div>

          <!-- Error message -->
          <div v-if="guideErrorKey || guideErrorMessage" class="text-xs text-rose-600">
            {{ guideErrorKey ? t(guideErrorKey) : guideErrorMessage }}
          </div>
        </div>
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
            <span class="text-slate-600">{{ t('admin.participants.form.tcNo') }}</span>
            <input
              v-model.trim="form.tcNo"
              class="rounded border bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :class="tcNoErrorKey ? 'border-rose-300' : 'border-slate-200'"
              :placeholder="t('admin.participants.form.tcNoPlaceholder')"
              inputmode="numeric"
              type="text"
              @input="handleTcNoInput"
            />
          </label>
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.participants.form.birthDate') }}</span>
            <input
              v-model="form.birthDate"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="date"
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
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.participants.form.gender') }}</span>
            <select
              v-model="form.gender"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            >
              <option value="" disabled>{{ t('admin.participants.form.genderPlaceholder') }}</option>
              <option v-for="option in genderOptions" :key="option.value" :value="option.value">
                {{ t(option.label) }}
              </option>
            </select>
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
        <p v-if="tcNoErrorKey" class="mt-2 text-sm text-rose-600">{{ t(tcNoErrorKey) }}</p>
        <p v-if="formErrorKey || formErrorMessage" class="mt-3 text-sm text-rose-600">
          {{ formErrorKey ? t(formErrorKey) : formErrorMessage }}
        </p>
      </section>

      <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 class="text-lg font-semibold">{{ t('admin.portal.title') }}</h2>
            <span class="text-xs text-slate-500">{{ t('admin.portal.subtitle') }}</span>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <RouterLink
              class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
              :to="`/admin/events/${eventId}/program`"
            >
              {{ t('admin.eventDetail.openProgram') }}
            </RouterLink>
            <RouterLink
              class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
              :to="`/admin/events/${eventId}/docs/tabs`"
            >
              {{ t('admin.portal.docsTabs') }}
            </RouterLink>
          </div>
        </div>

        <form class="mt-4 space-y-6" @submit.prevent="savePortal">
          <fieldset class="space-y-6" :disabled="portalSaving">
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
          </fieldset>

          <div class="flex flex-wrap items-center gap-3">
            <button
              class="inline-flex items-center gap-2 rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
              :disabled="portalSaving"
              type="submit"
            >
              <span v-if="portalSaving" class="h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"></span>
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
        <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <h2 class="text-lg font-semibold">{{ t('admin.participants.title') }}</h2>
          <div class="flex flex-wrap items-center gap-2 md:justify-end md:gap-3">
            <button
              class="rounded border border-slate-200 bg-white px-2 py-1 text-xs font-medium leading-tight text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50 md:px-3 md:py-1.5"
              type="button"
              :disabled="participants.length === 0 || resettingAllCheckIns"
              @click="requestResetAllCheckIns"
            >
              {{ resettingAllCheckIns ? t('common.saving') : t('admin.participants.resetAll') }}
            </button>
            <button
              class="rounded border border-rose-200 bg-rose-50 px-2 py-1 text-xs font-medium leading-tight text-rose-700 hover:border-rose-300 disabled:cursor-not-allowed disabled:opacity-50 md:px-3 md:py-1.5"
              type="button"
              :disabled="participants.length === 0 || deletingAllParticipants"
              @click="requestDeleteAllParticipants"
            >
              {{ deletingAllParticipants ? t('common.saving') : t('admin.participants.deleteAll') }}
            </button>
            <RouterLink
              class="rounded border border-slate-200 bg-white px-2 py-1 text-xs font-medium leading-tight text-slate-700 hover:border-slate-300 md:px-3 md:py-1.5"
              :to="`/admin/events/${eventId}/participants/import`"
            >
              {{ t('admin.participants.import') }}
            </RouterLink>
            <RouterLink
              class="rounded border border-slate-200 bg-white px-2 py-1 text-xs font-medium leading-tight text-slate-700 hover:border-slate-300 md:px-3 md:py-1.5"
              :to="`/admin/events/${eventId}/participants/table`"
            >
              {{ t('admin.participants.tableView') }}
            </RouterLink>
            <span class="text-xs text-slate-500">{{ participants.length }} {{ t('common.total') }}</span>
            <button
              class="rounded border border-slate-200 bg-white px-2 py-1 text-xs font-medium leading-tight text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50 md:px-3 md:py-1.5"
              type="button"
              :disabled="participants.length === 0"
              @click="downloadParticipantsExcel"
            >
              {{ t('admin.participants.exportExcel') }}
            </button>
            <button
              class="rounded border border-slate-200 bg-white px-2 py-1 text-xs font-medium leading-tight text-slate-700 hover:border-slate-300 disabled:cursor-not-allowed disabled:opacity-50 md:px-3 md:py-1.5"
              type="button"
              :disabled="participants.length === 0"
              @click="downloadBadgesPdf"
            >
              {{ t('admin.participants.badges') }}
            </button>
          </div>
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
                  <span class="text-slate-600">{{ t('admin.participants.form.tcNo') }}</span>
                  <input
                    v-model.trim="editForm.tcNo"
                    class="rounded border bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    :class="editTcNoErrorKey ? 'border-rose-300' : 'border-slate-200'"
                    inputmode="numeric"
                    type="text"
                    @input="handleEditTcNoInput"
                  />
                  <span v-if="tcNoWarnings[participant.id]" class="text-xs text-amber-600">
                    {{ t('warnings.tcNoDuplicate') }}
                  </span>
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participants.form.birthDate') }}</span>
                  <input
                    v-model="editForm.birthDate"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    type="date"
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
                    ref="editPhoneInput"
                    class="rounded border bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                    :class="
                      editPhoneErrorKey
                        ? 'border-rose-300'
                        : missingPhoneParticipantId === participant.id
                          ? 'border-amber-300'
                          : 'border-slate-200'
                    "
                    :placeholder="t('admin.participants.form.phonePlaceholder')"
                    inputmode="tel"
                    maxlength="15"
                    type="tel"
                    @input="handleEditPhoneInput"
                    @blur="handleEditPhoneBlur"
                  />
                </label>
                <label class="grid gap-1 text-sm">
                  <span class="text-slate-600">{{ t('admin.participants.form.gender') }}</span>
                  <select
                    v-model="editForm.gender"
                    class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
                  >
                    <option value="" disabled>{{ t('admin.participants.form.genderPlaceholder') }}</option>
                    <option v-for="option in genderOptions" :key="option.value" :value="option.value">
                      {{ t(option.label) }}
                    </option>
                  </select>
                </label>
              </div>
              <div class="space-y-3">
                <div class="text-sm font-semibold text-slate-700">{{ t('admin.participants.details.title') }}</div>
                <div class="grid gap-3 md:grid-cols-3">
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.roomNo') }}</span>
                    <input v-model.trim="editDetails.roomNo" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.roomType') }}</span>
                    <input v-model.trim="editDetails.roomType" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.personNo') }}</span>
                    <input v-model.trim="editDetails.personNo" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.agencyName') }}</span>
                    <input v-model.trim="editDetails.agencyName" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.city') }}</span>
                    <input v-model.trim="editDetails.city" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.flightCity') }}</span>
                    <input v-model.trim="editDetails.flightCity" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.hotelCheckInDate') }}</span>
                    <input v-model.trim="editDetails.hotelCheckInDate" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.hotelCheckOutDate') }}</span>
                    <input v-model.trim="editDetails.hotelCheckOutDate" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalTicketNo') }}</span>
                    <input v-model.trim="editDetails.arrivalTicketNo" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnTicketNo') }}</span>
                    <input v-model.trim="editDetails.returnTicketNo" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.attendanceStatus') }}</span>
                    <input v-model.trim="editDetails.attendanceStatus" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                </div>
                <div class="text-sm font-semibold text-slate-700">{{ t('admin.participants.details.arrivalTitle') }}</div>
                <div class="grid gap-3 md:grid-cols-3">
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalAirline') }}</span>
                    <input v-model.trim="editDetails.arrivalAirline" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalDepartureAirport') }}</span>
                    <input v-model.trim="editDetails.arrivalDepartureAirport" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalArrivalAirport') }}</span>
                    <input v-model.trim="editDetails.arrivalArrivalAirport" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalFlightCode') }}</span>
                    <input v-model.trim="editDetails.arrivalFlightCode" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalDepartureTime') }}</span>
                    <input v-model.trim="editDetails.arrivalDepartureTime" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalArrivalTime') }}</span>
                    <input v-model.trim="editDetails.arrivalArrivalTime" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalPnr') }}</span>
                    <input v-model.trim="editDetails.arrivalPnr" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalBaggageAllowance') }}</span>
                    <input v-model.trim="editDetails.arrivalBaggageAllowance" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.cabinBaggage') }}</span>
                    <input v-model.trim="editDetails.arrivalCabinBaggage" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                </div>
                <div class="text-sm font-semibold text-slate-700">{{ t('admin.participants.details.returnTitle') }}</div>
                <div class="grid gap-3 md:grid-cols-3">
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnAirline') }}</span>
                    <input v-model.trim="editDetails.returnAirline" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnDepartureAirport') }}</span>
                    <input v-model.trim="editDetails.returnDepartureAirport" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnArrivalAirport') }}</span>
                    <input v-model.trim="editDetails.returnArrivalAirport" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnFlightCode') }}</span>
                    <input v-model.trim="editDetails.returnFlightCode" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnDepartureTime') }}</span>
                    <input v-model.trim="editDetails.returnDepartureTime" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnArrivalTime') }}</span>
                    <input v-model.trim="editDetails.returnArrivalTime" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnPnr') }}</span>
                    <input v-model.trim="editDetails.returnPnr" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnBaggageAllowance') }}</span>
                    <input v-model.trim="editDetails.returnBaggageAllowance" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.cabinBaggage') }}</span>
                    <input v-model.trim="editDetails.returnCabinBaggage" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                </div>
                <div class="text-sm font-semibold text-slate-700">{{ t('admin.participants.details.transferTitle') }}</div>
                <div class="text-sm font-semibold text-slate-600">{{ t('admin.participants.details.arrivalTransferTitle') }}</div>
                <div class="grid gap-3 md:grid-cols-3">
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalTransferPickupTime') }}</span>
                    <input v-model.trim="editDetails.arrivalTransferPickupTime" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalTransferPickupPlace') }}</span>
                    <input v-model.trim="editDetails.arrivalTransferPickupPlace" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalTransferDropoffPlace') }}</span>
                    <input v-model.trim="editDetails.arrivalTransferDropoffPlace" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalTransferVehicle') }}</span>
                    <input v-model.trim="editDetails.arrivalTransferVehicle" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalTransferPlate') }}</span>
                    <input v-model.trim="editDetails.arrivalTransferPlate" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalTransferDriverInfo') }}</span>
                    <input v-model.trim="editDetails.arrivalTransferDriverInfo" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm md:col-span-3">
                    <span class="text-slate-600">{{ t('admin.participants.details.arrivalTransferNote') }}</span>
                    <input v-model.trim="editDetails.arrivalTransferNote" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                </div>
                <div class="text-sm font-semibold text-slate-600">{{ t('admin.participants.details.returnTransferTitle') }}</div>
                <div class="grid gap-3 md:grid-cols-3">
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnTransferPickupTime') }}</span>
                    <input v-model.trim="editDetails.returnTransferPickupTime" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnTransferPickupPlace') }}</span>
                    <input v-model.trim="editDetails.returnTransferPickupPlace" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnTransferDropoffPlace') }}</span>
                    <input v-model.trim="editDetails.returnTransferDropoffPlace" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnTransferVehicle') }}</span>
                    <input v-model.trim="editDetails.returnTransferVehicle" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnTransferPlate') }}</span>
                    <input v-model.trim="editDetails.returnTransferPlate" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnTransferDriverInfo') }}</span>
                    <input v-model.trim="editDetails.returnTransferDriverInfo" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                  <label class="grid gap-1 text-sm md:col-span-3">
                    <span class="text-slate-600">{{ t('admin.participants.details.returnTransferNote') }}</span>
                    <input v-model.trim="editDetails.returnTransferNote" class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none" type="text" />
                  </label>
                </div>
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
                <span v-if="editTcNoErrorKey" class="text-xs text-rose-600">
                  {{ t(editTcNoErrorKey) }}
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
                <a
                  class="inline-flex items-center gap-1.5 rounded border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-xs font-medium text-emerald-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                  :class="
                    !participant.phone
                      ? 'cursor-not-allowed opacity-50'
                      : 'hover:border-emerald-300 hover:bg-emerald-100'
                  "
                  :aria-label="t('actions.whatsappAria')"
                  :aria-disabled="!participant.phone"
                  rel="noreferrer"
                  target="_blank"
                  @click.prevent="openWhatsApp(participant)"
                >
                  <WhatsAppIcon class="text-emerald-700" :size="14" />
                  <span>{{ t('actions.whatsapp') }}</span>
                </a>
                <RouterLink
                  class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
                  :to="`/admin/events/${eventId}/participants/${participant.id}`"
                >
                  {{ t('admin.participants.detailsButton') }}
                </RouterLink>
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
              <p
                v-if="missingPhoneParticipantId === participant.id && !participant.phone"
                class="text-xs text-amber-600"
              >
                {{ t('warnings.phoneRequiredForWhatsapp') }}
              </p>
            </div>
          </li>
        </ul>
      </section>

      <section class="rounded-lg border border-rose-200 bg-rose-50 p-6 shadow-sm">
        <div class="flex flex-col gap-2">
          <h2 class="text-lg font-semibold text-rose-800">{{ t('common.dangerZone') }}</h2>
          <p class="text-sm text-rose-700">{{ t('admin.eventDetail.purgeWarning') }}</p>
        </div>

        <div class="mt-4 space-y-3">
          <p v-if="event && !event.isDeleted" class="text-sm text-rose-700">
            {{ t('admin.eventDetail.purgeRequiresArchive') }}
          </p>
          <label class="grid gap-1 text-sm text-rose-800">
            <span>{{ t('admin.eventDetail.purgeConfirmLabel') }}</span>
            <input
              v-model.trim="purgeConfirmText"
              class="rounded border border-rose-200 bg-white px-3 py-2 text-sm focus:border-rose-400 focus:outline-none"
              :placeholder="t('admin.eventDetail.purgeConfirmPlaceholder')"
              type="text"
            />
          </label>
          <p v-if="purgeErrorKey" class="text-sm text-rose-700">{{ t(purgeErrorKey) }}</p>
          <button
            class="rounded border border-rose-200 bg-rose-600 px-4 py-2 text-sm font-semibold text-white hover:bg-rose-700 disabled:cursor-not-allowed disabled:opacity-60"
            :disabled="purgingEvent || !event?.isDeleted"
            type="button"
            @click="purgeEvent"
          >
            {{ purgingEvent ? t('common.saving') : t('common.purge') }}
          </button>
        </div>
      </section>
    </template>
  </div>

  <Teleport to="body">
    <div
      v-if="editCodeModalOpen"
      class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      @click.self="closeEditCodeModal"
    >
      <div
        class="max-h-[90vh] w-full max-w-md overflow-y-auto rounded-xl border border-slate-200 bg-white p-6 shadow-lg"
        role="dialog"
        aria-labelledby="edit-code-title"
      >
        <h2 id="edit-code-title" class="text-lg font-semibold text-slate-900">{{ t('admin.eventAccess.editCodeModalTitle') }}</h2>
        <p class="mt-2 text-sm text-amber-700">{{ t('admin.eventAccess.editCodeWarning') }}</p>
        <label class="mt-4 grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.eventAccess.editCodeLabel') }}</span>
          <input
            v-model="editCodeValue"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm font-mono uppercase tracking-wider focus:border-slate-400 focus:outline-none"
            :class="editCodeErrorKey ? 'border-rose-400' : ''"
            :placeholder="t('admin.eventAccess.editCodePlaceholder')"
            maxlength="10"
            autocomplete="off"
            autocapitalize="characters"
            :disabled="editCodeSaving"
            @input="editCodeValue = sanitizeEventAccessCode(editCodeValue); editCodeErrorKey = null"
          />
        </label>
        <p v-if="editCodeErrorKey" class="mt-2 text-sm text-rose-600">{{ t(editCodeErrorKey) }}</p>
        <div class="mt-6 flex justify-end gap-2">
          <button
            type="button"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300"
            :disabled="editCodeSaving"
            @click="closeEditCodeModal"
          >
            {{ t('common.cancel') }}
          </button>
          <button
            type="button"
            class="rounded bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            :disabled="editCodeSaving"
            @click="saveEditCode"
          >
            {{ editCodeSaving ? t('common.saving') : t('admin.eventAccess.editCodeSave') }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>

  <ConfirmDialog
    v-model:open="confirmOpen"
    :title="t('common.confirm')"
    :message="confirmMessageKey ? t(confirmMessageKey) : ''"
    :confirm-label="confirmTone === 'danger' ? t('common.delete') : t('common.confirm')"
    :cancel-label="t('common.cancel')"
    :tone="confirmTone"
    @confirm="handleConfirm"
  />
</template>
