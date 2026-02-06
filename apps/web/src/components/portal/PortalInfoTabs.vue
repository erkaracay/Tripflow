<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatBaggage, formatTime } from '../../lib/formatters'
import { formatPhoneDisplay, normalizePhone } from '../../lib/normalize'
import type { PortalDocsResponse, PortalFlightInfo, PortalInsuranceInfo } from '../../types'

type TabItem = {
  id: string
  label: string
  kind: 'flight' | 'hotel' | 'insurance' | 'custom'
  content?: unknown
}

const props = defineProps<{ docs?: PortalDocsResponse | null; printMode?: boolean }>()

const { t } = useI18n()

const travel = computed(() => props.docs?.participantTravel ?? null)

const normalizeType = (value?: string | null) => (value ?? '').trim().toLowerCase()

const hotelTab = computed(() => props.docs?.tabs.find((tab) => normalizeType(tab.type) === 'hotel') ?? null)
const customTabs = computed(() =>
  [...(props.docs?.tabs ?? [])]
    .filter((tab) => !['hotel', 'insurance'].includes(normalizeType(tab.type)))
    .sort((a, b) => a.sortOrder - b.sortOrder)
)

const tabs = computed<TabItem[]>(() => [
  { id: 'flight', label: t('portal.infoTabs.flight'), kind: 'flight' },
  { id: 'hotel', label: t('portal.infoTabs.hotel'), kind: 'hotel' },
  { id: 'insurance', label: t('portal.infoTabs.insurance'), kind: 'insurance' },
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

const toObject = (content: unknown): Record<string, unknown> | null => {
  if (!content || typeof content !== 'object' || Array.isArray(content)) {
    return null
  }
  return content as Record<string, unknown>
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
        {{ tab.label }}
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
        <div class="mt-4 space-y-2 text-sm">
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
        v-for="tab in customTabs"
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
          <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.arrival?.pnr) }}</span>
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
            <span class="text-right font-medium text-slate-800">{{ valueOrDash(travel?.return?.pnr) }}</span>
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

      <div class="mt-5 border-t border-slate-100 pt-4">
        <div class="text-sm font-semibold text-slate-900">{{ t('portal.docs.hotelName') }}</div>
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

    <div v-else-if="activeTab?.kind === 'custom'" class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div class="text-sm font-semibold text-slate-900">{{ activeTab.label }}</div>
      <div class="mt-3 space-y-3 text-sm text-slate-600">
        <div v-if="getCustomText(activeTab.content)" class="whitespace-pre-line">
          {{ getCustomText(activeTab.content) }}
        </div>
        <div v-if="getCustomFields(activeTab.content).length > 0" class="grid gap-2">
          <div
            v-for="field in getCustomFields(activeTab.content)"
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
        <div v-if="!getCustomText(activeTab.content) && getCustomFields(activeTab.content).length === 0" class="text-sm text-slate-500">
          {{ t('portal.infoTabs.empty') }}
        </div>
      </div>
    </div>
  </div>
</template>
