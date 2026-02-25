<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPut } from '../../lib/api'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Event as EventDto, EventContacts } from '../../types'

const props = defineProps<{ eventId?: string }>()
const route = useRoute()
const { t } = useI18n()
const { pushToast } = useToast()

const eventId = computed(() => (props.eventId ?? route.params.eventId) as string)

const loading = ref(true)
const loadErrorKey = ref<string | null>(null)
const loadErrorMessage = ref<string | null>(null)

const saving = ref(false)
const saveErrorKey = ref<string | null>(null)
const saveErrorMessage = ref<string | null>(null)

const event = ref<EventDto | null>(null)
const savedBadgeVisible = ref(false)
let savedBadgeTimer: ReturnType<typeof setTimeout> | null = null

const form = reactive({
  guideName: '',
  guidePhone: '',
  leaderName: '',
  leaderPhone: '',
  emergencyPhone: '',
  whatsappGroupUrl: '',
})

const fieldErrors = reactive({
  whatsappGroupUrl: '',
})

const clearErrors = () => {
  saveErrorKey.value = null
  saveErrorMessage.value = null
  fieldErrors.whatsappGroupUrl = ''
}

const setFormFromContacts = (value: EventContacts | null | undefined) => {
  form.guideName = value?.guideName ?? ''
  form.guidePhone = value?.guidePhone ?? ''
  form.leaderName = value?.leaderName ?? ''
  form.leaderPhone = value?.leaderPhone ?? ''
  form.emergencyPhone = value?.emergencyPhone ?? ''
  form.whatsappGroupUrl = value?.whatsappGroupUrl ?? ''
}

const toNull = (value: string) => {
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}

const isValidHttpsAbsoluteUrl = (value: string) => {
  try {
    const parsed = new URL(value)
    return parsed.protocol === 'https:'
  } catch {
    return false
  }
}

const validate = () => {
  fieldErrors.whatsappGroupUrl = ''

  const groupUrl = form.whatsappGroupUrl.trim()
  if (groupUrl && !isValidHttpsAbsoluteUrl(groupUrl)) {
    fieldErrors.whatsappGroupUrl = t('admin.contacts.errors.invalidWhatsappGroupUrl')
    return false
  }

  return true
}

const showSavedBadge = () => {
  savedBadgeVisible.value = true
  if (savedBadgeTimer) {
    clearTimeout(savedBadgeTimer)
  }
  savedBadgeTimer = setTimeout(() => {
    savedBadgeVisible.value = false
    savedBadgeTimer = null
  }, 2500)
}

const loadData = async () => {
  loading.value = true
  loadErrorKey.value = null
  loadErrorMessage.value = null

  try {
    const [eventData, contacts] = await Promise.all([
      apiGet<EventDto>(`/api/events/${eventId.value}`),
      apiGet<EventContacts>(`/api/events/${eventId.value}/contacts`),
    ])

    event.value = eventData
    setFormFromContacts(contacts)
  } catch (err) {
    loadErrorMessage.value = err instanceof Error ? err.message : null
    if (!loadErrorMessage.value) {
      loadErrorKey.value = 'errors.events.load'
    }
  } finally {
    loading.value = false
  }
}

