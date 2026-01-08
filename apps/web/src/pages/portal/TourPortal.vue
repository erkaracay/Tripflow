<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { apiGet } from '../../lib/api'
import type { Tour, TourPortalInfo } from '../../types'

const route = useRoute()
const tourId = computed(() => route.params.tourId as string)

const tour = ref<Tour | null>(null)
const portal = ref<TourPortalInfo | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const copyStatus = ref<string | null>(null)

const loadPortal = async () => {
  loading.value = true
  error.value = null

  try {
    const [tourData, portalData] = await Promise.all([
      apiGet<Tour>(`/api/tours/${tourId.value}`),
      apiGet<TourPortalInfo>(`/api/tours/${tourId.value}/portal`),
    ])

    tour.value = tourData
    portal.value = portalData
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load tour.'
  } finally {
    loading.value = false
  }
}

const copyShareLink = async () => {
  copyStatus.value = null
  const url = globalThis.location?.href

  if (!url) {
    copyStatus.value = 'Unable to copy link.'
    return
  }

  const clipboard = globalThis.navigator?.clipboard
  if (!clipboard?.writeText) {
    copyStatus.value = 'Copy not supported.'
    return
  }

  try {
    await clipboard.writeText(url)
    copyStatus.value = 'Link copied.'
  } catch {
    copyStatus.value = 'Copy failed.'
  }
}

onMounted(loadPortal)
</script>

<template>
  <div class="space-y-6 sm:space-y-8">
    <div class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-xs uppercase tracking-wide text-slate-500">Participant Portal</p>
          <h1 class="mt-3 text-2xl font-semibold">
            {{ tour?.name ?? 'Tour' }}
          </h1>
          <p class="text-sm text-slate-500" v-if="tour">
            {{ tour.startDate }} to {{ tour.endDate }}
          </p>
        </div>
        <div class="flex w-full flex-col items-start gap-2 sm:w-auto sm:items-end">
          <button
            class="w-full rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
            type="button"
            @click="copyShareLink"
          >
            Copy link
          </button>
          <p v-if="copyStatus" class="text-xs text-slate-500">{{ copyStatus }}</p>
        </div>
      </div>
    </div>

    <div v-if="loading" class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500 sm:p-6">
      Loading tour details...
    </div>

    <p v-else-if="error" class="text-sm text-rose-600">{{ error }}</p>

    <template v-else>
      <div v-if="portal" class="space-y-6 sm:space-y-8">
        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <div class="flex items-center justify-between">
            <h2 class="text-lg font-semibold">Meeting point</h2>
            <span class="text-xs uppercase tracking-wide text-slate-400">Day 0</span>
          </div>
          <div class="mt-4 grid gap-3 text-sm text-slate-700 sm:grid-cols-2">
            <div class="rounded-xl border border-slate-200 bg-slate-50 p-3">
              <p class="text-xs uppercase tracking-wide text-slate-400">Time</p>
              <p class="mt-1 font-medium">{{ portal.meeting.time }}</p>
            </div>
            <div class="rounded-xl border border-slate-200 bg-slate-50 p-3">
              <p class="text-xs uppercase tracking-wide text-slate-400">Place</p>
              <p class="mt-1 font-medium">{{ portal.meeting.place }}</p>
            </div>
            <div class="rounded-xl border border-slate-200 bg-slate-50 p-3 sm:col-span-2">
              <p class="text-xs uppercase tracking-wide text-slate-400">Maps</p>
              <a
                class="mt-2 inline-flex w-full items-center justify-center rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                :href="portal.meeting.mapsUrl"
                rel="noreferrer"
                target="_blank"
              >
                Open in Maps
              </a>
            </div>
            <div class="rounded-xl border border-slate-200 bg-slate-50 p-3 sm:col-span-2">
              <p class="text-xs uppercase tracking-wide text-slate-400">Note</p>
              <p class="mt-2 text-sm text-slate-600">{{ portal.meeting.note }}</p>
            </div>
          </div>
        </section>

        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <h2 class="text-lg font-semibold">Quick links</h2>
          <ul class="mt-4 space-y-2 text-sm">
            <li
              v-for="link in portal.links"
              :key="link.url"
              class="flex flex-col gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
            >
              <span class="font-medium text-slate-700">{{ link.label }}</span>
              <a
                class="inline-flex w-full items-center justify-center rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-medium text-slate-700 shadow-sm hover:border-slate-300 sm:w-auto"
                :href="link.url"
                rel="noreferrer"
                target="_blank"
              >
                Open
              </a>
            </li>
          </ul>
        </section>

        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <h2 class="text-lg font-semibold">Itinerary</h2>
          <div class="mt-4 space-y-4">
            <div
              v-for="day in portal.days"
              :key="day.day"
              class="rounded-xl border border-slate-200 bg-slate-50 p-4 sm:p-5"
            >
              <div class="text-sm font-semibold text-slate-800">Day {{ day.day }} - {{ day.title }}</div>
              <ul class="mt-3 space-y-2 text-sm text-slate-600">
                <li v-for="item in day.items" :key="item" class="flex items-start gap-2">
                  <span class="mt-1 h-1.5 w-1.5 rounded-full bg-slate-400"></span>
                  <span>{{ item }}</span>
                </li>
              </ul>
            </div>
          </div>
        </section>

        <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
          <h2 class="text-lg font-semibold">Notes</h2>
          <ul class="mt-4 space-y-2 text-sm text-slate-600">
            <li v-for="note in portal.notes" :key="note" class="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
              {{ note }}
            </li>
          </ul>
        </section>
      </div>

      <div v-else class="rounded-2xl border border-dashed border-slate-200 bg-white p-4 text-sm text-slate-500 sm:p-6">
        Portal content is not available yet.
      </div>
    </template>
  </div>
</template>
