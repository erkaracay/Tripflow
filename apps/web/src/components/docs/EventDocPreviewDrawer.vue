<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatPhoneDisplay, normalizePhone } from '../../lib/normalize'
import AppDrawerShell from '../ui/AppDrawerShell.vue'
import type { EventDocTabDto } from '../../types'

const props = defineProps<{
  open: boolean
  eventTitle?: string | null
  tab: EventDocTabDto | null
  participantName?: string | null
}>()

const emit = defineEmits<{
  (event: 'close'): void
}>()

const { t } = useI18n()

const previewTitleId = 'event-doc-preview-title'
const normalizedType = computed(() => (props.tab?.type ?? '').trim().toLowerCase())

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

const hasText = (value?: string | null) => Boolean(value?.trim())
const valueOrDash = (value?: string | null) => (hasText(value) ? value!.trim() : '—')

const buildMapsLink = (value?: string | null) => {
  if (!hasText(value)) return ''
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(value!.trim())}`
}

const buildTelLink = (value?: string | null) => {
  if (!hasText(value)) return ''
  const trimmed = value?.trim() ?? ''
  const { normalized } = normalizePhone(trimmed)
  return normalized ? `tel:${normalized}` : ''
}

const contentPhone = computed(() => {
  const raw = readContentString(props.tab?.content, 'phone')
  const display = raw ? formatPhoneDisplay(raw) || raw : ''
  return {
    raw,
    display: display || raw,
    link: buildTelLink(raw),
  }
})

const hasTransferValue = (transfer?: Record<string, string> | null) =>
  Boolean(
    transfer?.pickupTime ||
      transfer?.pickupPlace ||
      transfer?.dropoffPlace ||
      transfer?.vehicle ||
      transfer?.plate ||
      transfer?.driverInfo ||
      transfer?.note
  )

const parseTransferContent = (content: unknown) => {
  const obj = toObject(content)
  if (!obj) {
    return { arrival: null as Record<string, string> | null, return: null as Record<string, string> | null }
  }

  const arrivalObj = toObject(obj.arrival)
  const returnObj = toObject(obj.return)

  const read = (section: Record<string, unknown> | null, key: string, fallbackKey: string) => {
    const sectionValue = section?.[key]
    if (typeof sectionValue === 'string' && sectionValue.trim()) {
      return sectionValue.trim()
    }
    const fallbackValue = obj[fallbackKey]
    return typeof fallbackValue === 'string' ? fallbackValue.trim() : ''
  }

  const build = (section: Record<string, unknown> | null, prefix: string) => {
    const transfer = {
      pickupTime: read(section, 'pickupTime', `${prefix}PickupTime`),
      pickupPlace: read(section, 'pickupPlace', `${prefix}PickupPlace`),
      dropoffPlace: read(section, 'dropoffPlace', `${prefix}DropoffPlace`),
      vehicle: read(section, 'vehicle', `${prefix}Vehicle`),
      plate: read(section, 'plate', `${prefix}Plate`),
      driverInfo: read(section, 'driverInfo', `${prefix}DriverInfo`),
      note: read(section, 'note', `${prefix}Note`),
    }

    return hasTransferValue(transfer) ? transfer : null
  }

  return {
    arrival: build(arrivalObj, 'arrival'),
    return: build(returnObj, 'return'),
  }
}

const transferData = computed(() => parseTransferContent(props.tab?.content))

const getCustomText = (content: unknown) => {
  const obj = toObject(content)
  if (!obj) return ''
  const text = obj.text
  return typeof text === 'string' ? text.trim() : ''
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
      const stringValue = typeof value === 'string' ? value.trim() : value.toString?.().trim?.() ?? ''
      if (!stringValue) return
      const label = key
        .replace(/_/g, ' ')
        .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
        .replace(/\b\w/g, (char) => char.toUpperCase())
      fields.push({ label, value: stringValue })
    })

  return fields
}

const customText = computed(() => getCustomText(props.tab?.content))
const customFields = computed(() => getCustomFields(props.tab?.content))

const isPhoneField = (label: string, value: string) => {
  const normalizedLabel = label.toLowerCase()
  if (/(telefon|phone|tel|gsm|whatsapp)/i.test(normalizedLabel)) {
    return true
  }
  return normalizePhone(value).normalized.length >= 8
}

const isAddressField = (label: string) => /(adres|address|location|konum|meeting|buluşma|toplanma|yer)/i.test(label.toLowerCase())

const hasVisibleContent = computed(() => {
  if (!props.tab) return false

  if (normalizedType.value === 'hotel') {
    return Boolean(
      hasText(readContentString(props.tab.content, 'hotelName')) ||
        hasText(readContentString(props.tab.content, 'address')) ||
        hasText(readContentString(props.tab.content, 'phone')) ||
        hasText(readContentString(props.tab.content, 'checkInNote')) ||
        hasText(readContentString(props.tab.content, 'checkOutNote'))
    )
  }

  if (normalizedType.value === 'insurance') {
    return true
  }

  if (normalizedType.value === 'transfer') {
    return Boolean(transferData.value.arrival || transferData.value.return)
  }

  return Boolean(customText.value || customFields.value.length > 0)
})
</script>

<template>
  <AppDrawerShell
    :open="open"
    desktop-width="xl"
    content-class="z-[60]"
    labelled-by="event-doc-preview-title"
    @close="emit('close')"
  >
    <template #default="{ panelClass, labelledBy }">
      <section :class="[panelClass, 'overflow-hidden']" role="dialog" aria-modal="true" :aria-labelledby="labelledBy">
        <div class="border-b border-slate-200 px-4 py-4 sm:px-6">
          <div class="flex items-start justify-between gap-4">
            <div>
              <div class="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{{ t('admin.docs.previewTitle') }}</div>
              <h2 :id="previewTitleId" class="mt-1 text-xl font-semibold text-slate-900">
                {{ tab?.title || t('admin.docs.previewFallbackTitle') }}
              </h2>
              <p v-if="eventTitle" class="mt-1 text-sm text-slate-500">{{ eventTitle }}</p>
              <p v-if="participantName" class="text-xs text-slate-500">{{ participantName }}</p>
            </div>
            <button
              class="rounded-full border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 transition hover:border-slate-300 hover:bg-slate-50"
              type="button"
              @click="emit('close')"
            >
              {{ t('common.dismiss') }}
            </button>
          </div>
        </div>

        <div class="min-h-0 flex-1 overflow-y-auto px-4 py-4 sm:px-6">
          <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
            <div class="text-sm font-semibold text-slate-900">{{ tab?.title || t('admin.docs.previewFallbackTitle') }}</div>

            <div v-if="hasVisibleContent" class="mt-3 space-y-3 text-sm">
              <template v-if="normalizedType === 'hotel'">
                <div v-if="hasText(readContentString(tab?.content, 'hotelName'))" class="flex items-start justify-between gap-3">
                  <span class="text-slate-500">{{ t('portal.docs.hotelName') }}</span>
                  <span class="text-right font-medium text-slate-800">{{ valueOrDash(readContentString(tab?.content, 'hotelName')) }}</span>
                </div>
                <div v-if="hasText(readContentString(tab?.content, 'address'))" class="flex items-start justify-between gap-3">
                  <span class="text-slate-500">{{ t('portal.docs.hotelAddress') }}</span>
                  <span class="text-right font-medium text-slate-800">
                    <a
                      v-if="buildMapsLink(readContentString(tab?.content, 'address'))"
                      :href="buildMapsLink(readContentString(tab?.content, 'address'))"
                      target="_blank"
                      rel="noreferrer"
                      class="underline"
                    >
                      {{ readContentString(tab?.content, 'address') }}
                    </a>
                    <span v-else>{{ valueOrDash(readContentString(tab?.content, 'address')) }}</span>
                  </span>
                </div>
                <div v-if="hasText(contentPhone.raw)" class="flex items-start justify-between gap-3">
                  <span class="text-slate-500">{{ t('portal.docs.hotelPhone') }}</span>
                  <span class="text-right font-medium text-slate-800">
                    <a v-if="contentPhone.link" :href="contentPhone.link" class="underline">
                      {{ contentPhone.display || contentPhone.raw }}
                    </a>
                    <span v-else>{{ contentPhone.display || contentPhone.raw }}</span>
                  </span>
                </div>
                <div v-if="hasText(readContentString(tab?.content, 'checkInNote'))" class="flex items-start justify-between gap-3">
                  <span class="text-slate-500">{{ t('portal.docs.checkInNote') }}</span>
                  <span class="text-right font-medium text-slate-800">{{ valueOrDash(readContentString(tab?.content, 'checkInNote')) }}</span>
                </div>
                <div v-if="hasText(readContentString(tab?.content, 'checkOutNote'))" class="flex items-start justify-between gap-3">
                  <span class="text-slate-500">{{ t('portal.docs.checkOutNote') }}</span>
                  <span class="text-right font-medium text-slate-800">{{ valueOrDash(readContentString(tab?.content, 'checkOutNote')) }}</span>
                </div>
              </template>

              <template v-else-if="normalizedType === 'insurance'">
                <p class="text-sm text-slate-600">{{ t('admin.docs.insurancePersonalNote') }}</p>
              </template>

              <template v-else-if="normalizedType === 'transfer'">
                <div v-if="transferData.arrival" class="space-y-2 rounded-xl border border-slate-100 p-3">
                  <div class="text-xs font-semibold text-slate-500">{{ t('portal.docs.transferOutboundTitle') }}</div>
                  <div v-if="hasText(transferData.arrival.pickupTime)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferPickupTime') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.arrival.pickupTime }}</span>
                  </div>
                  <div v-if="hasText(transferData.arrival.pickupPlace)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferPickupPlace') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.arrival.pickupPlace }}</span>
                  </div>
                  <div v-if="hasText(transferData.arrival.dropoffPlace)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferDropoffPlace') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.arrival.dropoffPlace }}</span>
                  </div>
                  <div v-if="hasText(transferData.arrival.vehicle)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferVehicle') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.arrival.vehicle }}</span>
                  </div>
                  <div v-if="hasText(transferData.arrival.plate)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferPlate') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.arrival.plate }}</span>
                  </div>
                  <div v-if="hasText(transferData.arrival.driverInfo)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferDriver') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.arrival.driverInfo }}</span>
                  </div>
                  <div v-if="hasText(transferData.arrival.note)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferNote') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.arrival.note }}</span>
                  </div>
                </div>

                <div v-if="transferData.return" class="space-y-2 rounded-xl border border-slate-100 p-3">
                  <div class="text-xs font-semibold text-slate-500">{{ t('portal.docs.transferReturnTitle') }}</div>
                  <div v-if="hasText(transferData.return.pickupTime)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferPickupTime') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.return.pickupTime }}</span>
                  </div>
                  <div v-if="hasText(transferData.return.pickupPlace)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferPickupPlace') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.return.pickupPlace }}</span>
                  </div>
                  <div v-if="hasText(transferData.return.dropoffPlace)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferDropoffPlace') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.return.dropoffPlace }}</span>
                  </div>
                  <div v-if="hasText(transferData.return.vehicle)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferVehicle') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.return.vehicle }}</span>
                  </div>
                  <div v-if="hasText(transferData.return.plate)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferPlate') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.return.plate }}</span>
                  </div>
                  <div v-if="hasText(transferData.return.driverInfo)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferDriver') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.return.driverInfo }}</span>
                  </div>
                  <div v-if="hasText(transferData.return.note)" class="flex items-start justify-between gap-3">
                    <span class="text-slate-500">{{ t('portal.docs.transferNote') }}</span>
                    <span class="text-right font-medium text-slate-800">{{ transferData.return.note }}</span>
                  </div>
                </div>
              </template>

              <template v-else>
                <div v-if="customText" class="whitespace-pre-line text-slate-700">
                  {{ customText }}
                </div>
                <div v-if="customFields.length > 0" class="grid gap-2">
                  <div v-for="field in customFields" :key="field.label" class="flex items-start justify-between gap-3">
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
              </template>
            </div>

            <p v-else class="mt-3 text-sm text-slate-500">{{ t('portal.infoTabs.empty') }}</p>
          </div>
        </div>
      </section>
    </template>
  </AppDrawerShell>
</template>