const save = async () => {
  clearErrors()

  if (!validate()) {
    return
  }

  saving.value = true
  try {
    const payload = {
      guideName: toNull(form.guideName),
      guidePhone: toNull(form.guidePhone),
      leaderName: toNull(form.leaderName),
      leaderPhone: toNull(form.leaderPhone),
      emergencyPhone: toNull(form.emergencyPhone),
      whatsappGroupUrl: toNull(form.whatsappGroupUrl),
    }

    const saved = await apiPut<EventContacts>(`/api/events/${eventId.value}/contacts`, payload)
    setFormFromContacts(saved)
    showSavedBadge()
    pushToast({ key: 'admin.contacts.saved', tone: 'success' })
  } catch (err) {
    const apiError = err as Error & { payload?: unknown }
    const payload = apiError.payload

    if (payload && typeof payload === 'object' && 'code' in payload) {
      const code = String((payload as { code?: string }).code ?? '')
      if (code === 'invalid_whatsapp_group_url') {
        fieldErrors.whatsappGroupUrl = t('admin.contacts.errors.invalidWhatsappGroupUrl')
      }
    }

    if (!fieldErrors.whatsappGroupUrl) {
      saveErrorMessage.value = err instanceof Error ? err.message : null
      if (!saveErrorMessage.value) {
        saveErrorKey.value = 'errors.generic'
      }
    }

    pushToast({ key: 'errors.generic', tone: 'error' })
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  void loadData()
})

onBeforeUnmount(() => {
  if (savedBadgeTimer) {
    clearTimeout(savedBadgeTimer)
  }
})
</script>

<template>
  <div class="mx-auto max-w-4xl p-4 sm:p-6">
    <div class="mb-4">
      <RouterLink
        class="text-sm text-slate-500 hover:text-slate-700"
        :to="`/admin/events/${eventId}`"
      >
        {{ t('nav.backToEvent') }}
      </RouterLink>
    </div>

    <LoadingState v-if="loading" message-key="admin.eventDetail.loading" />

    <ErrorState
      v-else-if="loadErrorKey || loadErrorMessage"
      :message-key="loadErrorKey || undefined"
      :message="loadErrorMessage || undefined"
      @retry="loadData"
    />

    <section v-else class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <div class="mb-6 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-xl font-semibold text-slate-900">{{ t('admin.contacts.title') }}</h1>
          <p class="text-sm text-slate-500">
            {{ t('admin.contacts.subtitle', { event: event?.name ?? '' }) }}
          </p>
        </div>
      </div>

      <form class="space-y-5" @submit.prevent="save">
        <div class="grid gap-4 md:grid-cols-2">
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.contacts.guideName') }}</span>
            <input
              v-model.trim="form.guideName"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="text"
            />
          </label>

          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.contacts.guidePhone') }}</span>
            <input
              v-model.trim="form.guidePhone"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="tel"
            />
          </label>

          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.contacts.leaderName') }}</span>
            <input
              v-model.trim="form.leaderName"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="text"
            />
          </label>

          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.contacts.leaderPhone') }}</span>
            <input
              v-model.trim="form.leaderPhone"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="tel"
            />
          </label>

          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.contacts.emergencyPhone') }}</span>
            <input
              v-model.trim="form.emergencyPhone"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              type="tel"
            />
          </label>

          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.contacts.whatsappGroupUrl') }}</span>
            <input
              v-model.trim="form.whatsappGroupUrl"
              class="rounded border bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
              :class="fieldErrors.whatsappGroupUrl ? 'border-rose-300' : 'border-slate-200'"
              type="url"
              placeholder="https://chat.whatsapp.com/..."
            />
            <span v-if="fieldErrors.whatsappGroupUrl" class="text-xs text-rose-600">{{ fieldErrors.whatsappGroupUrl }}</span>
          </label>
        </div>

        <div v-if="saveErrorKey || saveErrorMessage" class="text-sm text-rose-600">
          {{ saveErrorKey ? t(saveErrorKey) : saveErrorMessage }}
        </div>

        <div class="flex items-center gap-3">
          <button
            class="inline-flex items-center rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            :disabled="saving"
            type="submit"
          >
            <span v-if="saving" class="mr-2 h-3 w-3 animate-spin rounded-full border border-white/60 border-t-transparent"></span>
            {{ saving ? t('admin.contacts.saving') : t('admin.contacts.save') }}
          </button>

          <span v-if="savedBadgeVisible" class="rounded border border-emerald-200 bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700">
            {{ t('admin.contacts.saved') }}
          </span>
        </div>
      </form>
    </section>
  </div>
</template>
