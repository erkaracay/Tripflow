<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiGet, apiPost } from '../../lib/api'
import { getToken, getTokenRole, isTokenExpired } from '../../lib/auth'
import { sanitizeEventAccessCode, isValidEventCodeLength } from '../../lib/eventAccessCode'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type { Event as EventDto, EventListItem } from '../../types'

const router = useRouter()
const { t } = useI18n()
const { pushToast } = useToast()
const events = ref<EventListItem[]>([])
const loading = ref(true)
const submitting = ref(false)
const listErrorKey = ref<string | null>(null)
const listErrorMessage = ref<string | null>(null)
const formErrorKey = ref<string | null>(null)
const formErrorMessage = ref<string | null>(null)
const dateHintKey = ref<string | null>(null)
const showArchived = ref(false)

const EVENT_CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'

const form = reactive({
  name: '',
  startDate: '',
  endDate: '',
  eventCode: '',
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

const loadEvents = async () => {
  loading.value = true
  listErrorKey.value = null
  listErrorMessage.value = null
  try {
    const query = showArchived.value ? '?includeArchived=true' : ''
    events.value = await apiGet<EventListItem[]>(`/api/events${query}`)
  } catch (err) {
    listErrorMessage.value = err instanceof Error ? err.message : null
    if (!listErrorMessage.value) {
      listErrorKey.value = 'errors.events.load'
    }
  } finally {
    loading.value = false
  }
}

const eventCodeErrorKey = ref<string | null>(null)

const generateRandomEventCode = () => {
  let code = ''
  for (let i = 0; i < 8; i++) {
    code += EVENT_CODE_ALPHABET[Math.floor(Math.random() * EVENT_CODE_ALPHABET.length)]
  }
  form.eventCode = code
  eventCodeErrorKey.value = null
}

const createEvent = async () => {
  formErrorKey.value = null
  formErrorMessage.value = null
  eventCodeErrorKey.value = null

  if (!form.name.trim()) {
    formErrorKey.value = 'validation.eventNameRequired'
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

  const code = sanitizeEventAccessCode(form.eventCode)
  if (code && !isValidEventCodeLength(code)) {
    eventCodeErrorKey.value = 'admin.events.form.eventCodeInvalid'
    return
  }

  submitting.value = true
  try {
    const body: { name: string; startDate: string; endDate: string; eventAccessCode?: string } = {
      name: form.name,
      startDate: form.startDate,
      endDate: form.endDate,
    }
    if (code) body.eventAccessCode = code

    const created = await apiPost<EventDto>('/api/events', body)

    await router.push(`/admin/events/${created.id}`)
  } catch (err: unknown) {
    const payload = err && typeof err === 'object' && 'payload' in err ? (err as { payload?: { code?: string } }).payload : undefined
    const apiCode = payload?.code
    if (apiCode === 'event_access_code_taken') {
      formErrorKey.value = null
      formErrorMessage.value = null
      eventCodeErrorKey.value = 'admin.events.form.eventCodeTaken'
      return
    }
    formErrorMessage.value = err instanceof Error ? err.message : null
    if (!formErrorMessage.value) {
      formErrorKey.value = 'errors.events.create'
    }
  } finally {
    submitting.value = false
  }
}

const copyText = async (value: string) => {
  if (!value) {
    return false
  }

  if (!globalThis.navigator?.clipboard?.writeText) {
    return false
  }

  await globalThis.navigator.clipboard.writeText(value)
  return true
}

const copyEventAccessCode = async (code: string) => {
  try {
    const ok = await copyText(code)
    if (!ok) {
      pushToast({ key: 'errors.copyNotSupported', tone: 'error' })
      return
    }
    pushToast({ key: 'toast.eventAccessCodeCopied', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.eventAccessCodeCopyFailed', tone: 'error' })
  }
}

const buildPortalLoginLink = (code?: string) => {
  const base = globalThis.location?.origin ?? ''
  if (!base) return ''
  if (code) return `${base}/e/login?code=${encodeURIComponent(code)}`
  return `${base}/e/login`
}

const copyPortalLoginLink = async (code?: string) => {
  const loginUrl = buildPortalLoginLink(code)
  try {
    const ok = await copyText(loginUrl)
    if (!ok) {
      pushToast({ key: 'errors.copyNotSupported', tone: 'error' })
      return
    }
    pushToast({ key: 'toast.portalLinkCopied', tone: 'success' })
  } catch {
    pushToast({ key: 'toast.portalLinkCopyFailed', tone: 'error' })
  }
}

onMounted(loadEvents)
</script>

<template>
  <div class="space-y-8">
    <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="flex flex-col gap-2">
          <h1 class="text-xl font-semibold">{{ t('admin.events.title') }}</h1>
          <p class="text-sm text-slate-600">{{ t('admin.events.subtitle') }}</p>
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

      <form class="mt-5 grid gap-4 md:grid-cols-3" @submit.prevent="createEvent">
        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.events.form.nameLabel') }}</span>
          <input
            v-model.trim="form.name"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            :placeholder="t('admin.events.form.namePlaceholder')"
            type="text"
          />
        </label>

        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.events.form.startDateLabel') }}</span>
          <input
            v-model="form.startDate"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="date"
          />
        </label>

        <label class="grid gap-1 text-sm">
          <span class="text-slate-600">{{ t('admin.events.form.endDateLabel') }}</span>
          <input
            v-model="form.endDate"
            class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            type="date"
            :min="form.startDate || undefined"
          />
        </label>

        <label class="grid gap-1 text-sm md:col-span-3">
          <span class="text-slate-600">{{ t('admin.events.form.eventCodeLabel') }}</span>
          <div class="flex flex-wrap items-center gap-2">
            <input
              v-model="form.eventCode"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm font-mono uppercase tracking-wider focus:border-slate-400 focus:outline-none max-w-48"
              :class="eventCodeErrorKey ? 'border-rose-400' : ''"
              :placeholder="t('admin.events.form.eventCodePlaceholder')"
              maxlength="10"
              autocomplete="off"
              autocapitalize="characters"
              :disabled="submitting"
              @input="form.eventCode = sanitizeEventAccessCode(form.eventCode); eventCodeErrorKey = null"
            />
            <button
              type="button"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-600 hover:border-slate-300"
              :disabled="submitting"
              @click="generateRandomEventCode"
            >
              {{ t('admin.events.form.eventCodeGenerate') }}
            </button>
          </div>
          <p class="text-xs text-slate-500">{{ t('admin.events.form.eventCodeHint') }}</p>
          <p v-if="eventCodeErrorKey" class="text-xs text-rose-600">{{ t(eventCodeErrorKey) }}</p>
        </label>

        <div class="md:col-span-3 flex flex-wrap items-center gap-3">
          <button
            class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
            :disabled="submitting"
            type="submit"
          >
            {{ submitting ? t('admin.events.form.creating') : t('admin.events.form.create') }}
          </button>
          <button
            class="text-sm text-slate-600 underline"
            type="button"
            @click="loadEvents"
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
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h2 class="text-lg font-semibold">{{ t('admin.events.list.title') }}</h2>
        <div class="flex items-center gap-4 text-xs text-slate-500">
          <label class="flex items-center gap-2 text-slate-600">
            <input v-model="showArchived" type="checkbox" class="rounded border-slate-300" @change="loadEvents" />
            {{ t('admin.events.list.showArchived') }}
          </label>
          <span>{{ events.length }} {{ t('common.total') }}</span>
        </div>
      </div>

      <LoadingState v-if="loading && events.length === 0" message-key="admin.events.list.loading" />

      <ErrorState
        v-if="listErrorKey || listErrorMessage"
        :message="listErrorMessage ?? undefined"
        :message-key="listErrorKey ?? undefined"
        @retry="loadEvents"
      />

      <div
        v-if="!loading && events.length === 0"
        class="rounded border border-dashed border-slate-200 bg-white p-6 text-sm text-slate-500"
      >
        {{ t('admin.events.list.empty') }}
      </div>

      <ul v-if="events.length > 0" class="space-y-3">
        <li
          v-for="event in events"
          :key="event.id"
          class="flex flex-col gap-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm md:flex-row md:items-center md:justify-between"
        >
          <div>
            <div class="flex items-center gap-2">
              <div class="font-medium">{{ event.name }}</div>
              <span
                v-if="event.isDeleted"
                class="rounded-full border border-rose-200 bg-rose-50 px-2 py-0.5 text-xs text-rose-700"
              >
                {{ t('common.archived') }}
              </span>
            </div>
            <div class="text-xs text-slate-500">
              {{ t('common.dateRange', { start: event.startDate, end: event.endDate }) }}
            </div>
            <div class="mt-2 inline-flex items-center rounded-full bg-slate-100 px-3 py-1 text-xs text-slate-700">
              {{ t('common.arrivedSummary', { arrived: event.arrivedCount, total: event.totalCount }) }}
            </div>
            <div class="mt-2 flex flex-wrap items-center gap-2 text-xs">
              <span class="text-[11px] uppercase tracking-[0.12em] text-slate-400">
                {{ t('admin.events.list.accessCode') }}
              </span>
              <button
                class="rounded border border-slate-200 bg-white px-2 py-1 font-mono tracking-[0.12em] text-slate-700 hover:border-slate-300"
                type="button"
                @click="copyEventAccessCode(event.eventAccessCode)"
              >
                {{ event.eventAccessCode }}
              </button>
              <button
                class="rounded border border-slate-200 bg-white px-2 py-1 text-slate-600 hover:border-slate-300"
                type="button"
                @click="copyPortalLoginLink(event.eventAccessCode)"
              >
                {{ t('admin.events.list.copyLoginLink') }}
              </button>
            </div>
          </div>
          <div class="flex items-center gap-4 text-sm">
            <RouterLink
              class="text-slate-700 underline hover:text-slate-900"
              :to="`/admin/events/${event.id}`"
            >
              {{ t('admin.events.list.manage') }}
            </RouterLink>
            <a
              class="text-slate-700 underline hover:text-slate-900"
              :href="buildPortalLoginLink(event.eventAccessCode)"
              rel="noreferrer"
              target="_blank"
            >
              {{ t('admin.events.list.openPortal') }}
            </a>
          </div>
        </li>
      </ul>
    </section>
  </div>
</template>
