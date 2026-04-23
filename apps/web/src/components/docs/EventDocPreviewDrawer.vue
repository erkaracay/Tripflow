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
const accommodationAliasSet = new Set(['hotel', 'otel', 'accommodation', 'konaklama'])
const displayTabTitle = computed(() => {
  const rawTitle = props.tab?.title?.trim() ?? ''
  if (normalizedType.value !== 'hotel') {
    return rawTitle || t('admin.docs.previewFallbackTitle')
  }
  if (!rawTitle) {
    return t('admin.docs.types.hotel')
  }
  return accommodationAliasSet.has(rawTitle.toLowerCase()) ? t('admin.docs.types.hotel') : rawTitle
})

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

const parseIsoDate = (value?: string | null) => {
  if (!value) return null
  const trimmed = value.trim()
  if (!trimmed) return null
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(trimmed)
  if (!match) return null
  const year = Number(match[1] ?? 0)
  const month = Number(match[2] ?? 0)
  const day = Number(match[3] ?? 0)
  const parsed = new Date(Date.UTC(year, month - 1, day))
  if (
    Number.isNaN(parsed.getTime())
    || parsed.getUTCFullYear() !== year
    || parsed.getUTCMonth() !== month - 1
    || parsed.getUTCDate() !== day
  ) {
    return null
  }
  return parsed
}

const accommodationStayNights = computed(() => {
  const checkIn = parseIsoDate(readContentString(props.tab?.content, 'checkInDate'))
  const checkOut = parseIsoDate(readContentString(props.tab?.content, 'checkOutDate'))
  if (!checkIn || !checkOut) {
    return null
  }
  const nights = Math.floor((checkOut.getTime() - checkIn.getTime()) / (24 * 60 * 60 * 1000))
  if (nights < 0) {
    return null
  }
  return nights
})

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
        hasText(readContentString(props.tab.content, 'checkInDate')) ||
        hasText(readContentString(props.tab.content, 'checkOutDate')) ||
        hasText(readContentString(props.tab.content, 'checkInNote')) ||
        hasText(readContentString(props.tab.content, 'checkOutNote'))
    )
  }

  if (normalizedType.value === 'insurance') {
    return true
  }

  if (normalizedType.value === 'transfer') {
    return true
  }

  return Boolean(customText.value || customFields.value.length > 0)
})
</script>

<template>
  <AppDrawerShell
    :open="open"
    desktop-width="md"
    desktop-breakpoint="md"
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
                {{ displayTabTitle }}
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
            <div class="text-sm font-semibold text-slate-900">{{ displayTabTitle }}</div>

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
                <div v-if="hasText(readContentString(tab?.content, 'checkInDate'))" class="flex items-start justify-between gap-3">
                  <span class="text-slate-500">{{ t('portal.docs.checkInDate') }}</span>
                  <span class="text-right font-medium text-slate-800">{{ formatDate(readContentString(tab?.content, 'checkInDate')) }}</span>
                </div>
                <div v-if="hasText(readContentString(tab?.content, 'checkOutDate'))" class="flex items-start justify-between gap-3">
                  <span class="text-slate-500">{{ t('portal.docs.checkOutDate') }}</span>
                  <span class="text-right font-medium text-slate-800">{{ formatDate(readContentString(tab?.content, 'checkOutDate')) }}</span>
                </div>
                <div v-if="accommodationStayNights !== null" class="flex items-start justify-between gap-3">
                  <span class="text-slate-500">{{ t('portal.docs.stayDuration') }}</span>
                  <span class="text-right font-medium text-slate-800">
                    {{ t('portal.docs.stayDurationNights', { count: accommodationStayNights }) }}
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
                <p class="text-sm text-slate-600">{{ t('admin.docs.transferPersonalNote') }}</p>
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
