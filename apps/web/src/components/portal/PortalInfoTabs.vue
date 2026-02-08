<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatBaggage, formatTime } from '../../lib/formatters'
import { formatPhoneDisplay, normalizePhone } from '../../lib/normalize'
import { useToast } from '../../lib/toast'
import CopyIcon from '../icons/CopyIcon.vue'
import type { PortalDocsResponse, PortalFlightInfo, PortalInsuranceInfo, PortalTransferInfo } from '../../types'

type TabItem = {
  id: string
  label: string
  kind: 'flight' | 'hotel' | 'insurance' | 'transfer' | 'custom'
  content?: unknown
}

const props = defineProps<{ docs?: PortalDocsResponse | null; printMode?: boolean }>()

const { t } = useI18n()
const { pushToast } = useToast()

const copyToClipboard = async (value: string) => {
  if (!value?.trim()) return
  try {
    if (globalThis.navigator?.clipboard?.writeText) {
      await globalThis.navigator.clipboard.writeText(value.trim())
      pushToast({ key: 'toast.copied', tone: 'success' })
    }
  } catch {
    pushToast({ key: 'errors.copyFailed', tone: 'error' })
  }
}

const travel = computed(() => props.docs?.participantTravel ?? null)

const normalizeType = (value?: string | null) => (value ?? '').trim().toLowerCase()

const hotelTab = computed(() => props.docs?.tabs.find((tab) => normalizeType(tab.type) === 'hotel') ?? null)
const transferTabs = computed(() =>
  [...(props.docs?.tabs ?? [])]
    .filter((tab) => normalizeType(tab.type) === 'transfer')
    .sort((a, b) => a.sortOrder - b.sortOrder)
)

const customTabs = computed(() =>
  [...(props.docs?.tabs ?? [])]
    .filter((tab) => !['hotel', 'insurance', 'transfer'].includes(normalizeType(tab.type)))
    .sort((a, b) => a.sortOrder - b.sortOrder)
)

const isFlightLikeTab = (tab: { title: string; type: string }) => {
  const normalizedType = normalizeType(tab.type)
  const normalizedTitle = normalizeType(tab.title)
  const titleMatches =
    normalizedTitle.includes('flight') ||
    normalizedTitle.includes('ucus') ||
    normalizedTitle.includes('uçuş')
  return normalizedType === 'flight' || normalizedType === 'ucus' || normalizedType === 'uçuş' || titleMatches
}

const printCustomTabs = computed(() => customTabs.value.filter((tab) => !isFlightLikeTab(tab)))

const tabs = computed<TabItem[]>(() => [
  { id: 'flight', label: t('portal.infoTabs.flight'), kind: 'flight' },
  { id: 'hotel', label: t('portal.infoTabs.hotel'), kind: 'hotel' },
  { id: 'insurance', label: t('portal.infoTabs.insurance'), kind: 'insurance' },
  ...transferTabs.value.map((tab) => ({
    id: tab.id,
    label: tab.title,
    kind: 'transfer' as const,
    content: tab.content,
  })),
  ...customTabs.value.map((tab) => ({
    id: tab.id,
    label: tab.title,
    kind: 'custom' as const,
    content: tab.content,
  })),
])

const activeTabId = ref('flight')

watch(
  tabs,
  (value) => {
  if (props.printMode) {
    return
  }
  if (!value.some((tab) => tab.id === activeTabId.value)) {
    activeTabId.value = value[0]?.id ?? 'flight'
  }
  },
  { immediate: true }
)

const activeTab = computed(() => tabs.value.find((tab) => tab.id === activeTabId.value) ?? null)

const valueOrDash = (value?: string | null) => {
  if (!value) return '—'
  const trimmed = value.trim()
  return trimmed ? trimmed : '—'
}

const hasText = (value?: string | null) => {
  if (!value) return false
  return Boolean(value.trim())
}

const formatDate = (value?: string | null) => {
  if (!value) return '—'
  const trimmed = value.trim()
  if (!trimmed) return '—'
  const datePart = trimmed.includes('T')
    ? trimmed.split('T')[0] ?? ''
    : trimmed.split(' ')[0] ?? ''
  const [year, month, day] = datePart.split('-')
  if (year && month && day) {
    return `${day}.${month}.${year}`
  }
  return trimmed
}

const boardLabels = computed(() => ({
  RO: t('portal.docs.board.roomOnly'),
  BB: t('portal.docs.board.bb'),
  HB: t('portal.docs.board.hb'),
  FB: t('portal.docs.board.fb'),
  AI: t('portal.docs.board.ai'),
  UAI: t('portal.docs.board.uai'),
}))

const formatBoard = (value?: string | null) => {
  if (!value) return '—'
  const trimmed = value.trim()
  if (!trimmed) return '—'
  const normalized = trimmed.toUpperCase()
  return boardLabels.value[normalized as keyof typeof boardLabels.value] ?? trimmed
}

