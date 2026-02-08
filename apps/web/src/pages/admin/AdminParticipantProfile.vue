<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet } from '../../lib/api'
import { useToast } from '../../lib/toast'
import { formatBaggage, formatDate, formatTime } from '../../lib/formatters'
import { formatPhoneDisplay } from '../../lib/normalize'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { ParticipantProfile } from '../../types'

const route = useRoute()
const { t } = useI18n()
const { pushToast } = useToast()

const eventId = computed(() => String(route.params.eventId ?? ''))
const participantId = computed(() => String(route.params.participantId ?? ''))

const loading = ref(true)
const errorMessage = ref<string | null>(null)
const profile = ref<ParticipantProfile | null>(null)

const details = computed(() => profile.value?.details ?? null)

const loadProfile = async () => {
  loading.value = true
  errorMessage.value = null
  try {
    profile.value = await apiGet<ParticipantProfile>(
      `/api/events/${eventId.value}/participants/${participantId.value}`
    )
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : t('errors.generic')
  } finally {
    loading.value = false
  }
}

const displayValue = (value?: string | null) => {
  if (!value) return t('common.noData')
  const trimmed = value.trim()
  return trimmed ? trimmed : t('common.noData')
}

const formatGender = (value?: string | null) => {
  if (!value) return t('common.noData')
  if (value === 'Female') return t('common.genderFemale')
  if (value === 'Male') return t('common.genderMale')
  if (value === 'Other') return t('common.genderOther')
  return value
}

const formatBaggageWithFallback = (
  pieces?: number | null,
  kg?: number | null,
  allowance?: string | null
) => {
  const formatted = formatBaggage(pieces, kg)
  if (formatted === '—' && allowance) {
    return allowance
  }
  return formatted
}

const copyText = async (value?: string | null) => {
  if (!value) return false
  if (!globalThis.navigator?.clipboard?.writeText) return false
  await globalThis.navigator.clipboard.writeText(value)
  return true
}

const copyValue = async (value?: string | null) => {
  try {
    const ok = await copyText(value)
    if (!ok) {
      pushToast({ key: 'errors.copyNotSupported', tone: 'error' })
      return
    }
    pushToast({ key: 'toast.copied', tone: 'success' })
  } catch {
    pushToast({ key: 'errors.copyFailed', tone: 'error' })
  }
}

onMounted(loadProfile)
</script>

