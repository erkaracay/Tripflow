<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiGet, apiPost } from '../../lib/api'
import type { Tour } from '../../types'

const router = useRouter()
const tours = ref<Tour[]>([])
const loading = ref(true)
const submitting = ref(false)
const error = ref<string | null>(null)

const form = reactive({
  name: '',
  startDate: '',
  endDate: '',
})

const loadTours = async () => {
  loading.value = true
  error.value = null
  try {
    tours.value = await apiGet<Tour[]>('/api/tours')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load tours.'
  } finally {
    loading.value = false
  }
}

const createTour = async () => {
  error.value = null

  if (!form.name.trim()) {
    error.value = 'Tour name is required.'
    return
  }

  if (!form.startDate || !form.endDate) {
    error.value = 'Start and end dates are required.'
    return
  }

  submitting.value = true
  try {
    const created = await apiPost<Tour>('/api/tours', {
      name: form.name,
      startDate: form.startDate,
      endDate: form.endDate,
    })

    await router.push(`/admin/tours/${created.id}`)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to create tour.'
  } finally {
    submitting.value = false
  }
}

onMounted(loadTours)
</script>

<template>
  <div class="space-y-8">
    <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <div class="flex flex-col gap-2">
        <h1 class="text-xl font-semibold">Tours</h1>
        <p class="text-sm text-slate-600">
          Create a tour, add participants, and open the portal.
        </p>
      </div>

      <form class="mt-5 grid gap-4 md:grid-cols-3" @submit.prevent="createTour">
        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">Tour name</span>
          <input
            v-model.trim="form.name"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            placeholder="Cappadocia Spring 2025"
            type="text"
          />
        </label>

        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">Start date</span>
          <input
            v-model="form.startDate"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="date"
          />
        </label>

        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">End date</span>
          <input
            v-model="form.endDate"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="date"
          />
        </label>

        <div class="md:col-span-3 flex flex-wrap items-center gap-3">
          <button
            class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
            :disabled="submitting"
            type="submit"
          >
            {{ submitting ? 'Creating...' : 'Create tour' }}
          </button>
          <button
            class="text-sm text-slate-600 underline"
            type="button"
            @click="loadTours"
          >
            Refresh list
          </button>
        </div>
      </form>

      <p v-if="error" class="mt-3 text-sm text-rose-600">{{ error }}</p>
    </section>

    <section class="space-y-4">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold">Existing tours</h2>
        <span class="text-xs text-slate-500">{{ tours.length }} total</span>
      </div>

      <div v-if="loading" class="rounded border border-dashed border-slate-200 bg-white p-6 text-sm text-slate-500">
        Loading tours...
      </div>

      <div
        v-else-if="tours.length === 0"
        class="rounded border border-dashed border-slate-200 bg-white p-6 text-sm text-slate-500"
      >
        No tours yet. Create your first tour above.
      </div>

      <ul v-else class="space-y-3">
        <li
          v-for="tour in tours"
          :key="tour.id"
          class="flex flex-col gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm md:flex-row md:items-center md:justify-between"
        >
          <div>
            <div class="font-medium">{{ tour.name }}</div>
            <div class="text-xs text-slate-500">{{ tour.startDate }} to {{ tour.endDate }}</div>
          </div>
          <div class="flex items-center gap-4 text-sm">
            <RouterLink
              class="text-slate-700 underline hover:text-slate-900"
              :to="`/admin/tours/${tour.id}`"
            >
              Manage
            </RouterLink>
            <a
              class="text-slate-700 underline hover:text-slate-900"
              :href="`/t/${tour.id}`"
              rel="noreferrer"
              target="_blank"
            >
              Open portal
            </a>
          </div>
        </li>
      </ul>
    </section>
  </div>
</template>
