<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPost } from '../../lib/api'
import { getToken, getTokenRole, isTokenExpired } from '../../lib/auth'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Tour, TourListItem } from '../../types'

const router = useRouter()
const { t } = useI18n()
const tours = ref<TourListItem[]>([])
const loading = ref(true)
const submitting = ref(false)
const listErrorKey = ref<string | null>(null)
const listErrorMessage = ref<string | null>(null)
const formErrorKey = ref<string | null>(null)
const formErrorMessage = ref<string | null>(null)
const dateHintKey = ref<string | null>(null)

const form = reactive({
  name: '',
  startDate: '',
  endDate: '',
})

const isSuperAdmin = computed(() => {
  const token = getToken()
  if (!token || isTokenExpired(token)) {
    return false
  }

  return getTokenRole(token) === 'SuperAdmin'
})

watch(
  () => form.startDate,
  (value) => {
    dateHintKey.value = null
    if (form.endDate && value && form.endDate < value) {
      form.endDate = value
      dateHintKey.value = 'validation.endDateAdjusted'
    }
  }
)

const loadTours = async () => {
  loading.value = true
  listErrorKey.value = null
  listErrorMessage.value = null
  try {
    tours.value = await apiGet<TourListItem[]>('/api/tours')
  } catch (err) {
    listErrorMessage.value = err instanceof Error ? err.message : null
    if (!listErrorMessage.value) {
      listErrorKey.value = 'errors.tours.load'
    }
  } finally {
    loading.value = false
  }
}

const createTour = async () => {
  formErrorKey.value = null
  formErrorMessage.value = null

  if (!form.name.trim()) {
    formErrorKey.value = 'validation.tourNameRequired'
    return
  }

  if (!form.startDate || !form.endDate) {
    formErrorKey.value = 'validation.startEndRequired'
    return
  }

  if (form.endDate < form.startDate) {
    formErrorKey.value = 'validation.endAfterStart'
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
    formErrorMessage.value = err instanceof Error ? err.message : null
    if (!formErrorMessage.value) {
      formErrorKey.value = 'errors.tours.create'
    }
  } finally {
    submitting.value = false
  }
}

onMounted(loadTours)
</script>

<template>
  <div class="space-y-8">
    <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="flex flex-col gap-2">
          <h1 class="text-xl font-semibold">{{ t('admin.tours.title') }}</h1>
          <p class="text-sm text-slate-600">{{ t('admin.tours.subtitle') }}</p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <RouterLink
            v-if="isSuperAdmin"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 shadow-sm hover:border-slate-300"
            to="/admin/orgs"
          >
            {{ t('nav.backToOrganizations') }}
          </RouterLink>
        </div>
      </div>

      <form class="mt-5 grid gap-4 md:grid-cols-3" @submit.prevent="createTour">
        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.tours.form.nameLabel') }}</span>
          <input
            v-model.trim="form.name"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            :placeholder="t('admin.tours.form.namePlaceholder')"
            type="text"
          />
        </label>

        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.tours.form.startDateLabel') }}</span>
          <input
            v-model="form.startDate"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="date"
          />
        </label>

        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.tours.form.endDateLabel') }}</span>
          <input
            v-model="form.endDate"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="date"
            :min="form.startDate || undefined"
          />
        </label>

        <div class="md:col-span-3 flex flex-wrap items-center gap-3">
          <button
            class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
            :disabled="submitting"
            type="submit"
          >
            {{ submitting ? t('admin.tours.form.creating') : t('admin.tours.form.create') }}
          </button>
          <button
            class="text-sm text-slate-600 underline"
            type="button"
            @click="loadTours"
          >
            {{ t('common.refreshList') }}
          </button>
        </div>
      </form>

      <p v-if="formErrorKey || formErrorMessage" class="mt-3 text-sm text-rose-600">
        {{ formErrorKey ? t(formErrorKey) : formErrorMessage }}
      </p>
      <p v-else-if="dateHintKey" class="mt-3 text-sm text-slate-500">{{ t(dateHintKey) }}</p>
    </section>

    <section class="space-y-4">
      <div class="flex items-center justify-between">
        <h2 class="text-lg font-semibold">{{ t('admin.tours.list.title') }}</h2>
        <span class="text-xs text-slate-500">{{ tours.length }} {{ t('common.total') }}</span>
      </div>

      <LoadingState v-if="loading && tours.length === 0" message-key="admin.tours.list.loading" />

      <ErrorState
        v-if="listErrorKey || listErrorMessage"
        :message="listErrorMessage ?? undefined"
        :message-key="listErrorKey ?? undefined"
        @retry="loadTours"
      />

      <div
        v-if="!loading && tours.length === 0"
        class="rounded border border-dashed border-slate-200 bg-white p-6 text-sm text-slate-500"
      >
        {{ t('admin.tours.list.empty') }}
      </div>

      <ul v-if="tours.length > 0" class="space-y-3">
        <li
          v-for="tour in tours"
          :key="tour.id"
          class="flex flex-col gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm md:flex-row md:items-center md:justify-between"
        >
          <div>
            <div class="font-medium">{{ tour.name }}</div>
            <div class="text-xs text-slate-500">
              {{ t('common.dateRange', { start: tour.startDate, end: tour.endDate }) }}
            </div>
            <div class="mt-2 inline-flex items-center rounded-full bg-slate-100 px-3 py-1 text-xs text-slate-700">
              {{ t('common.arrivedSummary', { arrived: tour.arrivedCount, total: tour.totalCount }) }}
            </div>
          </div>
          <div class="flex items-center gap-4 text-sm">
            <RouterLink
              class="text-slate-700 underline hover:text-slate-900"
              :to="`/admin/tours/${tour.id}`"
            >
              {{ t('admin.tours.list.manage') }}
            </RouterLink>
            <a
              class="text-slate-700 underline hover:text-slate-900"
              :href="`/t/${tour.id}`"
              rel="noreferrer"
              target="_blank"
            >
              {{ t('admin.tours.list.openPortal') }}
            </a>
          </div>
        </li>
      </ul>
    </section>
  </div>
</template>