<template>
  <div class="mx-auto max-w-6xl space-y-6">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <RouterLink
          class="text-sm text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
          :to="`/admin/events/${eventId}`"
        >
          {{ t('nav.backToEvent') }}
        </RouterLink>
        <h1 class="mt-1 text-2xl font-semibold text-slate-900">
          {{ profile?.fullName ?? t('admin.participantProfile.title') }}
        </h1>
        <div class="mt-2 flex flex-wrap items-center gap-2">
          <span
            v-if="profile"
            class="rounded-full px-3 py-1 text-xs font-semibold"
            :class="profile.arrived ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'"
          >
            {{ profile.arrived ? t('common.arrivedLabel') : t('common.pendingLabel') }}
          </span>
          <span
            v-if="profile?.tcNoDuplicate"
            class="rounded-full bg-amber-100 px-3 py-1 text-xs font-semibold text-amber-700"
          >
            {{ t('admin.participantProfile.tcNoDuplicate') }}
          </span>
        </div>
      </div>
    </div>

    <LoadingState v-if="loading" message-key="common.loading" />
    <ErrorState v-else-if="errorMessage" :message="errorMessage" @retry="loadProfile" />

    <template v-else-if="profile">
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.participantProfile.sections.core') }}</h2>
        <div class="mt-4 space-y-3 text-sm">
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.fullName') }}</span>
            <span class="text-slate-900">{{ profile.fullName }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.phone') }}</span>
            <div class="flex flex-wrap items-center gap-2">
              <span class="text-slate-900">{{ formatPhoneDisplay(profile.phone) }}</span>
              <button
                class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                type="button"
                @click="copyValue(profile.phone)"
              >
                {{ t('common.copy') }}
              </button>
            </div>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.email') }}</span>
            <div class="flex flex-wrap items-center gap-2">
              <span class="text-slate-900">{{ displayValue(profile.email ?? undefined) }}</span>
              <button
                class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                type="button"
                :disabled="!profile.email"
                @click="copyValue(profile.email ?? undefined)"
              >
                {{ t('common.copy') }}
              </button>
            </div>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.tcNo') }}</span>
            <div class="flex flex-wrap items-center gap-2">
              <span class="text-slate-900">{{ profile.tcNo }}</span>
              <button
                class="rounded border border-slate-200 bg-white px-2 py-1 text-xs text-slate-600 hover:border-slate-300"
                type="button"
                @click="copyValue(profile.tcNo)"
              >
                {{ t('common.copy') }}
              </button>
            </div>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.birthDate') }}</span>
            <span class="text-slate-900">{{ formatDate(profile.birthDate) }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.gender') }}</span>
            <span class="text-slate-900">{{ formatGender(profile.gender) }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.checkInCode') }}</span>
            <span class="text-slate-900 font-mono">{{ profile.checkInCode }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivedAt') }}</span>
            <span class="text-slate-900">
              {{ profile.arrivedAt ? `${formatDate(profile.arrivedAt)} ${formatTime(profile.arrivedAt)}` : t('common.noData') }}
            </span>
          </div>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.participantProfile.sections.hotel') }}</h2>
        <div class="mt-4 space-y-3 text-sm">
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.roomNo') }}</span>
            <span class="text-slate-900">{{ displayValue(details?.roomNo ?? undefined) }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.roomType') }}</span>
            <span class="text-slate-900">{{ displayValue(details?.roomType ?? undefined) }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.personNo') }}</span>
            <span class="text-slate-900">{{ displayValue(details?.personNo ?? undefined) }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.hotelCheckInDate') }}</span>
            <span class="text-slate-900">{{ formatDate(details?.hotelCheckInDate) }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.hotelCheckOutDate') }}</span>
            <span class="text-slate-900">{{ formatDate(details?.hotelCheckOutDate) }}</span>
          </div>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.participantProfile.sections.flights') }}</h2>
        <div class="mt-4 grid gap-6 lg:grid-cols-2">
          <div class="rounded-xl border border-slate-200 bg-slate-50 p-4">
            <h3 class="text-sm font-semibold text-slate-900">
              {{ t('admin.participantProfile.fields.arrivalTitle') }}
            </h3>
            <div class="mt-3 space-y-3 text-sm">
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivalAirline') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.arrivalAirline ?? undefined) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivalDepartureAirport') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.arrivalDepartureAirport ?? undefined) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivalArrivalAirport') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.arrivalArrivalAirport ?? undefined) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivalFlightCode') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.arrivalFlightCode ?? undefined) }}</span>
              </div>
              <div v-if="details?.arrivalTicketNo || details?.ticketNo" class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivalTicketNo') }}</span>
                <span class="text-slate-900">
                  {{ displayValue(details?.arrivalTicketNo ?? details?.ticketNo ?? undefined) }}
                </span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivalDepartureTime') }}</span>
                <span class="text-slate-900">{{ formatTime(details?.arrivalDepartureTime) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivalArrivalTime') }}</span>
                <span class="text-slate-900">{{ formatTime(details?.arrivalArrivalTime) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivalPnr') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.arrivalPnr ?? undefined) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.arrivalBaggage') }}</span>
                <span class="text-slate-900">
                  {{
                    formatBaggageWithFallback(
                      details?.arrivalBaggagePieces,
                      details?.arrivalBaggageTotalKg,
                      details?.arrivalBaggageAllowance
                    )
                  }}
                </span>
              </div>
            </div>
          </div>

          <div class="rounded-xl border border-slate-200 bg-slate-50 p-4">
            <h3 class="text-sm font-semibold text-slate-900">
              {{ t('admin.participantProfile.fields.returnTitle') }}
            </h3>
            <div class="mt-3 space-y-3 text-sm">
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.returnAirline') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.returnAirline ?? undefined) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.returnDepartureAirport') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.returnDepartureAirport ?? undefined) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.returnArrivalAirport') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.returnArrivalAirport ?? undefined) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.returnFlightCode') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.returnFlightCode ?? undefined) }}</span>
              </div>
              <div v-if="details?.returnTicketNo" class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.returnTicketNo') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.returnTicketNo ?? undefined) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.returnDepartureTime') }}</span>
                <span class="text-slate-900">{{ formatTime(details?.returnDepartureTime) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.returnArrivalTime') }}</span>
                <span class="text-slate-900">{{ formatTime(details?.returnArrivalTime) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.returnPnr') }}</span>
                <span class="text-slate-900">{{ displayValue(details?.returnPnr ?? undefined) }}</span>
              </div>
              <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
                <span class="text-slate-500">{{ t('admin.participantProfile.fields.returnBaggage') }}</span>
                <span class="text-slate-900">
                  {{
                    formatBaggageWithFallback(
                      details?.returnBaggagePieces,
                      details?.returnBaggageTotalKg,
                      details?.returnBaggageAllowance
                    )
                  }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.participantProfile.sections.other') }}</h2>
        <div class="mt-4 space-y-3 text-sm">
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.agencyName') }}</span>
            <span class="text-slate-900">{{ displayValue(details?.agencyName ?? undefined) }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.city') }}</span>
            <span class="text-slate-900">{{ displayValue(details?.city ?? undefined) }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.flightCity') }}</span>
            <span class="text-slate-900">{{ displayValue(details?.flightCity ?? undefined) }}</span>
          </div>
          <div class="grid gap-2 sm:grid-cols-[170px_1fr]">
            <span class="text-slate-500">{{ t('admin.participantProfile.fields.attendanceStatus') }}</span>
            <span class="text-slate-900">{{ displayValue(details?.attendanceStatus ?? undefined) }}</span>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>