const buildMapsLink = (value?: string | null) => {
  if (!value) return ''
  const trimmed = value.trim()
  if (!trimmed) return ''
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(trimmed)}`
}

const hasFlightValue = (flight?: PortalFlightInfo | null) => {
  if (!flight) return false
  return Boolean(
    flight.airline?.trim() ||
      flight.departureAirport?.trim() ||
      flight.arrivalAirport?.trim() ||
      flight.flightCode?.trim() ||
      flight.departureTime?.trim() ||
      flight.arrivalTime?.trim() ||
      flight.pnr?.trim() ||
      (typeof flight.baggagePieces === 'number' && flight.baggagePieces > 0) ||
      (typeof flight.baggageTotalKg === 'number' && flight.baggageTotalKg > 0)
  )
}

const hasInsuranceValue = (insurance?: PortalInsuranceInfo | null) => {
  if (!insurance) return false
  return Boolean(
    insurance.companyName?.trim() ||
      insurance.policyNo?.trim() ||
      insurance.startDate?.trim() ||
      insurance.endDate?.trim()
  )
}

const hasTransferValue = (transfer?: PortalTransferInfo | null) => {
  if (!transfer) return false
  return Boolean(
    transfer.pickupTime?.trim() ||
      transfer.pickupPlace?.trim() ||
      transfer.dropoffPlace?.trim() ||
      transfer.vehicle?.trim() ||
      transfer.plate?.trim() ||
      transfer.driverInfo?.trim() ||
      transfer.note?.trim()
  )
}

const toObject = (content: unknown): Record<string, unknown> | null => {
  if (!content || typeof content !== 'object' || Array.isArray(content)) {
    return null
  }
  return content as Record<string, unknown>
}

const readString = (content: Record<string, unknown> | null, key: string) => {
  if (!content) return ''
  const value = content[key]
  if (typeof value !== 'string') return ''
  return value.trim()
}

const readContentString = (content: unknown, key: string) => {
  const obj = toObject(content)
  if (!obj) return ''
  const value = obj[key]
  if (typeof value !== 'string') return ''
  return value.trim()
}

const contentValue = (content: unknown, key: string) => valueOrDash(readContentString(content, key))

const buildTelLink = (value?: string | null) => {
  if (!value) return ''
  const { normalized } = normalizePhone(value)
  return normalized ? `tel:${normalized}` : ''
}

const contentPhone = (content: unknown) => {
  const raw = readContentString(content, 'phone')
  const display = raw ? formatPhoneDisplay(raw) || raw : ''
  return {
    raw,
    display: display || raw,
    link: buildTelLink(raw),
  }
}

const parseTransferContent = (content: unknown) => {
  const obj = toObject(content)
  if (!obj) {
    return { arrival: null as PortalTransferInfo | null, return: null as PortalTransferInfo | null }
  }

  const arrivalObj = toObject(obj.arrival)
  const returnObj = toObject(obj.return)

  const build = (section: Record<string, unknown> | null, prefix: string) => {
    const pickupTime = readString(section, 'pickupTime') || readString(obj, `${prefix}PickupTime`)
    const pickupPlace = readString(section, 'pickupPlace') || readString(obj, `${prefix}PickupPlace`)
    const dropoffPlace = readString(section, 'dropoffPlace') || readString(obj, `${prefix}DropoffPlace`)
    const vehicle = readString(section, 'vehicle') || readString(obj, `${prefix}Vehicle`)
    const plate = readString(section, 'plate') || readString(obj, `${prefix}Plate`)
    const driverInfo = readString(section, 'driverInfo') || readString(obj, `${prefix}DriverInfo`)
    const note = readString(section, 'note') || readString(obj, `${prefix}Note`)

    const info: PortalTransferInfo = {
      pickupTime,
      pickupPlace,
      dropoffPlace,
      vehicle,
      plate,
      driverInfo,
      note,
    }

    return hasTransferValue(info) ? info : null
  }

  return {
    arrival: build(arrivalObj, 'arrival'),
    return: build(returnObj, 'return'),
  }
}

const resolveTransferData = (content?: unknown) => {
  const parsed = parseTransferContent(content)
  const outbound = hasTransferValue(travel.value?.transferOutbound)
    ? travel.value?.transferOutbound
    : parsed.arrival
  const ret = hasTransferValue(travel.value?.transferReturn)
    ? travel.value?.transferReturn
    : parsed.return
  return { outbound, return: ret, hasAny: hasTransferValue(outbound) || hasTransferValue(ret) }
}

const getCustomText = (content: unknown) => {
  const obj = toObject(content)
  if (!obj) return ''
  const text = obj.text
  if (typeof text !== 'string') return ''
  return text.trim()
}

const getCustomFields = (content: unknown) => {
  const obj = toObject(content)
  if (!obj) return [] as { label: string; value: string }[]
  const fields: { label: string; value: string }[] = []

  if (Array.isArray(obj.fields)) {
    obj.fields.forEach((field) => {
      if (!field || typeof field !== 'object') return
      const label = typeof field.label === 'string' ? field.label.trim() : ''
      const value = typeof field.value === 'string' ? field.value.trim() : field.value?.toString?.().trim?.() ?? ''
      if (!label || !value) return
      fields.push({ label, value })
    })
    return fields
  }

  Object.entries(obj)
    .filter(([key]) => !['text', 'html', 'fields'].includes(key))
    .forEach(([key, value]) => {
      if (value === null || value === undefined) return
      const label = key
        .replace(/_/g, ' ')
        .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
        .replace(/\b\w/g, (char) => char.toUpperCase())
      const stringValue = typeof value === 'string' ? value.trim() : value.toString?.().trim?.() ?? ''
      if (!label || !stringValue) return
      fields.push({ label, value: stringValue })
    })

  return fields
}

const isPhoneField = (label: string, value: string) => {
  const normalizedLabel = label.toLowerCase()
  if (/(telefon|phone|tel|gsm|whatsapp)/i.test(normalizedLabel)) {
    return true
  }
  const { normalized } = normalizePhone(value)
  return normalized.length >= 8
}

const isAddressField = (label: string) => {
  const normalized = label.toLowerCase()
  return /(adres|address|location|konum|meeting|buluşma|toplanma|yer)/i.test(normalized)
}

const formatCustomValue = (value: unknown): string => {
  if (value === null || value === undefined) return '—'
  if (typeof value === 'string') {
    const trimmed = value.trim()
    return trimmed ? trimmed : '—'
  }
  if (typeof value === 'number' || typeof value === 'boolean') {
    return String(value)
  }
  if (Array.isArray(value)) {
    const items: string[] = value
      .map((item) => formatCustomValue(item))
      .filter((item) => item !== '—')
    return items.length > 0 ? items.join(', ') : '—'
  }
  try {
    return JSON.stringify(value)
  } catch {
    return '—'
  }
}
</script>

<template>
  <div class="space-y-4">
    <div v-if="!printMode" class="flex gap-2 overflow-x-auto pb-1">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        class="whitespace-nowrap rounded-full border px-3 py-1.5 text-xs font-semibold transition"
        :class="
          activeTabId === tab.id
            ? 'border-slate-900 bg-slate-900 text-white'
            : 'border-slate-200 text-slate-600 hover:border-slate-300'
        "
        type="button"
        @click="activeTabId = tab.id"
      >
        <span class="inline-flex items-center gap-1.5">
          <span v-if="tab.kind === 'flight'" class="inline-flex h-4 w-4 items-center justify-center">
            <svg
              viewBox="-2.5 0 19 19"
              class="h-3.5 w-3.5 text-slate-900"
              fill="white"
              stroke="currentColor"
              stroke-width="1.2"
              stroke-linecap="round"
              stroke-linejoin="round"
              vector-effect="non-scaling-stroke"
              paint-order="stroke fill"
              aria-hidden="true"
            >
              <path d="M12.382 5.304 10.096 7.59l.006.02L11.838 14a.908.908 0 0 1-.211.794l-.573.573a.339.339 0 0 1-.566-.08l-2.348-4.25-.745-.746-1.97 1.97a3.311 3.311 0 0 1-.75.504l.44 1.447a.875.875 0 0 1-.199.79l-.175.176a.477.477 0 0 1-.672 0l-1.04-1.039-.018-.02-.788-.786-.02-.02-1.038-1.039a.477.477 0 0 1 0-.672l.176-.176a.875.875 0 0 1 .79-.197l1.447.438a3.322 3.322 0 0 1 .504-.75l1.97-1.97-.746-.744-4.25-2.348a.339.339 0 0 1-.08-.566l.573-.573a.909.909 0 0 1 .794-.211l6.39 1.736.02.006 2.286-2.286c.37-.372 1.621-1.02 1.993-.65.37.372-.279 1.622-.65 1.993z" />
            </svg>
          </span>
          <span v-else-if="tab.kind === 'hotel'" class="inline-flex h-4 w-4 items-center justify-center">
            <svg
              viewBox="0 0 380 380"
              class="h-3.5 w-3.5 text-slate-900"
              fill="white"
              stroke="currentColor"
              stroke-width="12"
              stroke-linecap="round"
              stroke-linejoin="round"
              vector-effect="non-scaling-stroke"
              paint-order="stroke fill"
              aria-hidden="true"
            >
              <path d="M303.838 353.36V106.867h7.697V64.12H68.465v42.747h7.697V353.36H61.759V380h256.482v-26.64H303.838zM145.162 336.865h-40.424v-40.424h40.424V336.865zM145.162 278.32h-40.424v-40.424h40.424V278.32zM145.162 219.775h-40.424v-40.424h40.424V219.775zM145.162 161.23h-40.424v-40.424h40.424V161.23zM210.213 353.36h-40.426v-56.919h40.426V353.36zM210.213 278.32h-40.426v-40.424h40.426V278.32zM210.213 219.775h-40.426v-40.424h40.426V219.775zM210.213 161.23h-40.426v-40.424h40.426V161.23zM275.262 336.865h-40.424v-40.424h40.424V336.865zM275.262 278.32h-40.424v-40.424h40.424V278.32zM275.262 219.775h-40.424v-40.424h40.424V219.775zM275.262 161.23h-40.424v-40.424h40.424V161.23z" />
              <path d="M111.373 55.757l18.116-9.525 18.117 9.525-3.46-20.173 14.657-14.287-20.255-2.943L129.489 0l-9.059 18.354-20.254 2.943 14.656 14.287z" />
              <path d="M171.883 55.757 190 46.232l18.117 9.525-3.461-20.173 14.657-14.287-20.254-2.943L190 0l-9.059 18.354-20.254 2.943 14.657 14.287z" />
              <path d="M232.394 55.757 250.511 46.232l18.116 9.525-3.459-20.173 14.656-14.287-20.254-2.943L250.511 0l-9.059 18.354-20.255 2.943 14.657 14.287z" />
            </svg>
          </span>
          <span v-else-if="tab.kind === 'insurance'" class="inline-flex h-4 w-4 items-center justify-center">
            <svg
              viewBox="0 0 24 24"
              class="h-3.5 w-3.5"
              fill="none"
              stroke="currentColor"
              stroke-width="1.6"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M12 3l8 4v5c0 5-3.5 8.5-8 10-4.5-1.5-8-5-8-10V7l8-4z" />
              <path d="M9 12l2 2 4-4" />
            </svg>
          </span>
          <span v-else-if="tab.kind === 'transfer'" class="inline-flex h-4 w-4 items-center justify-center">
            <svg
              viewBox="0 0 50 50"
              class="h-3.5 w-3.5 text-slate-900"
              fill="white"
              stroke="currentColor"
              stroke-width="1.2"
              stroke-linecap="round"
              stroke-linejoin="round"
              vector-effect="non-scaling-stroke"
              paint-order="stroke fill"
              aria-hidden="true"
            >
              <path d="M5.875 15.90625C2.652344 16.027344 0 18.648438 0 21.8125L0 42.1875C0 43.722656 1.277344 45 2.8125 45L5.09375 45C5.570313 47.835938 8.035156 50 11 50C13.964844 50 16.429688 47.835938 16.90625 45L33.09375 45C33.570313 47.835938 36.035156 50 39 50C41.964844 50 44.429688 47.835938 44.90625 45L45.3125 45C46.242188 45 47.320313 44.761719 48.28125 44.09375C49.242188 43.425781 50 42.230469 50 40.6875L50 32.8125C50 31.765625 49.765625 30.671875 49.4375 29.6875L49.4375 29.65625C49.4375 29.65625 47.734375 24.949219 46.21875 21.3125C45.421875 19.34375 44.40625 17.9375 42.96875 17.0625C41.53125 16.1875 39.777344 15.90625 37.6875 15.90625 Z M 5.90625 17.90625L37.6875 17.90625C39.597656 17.90625 40.917969 18.179688 41.90625 18.78125C42.402344 19.082031 42.84375 19.472656 43.25 20L38.3125 20C36.5 20 35 21.5 35 23.3125L35 27.6875C35 29.5 36.5 31 38.3125 31L47.71875 31C47.875 31.625 48 32.265625 48 32.8125L48 40.6875C48 41.644531 47.671875 42.109375 47.15625 42.46875C46.640625 42.828125 45.882813 43 45.3125 43L44.90625 43C44.429688 40.164063 41.964844 38 39 38C36.035156 38 33.570313 40.164063 33.09375 43L16.90625 43C16.429688 40.164063 13.964844 38 11 38C8.035156 38 5.570313 40.164063 5.09375 43L2.8125 43C2.347656 43 2 42.652344 2 42.1875L2 21.8125C2 19.785156 3.742188 18.003906 5.90625 17.90625 Z M 7.3125 20C5.5 20 4 21.5 4 23.3125L4 27.6875C4 29.5 5.5 31 7.3125 31L13.6875 31C15.5 31 17 29.5 17 27.6875L17 23.3125C17 21.5 15.5 20 13.6875 20 Z M 22.3125 20C20.5 20 19 21.5 19 23.3125L19 27.6875C19 29.5 20.5 31 22.3125 31L29.6875 31C31.5 31 33 29.5 33 27.6875L33 23.3125C33 21.5 31.5 20 29.6875 20 Z M 7.3125 22L13.6875 22C14.476563 22 15 22.523438 15 23.3125L15 27.6875C15 28.476563 14.476563 29 13.6875 29L7.3125 29C6.523438 29 6 28.476563 6 27.6875L6 23.3125C6 22.523438 6.523438 22 7.3125 22 Z M 22.3125 22L29.6875 22C30.476563 22 31 22.523438 31 23.3125L31 27.6875C31 28.476563 30.476563 29 29.6875 29L22.3125 29C21.523438 29 21 28.476563 21 27.6875L21 23.3125C21 22.523438 21.523438 22 22.3125 22 Z M 38.3125 22L44.34375 22C44.351563 22.023438 44.367188 22.039063 44.375 22.0625L44.375 22.09375C45.503906 24.804688 46.527344 27.554688 47.0625 29L38.3125 29C37.523438 29 37 28.476563 37 27.6875L37 23.3125C37 22.523438 37.523438 22 38.3125 22 Z M 11 40C13.175781 40 14.902344 41.714844 14.96875 43.875C14.949219 43.988281 14.949219 44.105469 14.96875 44.21875C14.855469 46.335938 13.144531 48 11 48C8.8125 48 7.050781 46.269531 7 44.09375C7.007813 44.019531 7.007813 43.949219 7 43.875C7.066406 41.714844 8.820313 40 11 40 Z M 39 40C41.222656 40 43 41.777344 43 44C42.996094 44.042969 42.996094 44.082031 43 44.125C42.933594 46.285156 41.179688 48 39 48C36.8125 48 35.050781 46.269531 35 44.09375C35.007813 44.019531 35.007813 43.949219 35 43.875C35.066406 41.714844 36.820313 40 39 40Z" />
            </svg>
          </span>
          <span>{{ tab.label }}</span>
        </span>
      </button>
    </div>

    <div v-if="printMode" class="space-y-4">
      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm print-card">
        <div class="print-title text-sm font-semibold text-slate-900">{{ t('portal.infoTabs.flight') }}</div>
        <div class="mt-3 space-y-2 text-sm">
          <div class="text-xs font-semibold text-slate-500">{{ t('portal.docs.flightOutboundTitle') }}</div>
          <div v-if="hasText(travel?.arrival?.airline)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.airline') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.airline) }}</span>
          </div>
          <div v-if="hasText(travel?.arrival?.departureAirport)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.departureAirport') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.departureAirport) }}</span>
          </div>
          <div v-if="hasText(travel?.arrival?.arrivalAirport)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.arrivalAirport') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.arrivalAirport) }}</span>
          </div>
          <div v-if="hasText(travel?.arrival?.flightCode)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.flightCode') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.flightCode) }}</span>
          </div>
          <div v-if="hasText(travel?.arrival?.ticketNo ?? travel?.ticketNo)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.ticketNoOutbound') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.ticketNo ?? travel?.ticketNo) }}</span>
          </div>
          <div v-if="formatTime(travel?.arrival?.departureTime) !== '—'" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.departureTime') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatTime(travel?.arrival?.departureTime) }}</span>
          </div>
          <div v-if="formatTime(travel?.arrival?.arrivalTime) !== '—'" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.arrivalTime') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatTime(travel?.arrival?.arrivalTime) }}</span>
          </div>
          <div v-if="hasText(travel?.arrival?.pnr)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.pnr') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.pnr) }}</span>
          </div>
          <div v-if="formatBaggage(travel?.arrival?.baggagePieces, travel?.arrival?.baggageTotalKg) !== '—'" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.baggage') }}</span>
            <span class="text-right font-medium text-slate-800">
              {{ formatBaggage(travel?.arrival?.baggagePieces, travel?.arrival?.baggageTotalKg) }}
            </span>
          </div>
        </div>

        <div class="mt-4 space-y-2 text-sm">
          <div class="text-xs font-semibold text-slate-500">{{ t('portal.docs.flightReturnTitle') }}</div>
          <div v-if="hasText(travel?.return?.airline)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.airline') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.airline) }}</span>
          </div>
          <div v-if="hasText(travel?.return?.departureAirport)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.departureAirport') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.departureAirport) }}</span>
          </div>
          <div v-if="hasText(travel?.return?.arrivalAirport)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.arrivalAirport') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.arrivalAirport) }}</span>
          </div>
          <div v-if="hasText(travel?.return?.flightCode)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.flightCode') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.flightCode) }}</span>
          </div>
          <div v-if="hasText(travel?.return?.ticketNo)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.ticketNoReturn') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.ticketNo) }}</span>
          </div>
          <div v-if="formatTime(travel?.return?.departureTime) !== '—'" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.departureTime') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatTime(travel?.return?.departureTime) }}</span>
          </div>
          <div v-if="formatTime(travel?.return?.arrivalTime) !== '—'" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.arrivalTime') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatTime(travel?.return?.arrivalTime) }}</span>
          </div>
          <div v-if="hasText(travel?.return?.pnr)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.pnr') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.pnr) }}</span>
          </div>
          <div v-if="formatBaggage(travel?.return?.baggagePieces, travel?.return?.baggageTotalKg) !== '—'" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.baggage') }}</span>
            <span class="text-right font-medium text-slate-800">
              {{ formatBaggage(travel?.return?.baggagePieces, travel?.return?.baggageTotalKg) }}
            </span>
          </div>
        </div>
        <p v-if="!hasFlightValue(travel?.arrival) && !hasFlightValue(travel?.return)" class="mt-3 text-xs text-slate-500">
          {{ t('portal.infoTabs.empty') }}
        </p>
      </div>

      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm print-card">
        <div class="print-title text-sm font-semibold text-slate-900">{{ t('portal.infoTabs.hotel') }}</div>
        <div class="mt-3 space-y-2 text-sm">
          <div v-if="hasText(readContentString(hotelTab?.content, 'hotelName'))" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.hotelName') }}</span>
            <span class="text-right font-medium text-slate-800">{{ contentValue(hotelTab?.content, 'hotelName') }}</span>
          </div>
          <div v-if="hasText(readContentString(hotelTab?.content, 'address'))" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.hotelAddress') }}</span>
            <span class="text-right font-medium text-slate-800">
              <a
                v-if="buildMapsLink(readContentString(hotelTab?.content, 'address'))"
                :href="buildMapsLink(readContentString(hotelTab?.content, 'address'))"
                target="_blank"
                rel="noreferrer"
                class="underline"
              >
                {{ readContentString(hotelTab?.content, 'address') }}
              </a>
              <span v-else>{{ contentValue(hotelTab?.content, 'address') }}</span>
            </span>
          </div>
          <div v-if="hasText(contentPhone(hotelTab?.content).raw)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.hotelPhone') }}</span>
            <span class="text-right font-medium text-slate-800">
              <a
                v-if="contentPhone(hotelTab?.content).link"
                :href="contentPhone(hotelTab?.content).link"
                class="underline"
              >
                {{ contentPhone(hotelTab?.content).display || contentPhone(hotelTab?.content).raw }}
              </a>
              <span v-else>{{ contentPhone(hotelTab?.content).display || contentPhone(hotelTab?.content).raw }}</span>
            </span>
          </div>
          <div v-if="hasText(readContentString(hotelTab?.content, 'checkInNote'))" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.checkInNote') }}</span>
            <span class="text-right font-medium text-slate-800">{{ contentValue(hotelTab?.content, 'checkInNote') }}</span>
          </div>
          <div v-if="hasText(readContentString(hotelTab?.content, 'checkOutNote'))" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.checkOutNote') }}</span>
            <span class="text-right font-medium text-slate-800">{{ contentValue(hotelTab?.content, 'checkOutNote') }}</span>
          </div>
        </div>
        <div class="mt-4 space-y-2 text-sm">
          <div v-if="hasText(travel?.roomNo)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.roomNo') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.roomNo) }}</span>
          </div>
          <div v-if="hasText(travel?.roomType)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.roomType') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.roomType) }}</span>
          </div>
          <div v-if="hasText(travel?.boardType)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.boardType') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatBoard(travel?.boardType) }}</span>
          </div>
          <div v-if="hasText(travel?.hotelCheckInDate)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.checkIn') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatDate(travel?.hotelCheckInDate) }}</span>
          </div>
          <div v-if="hasText(travel?.hotelCheckOutDate)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.checkOut') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatDate(travel?.hotelCheckOutDate) }}</span>
          </div>
        </div>
      </div>

      <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm print-card">
        <div class="print-title text-sm font-semibold text-slate-900">{{ t('portal.infoTabs.insurance') }}</div>
        <div class="mt-3 space-y-2 text-sm">
          <div v-if="hasText(travel?.insurance?.companyName)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.companyName') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.insurance?.companyName) }}</span>
          </div>
          <div v-if="hasText(travel?.insurance?.policyNo)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.policyNo') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.insurance?.policyNo) }}</span>
          </div>
          <div v-if="hasText(travel?.insurance?.startDate)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.startDate') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatDate(travel?.insurance?.startDate) }}</span>
          </div>
          <div v-if="hasText(travel?.insurance?.endDate)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.endDate') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatDate(travel?.insurance?.endDate) }}</span>
          </div>
        </div>
        <p v-if="!hasInsuranceValue(travel?.insurance)" class="mt-3 text-xs text-slate-500">
          {{ t('portal.infoTabs.empty') }}
        </p>
      </div>

      <div
        v-for="tab in transferTabs"
        :key="tab.id"
        class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm print-card"
      >
        <template v-if="resolveTransferData(tab.content).hasAny">
        <div class="print-title text-sm font-semibold text-slate-900">{{ tab.title }}</div>
        <div class="mt-3 space-y-2 text-sm">
          <div class="text-xs font-semibold text-slate-500">{{ t('portal.docs.transferOutboundTitle') }}</div>
          <div v-if="formatTime(resolveTransferData(tab.content).outbound?.pickupTime) !== '—'" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferPickupTime') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatTime(resolveTransferData(tab.content).outbound?.pickupTime) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).outbound?.pickupPlace)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferPickupPlace') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).outbound?.pickupPlace) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).outbound?.dropoffPlace)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferDropoffPlace') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).outbound?.dropoffPlace) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).outbound?.vehicle)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferVehicle') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).outbound?.vehicle) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).outbound?.plate)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferPlate') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).outbound?.plate) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).outbound?.driverInfo)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferDriver') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).outbound?.driverInfo) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).outbound?.note)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferNote') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).outbound?.note) }}</span>
          </div>
        </div>

        <div class="mt-4 space-y-2 text-sm">
          <div class="text-xs font-semibold text-slate-500">{{ t('portal.docs.transferReturnTitle') }}</div>
          <div v-if="formatTime(resolveTransferData(tab.content).return?.pickupTime) !== '—'" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferPickupTime') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatTime(resolveTransferData(tab.content).return?.pickupTime) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).return?.pickupPlace)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferPickupPlace') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).return?.pickupPlace) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).return?.dropoffPlace)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferDropoffPlace') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).return?.dropoffPlace) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).return?.vehicle)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferVehicle') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).return?.vehicle) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).return?.plate)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferPlate') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).return?.plate) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).return?.driverInfo)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferDriver') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).return?.driverInfo) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(tab.content).return?.note)" class="flex items-start justify-between gap-3 print-row">
            <span class="text-slate-500">{{ t('portal.docs.transferNote') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(tab.content).return?.note) }}</span>
          </div>
        </div>
        </template>
        <p v-else class="mt-3 text-xs text-slate-500">
          {{ t('portal.infoTabs.empty') }}
        </p>
      </div>

      <div
        v-for="tab in printCustomTabs"
        :key="tab.id"
        class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm print-card"
      >
        <div class="print-title text-sm font-semibold text-slate-900">{{ tab.title }}</div>
        <div class="mt-3 space-y-3 text-sm text-slate-600">
          <div v-if="getCustomText(tab.content)" class="whitespace-pre-line">
            {{ getCustomText(tab.content) }}
          </div>
          <div v-if="getCustomFields(tab.content).length > 0" class="grid gap-2">
            <div
              v-for="field in getCustomFields(tab.content)"
              :key="field.label"
              class="flex items-start justify-between gap-3 print-row"
            >
              <span class="text-slate-500">{{ field.label }}</span>
              <span class="text-right font-medium text-slate-800">
                <a
                  v-if="isPhoneField(field.label, field.value) && buildTelLink(field.value)"
                  :href="buildTelLink(field.value)"
                  class="underline"
                >
                  {{ formatPhoneDisplay(field.value) || field.value }}
                </a>
                <a
                  v-else-if="isAddressField(field.label) && buildMapsLink(field.value)"
                  :href="buildMapsLink(field.value)"
                  target="_blank"
                  rel="noreferrer"
                  class="underline"
                >
                  {{ field.value }}
                </a>
                <span v-else>{{ field.value }}</span>
              </span>
            </div>
          </div>
          <div v-if="!getCustomText(tab.content) && getCustomFields(tab.content).length === 0" class="text-sm text-slate-500">
            {{ t('portal.infoTabs.empty') }}
          </div>
        </div>
      </div>
    </div>

    <template v-else>
    <div v-if="activeTab?.kind === 'flight'" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div class="text-sm font-semibold text-slate-900">{{ t('portal.docs.flightOutboundTitle') }}</div>
      <div class="mt-3 space-y-2 text-sm">
        <div v-if="hasText(travel?.arrival?.airline)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.airline') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.airline) }}</span>
        </div>
        <div v-if="hasText(travel?.arrival?.departureAirport)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.departureAirport') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.departureAirport) }}</span>
        </div>
        <div v-if="hasText(travel?.arrival?.arrivalAirport)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.arrivalAirport') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.arrivalAirport) }}</span>
        </div>
        <div v-if="hasText(travel?.arrival?.flightCode)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.flightCode') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.flightCode) }}</span>
        </div>
        <div v-if="hasText(travel?.arrival?.ticketNo ?? travel?.ticketNo)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.ticketNoOutbound') }}</span>
          <button
            type="button"
            class="inline-flex items-center justify-end gap-1.5 font-medium text-slate-800 cursor-pointer hover:underline focus:outline-none focus:underline"
            @click="copyToClipboard((travel?.arrival?.ticketNo ?? travel?.ticketNo) ?? '')"
          >
            {{ valueOrDash(travel?.arrival?.ticketNo ?? travel?.ticketNo) }}
            <CopyIcon :size="14" icon-class="shrink-0 text-slate-500" />
          </button>
        </div>
        <div v-if="formatTime(travel?.arrival?.departureTime) !== '—'" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.departureTime') }}</span>
          <span class="text-right font-medium text-slate-800">{{ formatTime(travel?.arrival?.departureTime) }}</span>
        </div>
        <div v-if="formatTime(travel?.arrival?.arrivalTime) !== '—'" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.arrivalTime') }}</span>
          <span class="text-right font-medium text-slate-800">{{ formatTime(travel?.arrival?.arrivalTime) }}</span>
        </div>
        <div v-if="hasText(travel?.arrival?.pnr)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.pnr') }}</span>
          <button
            type="button"
            class="inline-flex items-center justify-end gap-1.5 font-medium text-slate-800 cursor-pointer hover:underline focus:outline-none focus:underline"
            @click="copyToClipboard(travel?.arrival?.pnr ?? '')"
          >
            {{ valueOrDash(travel?.arrival?.pnr) }}
            <CopyIcon :size="14" icon-class="shrink-0 text-slate-500" />
          </button>
        </div>
        <div v-if="formatBaggage(travel?.arrival?.baggagePieces, travel?.arrival?.baggageTotalKg) !== '—'" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.baggage') }}</span>
          <span class="text-right font-medium text-slate-800">
            {{ formatBaggage(travel?.arrival?.baggagePieces, travel?.arrival?.baggageTotalKg) }}
          </span>
        </div>
      </div>

      <div class="mt-5 border-t border-slate-100 pt-4">
        <div class="text-sm font-semibold text-slate-900">{{ t('portal.docs.flightReturnTitle') }}</div>
        <div class="mt-3 space-y-2 text-sm">
          <div v-if="hasText(travel?.return?.airline)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.airline') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.airline) }}</span>
          </div>
          <div v-if="hasText(travel?.return?.departureAirport)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.departureAirport') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.departureAirport) }}</span>
          </div>
          <div v-if="hasText(travel?.return?.arrivalAirport)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.arrivalAirport') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.arrivalAirport) }}</span>
          </div>
          <div v-if="hasText(travel?.return?.flightCode)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.flightCode') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.flightCode) }}</span>
          </div>
        <div v-if="hasText(travel?.return?.ticketNo)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.ticketNoReturn') }}</span>
          <button
            type="button"
            class="inline-flex items-center justify-end gap-1.5 font-medium text-slate-800 cursor-pointer hover:underline focus:outline-none focus:underline"
            @click="copyToClipboard(travel?.return?.ticketNo ?? '')"
          >
            {{ valueOrDash(travel?.return?.ticketNo) }}
            <CopyIcon :size="14" icon-class="shrink-0 text-slate-500" />
          </button>
        </div>
          <div v-if="formatTime(travel?.return?.departureTime) !== '—'" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.departureTime') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatTime(travel?.return?.departureTime) }}</span>
          </div>
          <div v-if="formatTime(travel?.return?.arrivalTime) !== '—'" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.arrivalTime') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatTime(travel?.return?.arrivalTime) }}</span>
          </div>
          <div v-if="hasText(travel?.return?.pnr)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.pnr') }}</span>
            <button
              type="button"
              class="inline-flex items-center justify-end gap-1.5 font-medium text-slate-800 cursor-pointer hover:underline focus:outline-none focus:underline"
              @click="copyToClipboard(travel?.return?.pnr ?? '')"
            >
              {{ valueOrDash(travel?.return?.pnr) }}
              <CopyIcon :size="14" icon-class="shrink-0 text-slate-500" />
            </button>
          </div>
          <div v-if="formatBaggage(travel?.return?.baggagePieces, travel?.return?.baggageTotalKg) !== '—'" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.baggage') }}</span>
            <span class="text-right font-medium text-slate-800">
              {{ formatBaggage(travel?.return?.baggagePieces, travel?.return?.baggageTotalKg) }}
            </span>
          </div>
        </div>
        <p v-if="!hasFlightValue(travel?.arrival) && !hasFlightValue(travel?.return)" class="mt-3 text-xs text-slate-500">
          {{ t('portal.infoTabs.empty') }}
        </p>
      </div>
    </div>

    <div v-else-if="activeTab?.kind === 'hotel'" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div class="text-sm font-semibold text-slate-900">{{ t('portal.docs.hotelCardTitle') }}</div>
      <div class="mt-3 space-y-2 text-sm">
        <div v-if="hasText(readContentString(hotelTab?.content, 'hotelName'))" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.hotelName') }}</span>
          <span class="text-right font-medium text-slate-800">{{ contentValue(hotelTab?.content, 'hotelName') }}</span>
        </div>
        <div v-if="hasText(readContentString(hotelTab?.content, 'address'))" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.hotelAddress') }}</span>
          <span class="text-right font-medium text-slate-800">
            <a
              v-if="buildMapsLink(readContentString(hotelTab?.content, 'address'))"
              :href="buildMapsLink(readContentString(hotelTab?.content, 'address'))"
              target="_blank"
              rel="noreferrer"
              class="underline"
            >
              {{ readContentString(hotelTab?.content, 'address') }}
            </a>
            <span v-else>{{ contentValue(hotelTab?.content, 'address') }}</span>
          </span>
        </div>
        <div v-if="hasText(contentPhone(hotelTab?.content).raw)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.hotelPhone') }}</span>
          <span class="text-right font-medium text-slate-800">
            <a
              v-if="contentPhone(hotelTab?.content).link"
              :href="contentPhone(hotelTab?.content).link"
              class="underline"
            >
              {{ contentPhone(hotelTab?.content).display || contentPhone(hotelTab?.content).raw }}
            </a>
            <span v-else>{{ contentPhone(hotelTab?.content).display || contentPhone(hotelTab?.content).raw }}</span>
          </span>
        </div>
        <div v-if="hasText(readContentString(hotelTab?.content, 'checkInNote'))" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.checkInNote') }}</span>
          <span class="text-right font-medium text-slate-800">{{ contentValue(hotelTab?.content, 'checkInNote') }}</span>
        </div>
        <div v-if="hasText(readContentString(hotelTab?.content, 'checkOutNote'))" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.checkOutNote') }}</span>
          <span class="text-right font-medium text-slate-800">{{ contentValue(hotelTab?.content, 'checkOutNote') }}</span>
        </div>
      </div>

      <div class="mt-5 border-t border-slate-100 pt-4">
        <div class="space-y-2 text-sm">
          <div v-if="hasText(travel?.roomNo)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.roomNo') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.roomNo) }}</span>
          </div>
          <div v-if="hasText(travel?.roomType)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.roomType') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.roomType) }}</span>
          </div>
          <div v-if="hasText(travel?.boardType)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.boardType') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatBoard(travel?.boardType) }}</span>
          </div>
          <div v-if="hasText(travel?.hotelCheckInDate)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.checkIn') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatDate(travel?.hotelCheckInDate) }}</span>
          </div>
          <div v-if="hasText(travel?.hotelCheckOutDate)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.checkOut') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatDate(travel?.hotelCheckOutDate) }}</span>
          </div>
        </div>
      </div>
    </div>

    <div v-else-if="activeTab?.kind === 'insurance'" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div class="text-sm font-semibold text-slate-900">{{ t('portal.docs.insuranceTitle') }}</div>
      <div class="mt-3 space-y-2 text-sm">
        <div v-if="hasText(travel?.insurance?.companyName)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.companyName') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.insurance?.companyName) }}</span>
        </div>
        <div v-if="hasText(travel?.insurance?.policyNo)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.policyNo') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.insurance?.policyNo) }}</span>
        </div>
        <div v-if="hasText(travel?.insurance?.startDate)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.startDate') }}</span>
          <span class="text-right font-medium text-slate-800">{{ formatDate(travel?.insurance?.startDate) }}</span>
        </div>
        <div v-if="hasText(travel?.insurance?.endDate)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.endDate') }}</span>
          <span class="text-right font-medium text-slate-800">{{ formatDate(travel?.insurance?.endDate) }}</span>
        </div>
      </div>
      <p v-if="!hasInsuranceValue(travel?.insurance)" class="mt-3 text-xs text-slate-500">
        {{ t('portal.infoTabs.empty') }}
      </p>
    </div>

    <div v-else-if="activeTab?.kind === 'transfer'" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div class="text-sm font-semibold text-slate-900">{{ activeTab?.label ?? t('portal.docs.transferTitle') }}</div>
      <div class="mt-3 space-y-2 text-sm">
        <div class="text-xs font-semibold text-slate-500">{{ t('portal.docs.transferOutboundTitle') }}</div>
        <div v-if="formatTime(resolveTransferData(activeTab?.content).outbound?.pickupTime) !== '—'" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.transferPickupTime') }}</span>
          <span class="text-right font-medium text-slate-800">{{ formatTime(resolveTransferData(activeTab?.content).outbound?.pickupTime) }}</span>
        </div>
        <div v-if="hasText(resolveTransferData(activeTab?.content).outbound?.pickupPlace)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.transferPickupPlace') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).outbound?.pickupPlace) }}</span>
        </div>
        <div v-if="hasText(resolveTransferData(activeTab?.content).outbound?.dropoffPlace)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.transferDropoffPlace') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).outbound?.dropoffPlace) }}</span>
        </div>
        <div v-if="hasText(resolveTransferData(activeTab?.content).outbound?.vehicle)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.transferVehicle') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).outbound?.vehicle) }}</span>
        </div>
        <div v-if="hasText(resolveTransferData(activeTab?.content).outbound?.plate)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.transferPlate') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).outbound?.plate) }}</span>
        </div>
        <div v-if="hasText(resolveTransferData(activeTab?.content).outbound?.driverInfo)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.transferDriver') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).outbound?.driverInfo) }}</span>
        </div>
        <div v-if="hasText(resolveTransferData(activeTab?.content).outbound?.note)" class="flex items-start justify-between gap-3">
          <span class="text-slate-500">{{ t('portal.docs.transferNote') }}</span>
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).outbound?.note) }}</span>
        </div>
      </div>

      <div class="mt-5 border-t border-slate-100 pt-4">
        <div class="text-xs font-semibold text-slate-500">{{ t('portal.docs.transferReturnTitle') }}</div>
        <div class="mt-3 space-y-2 text-sm">
          <div v-if="formatTime(resolveTransferData(activeTab?.content).return?.pickupTime) !== '—'" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.transferPickupTime') }}</span>
            <span class="text-right font-medium text-slate-800">{{ formatTime(resolveTransferData(activeTab?.content).return?.pickupTime) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(activeTab?.content).return?.pickupPlace)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.transferPickupPlace') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).return?.pickupPlace) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(activeTab?.content).return?.dropoffPlace)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.transferDropoffPlace') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).return?.dropoffPlace) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(activeTab?.content).return?.vehicle)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.transferVehicle') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).return?.vehicle) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(activeTab?.content).return?.plate)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.transferPlate') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).return?.plate) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(activeTab?.content).return?.driverInfo)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.transferDriver') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).return?.driverInfo) }}</span>
          </div>
          <div v-if="hasText(resolveTransferData(activeTab?.content).return?.note)" class="flex items-start justify-between gap-3">
            <span class="text-slate-500">{{ t('portal.docs.transferNote') }}</span>
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(resolveTransferData(activeTab?.content).return?.note) }}</span>
          </div>
        </div>
      </div>
      <p v-if="!resolveTransferData(activeTab?.content).hasAny" class="mt-3 text-xs text-slate-500">
        {{ t('portal.infoTabs.empty') }}
      </p>
    </div>

    <div v-else-if="activeTab?.kind === 'custom'" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div class="text-sm font-semibold text-slate-900">{{ activeTab?.label ?? '' }}</div>
      <div class="mt-3 space-y-3 text-sm text-slate-600">
        <div v-if="getCustomText(activeTab?.content)" class="whitespace-pre-line">
          {{ getCustomText(activeTab?.content) }}
        </div>
        <div v-if="getCustomFields(activeTab?.content).length > 0" class="grid gap-2">
          <div
            v-for="field in getCustomFields(activeTab?.content)"
            :key="field.label"
            class="flex items-start justify-between gap-3 print-row"
          >
            <span class="text-slate-500">{{ field.label }}</span>
            <span class="text-right font-medium text-slate-800">
              <a
                v-if="isPhoneField(field.label, field.value) && buildTelLink(field.value)"
                :href="buildTelLink(field.value)"
                class="underline"
              >
                {{ formatPhoneDisplay(field.value) || field.value }}
              </a>
              <a
                v-else-if="isAddressField(field.label) && buildMapsLink(field.value)"
                :href="buildMapsLink(field.value)"
                target="_blank"
                rel="noreferrer"
                class="underline"
              >
                {{ field.value }}
              </a>
              <span v-else>{{ field.value }}</span>
            </span>
          </div>
        </div>
        <div
          v-if="!getCustomText(activeTab?.content) && getCustomFields(activeTab?.content).length === 0"
          class="text-sm text-slate-500"
        >
          {{ t('portal.infoTabs.empty') }}
        </div>
      </div>
    </div>
    </template>
  </div>
</template>
