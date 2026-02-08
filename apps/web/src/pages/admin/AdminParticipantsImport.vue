<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { apiDownload, apiGet, apiPostForm } from '../../lib/api'
import { formatBaggage, formatDate, formatTime } from '../../lib/formatters'
import { useToast } from '../../lib/toast'
import LoadingState from '../../components/ui/LoadingState.vue'
import ErrorState from '../../components/ui/ErrorState.vue'
import type {
  Event as EventDto,
  ParticipantImportError,
  ParticipantImportPreviewRow,
  ParticipantImportReport,
  ParticipantImportWarning,
} from '../../types'

type ImportFormat = 'xlsx' | 'csv'
type HeaderLanguage = 'auto' | 'tr' | 'en'
type IssueFilter = 'all' | 'errors' | 'warnings'
type RowStatus = 'ok' | 'warn' | 'error'

type ImportIssueRow = {
  row: number
  status: RowStatus
  tcNo?: string
  participant: string
  message: string
}

type ApiError = Error & { status?: number; payload?: unknown }

const MAX_FILE_SIZE = 10 * 1024 * 1024

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const { pushToast } = useToast()

const eventId = computed(() => String(route.params.eventId ?? ''))

const loading = ref(true)
const loadError = ref<string | null>(null)
const event = ref<EventDto | null>(null)

const selectedFormat = ref<ImportFormat>('xlsx')
const headerLanguage = ref<HeaderLanguage>('auto')
const selectedFile = ref<File | null>(null)
const fileError = ref<string | null>(null)
const dragActive = ref(false)

const previewLoading = ref(false)
const applyLoading = ref(false)
const report = ref<ParticipantImportReport | null>(null)
const finalReport = ref<ParticipantImportReport | null>(null)
const reportError = ref<string | null>(null)
const issueFilter = ref<IssueFilter>('all')
const search = ref('')

const retryAfterSeconds = ref(0)
let retryTimer: ReturnType<typeof setInterval> | null = null

const summary = computed(() => {
  const current = finalReport.value ?? report.value
  if (!current) {
    return null
  }

  const validRows =
    typeof current.validRows === 'number'
      ? current.validRows
      : Math.max(current.totalRows - current.failed, 0)
  const skippedRows = typeof current.skipped === 'number' ? current.skipped : current.failed
  const errorRows = typeof current.errorCount === 'number' ? current.errorCount : current.errors.length
  return {
    totalRows: current.totalRows,
    validRows,
    importedWouldBe: current.imported,
    updatedWouldBe: current.updated,
    skippedRows,
    errorRows,
  }
})

const issueRows = computed<ImportIssueRow[]>(() => {
  const current = finalReport.value ?? report.value
  if (!current) {
    return []
  }

  const rows: ImportIssueRow[] = []
  for (const warning of current.warnings) {
    rows.push(mapWarningRow(warning))
  }
  for (const error of current.errors) {
    rows.push(mapErrorRow(error))
  }

  return rows.sort((a, b) => a.row - b.row)
})

const previewRows = computed<ParticipantImportPreviewRow[]>(() => {
  const current = finalReport.value ?? report.value
  return current?.parsedPreviewRows ?? []
})

const previewLimit = computed(() => {
  const current = finalReport.value ?? report.value
  return current?.previewLimit ?? 0
})

const previewTruncated = computed(() => {
  const current = finalReport.value ?? report.value
  return current?.previewTruncated ?? false
})

const filteredIssueRows = computed(() => {
  const term = search.value.trim().toLowerCase()

  return issueRows.value.filter((row) => {
    const passesFilter =
      issueFilter.value === 'all' ||
      (issueFilter.value === 'errors' && row.status === 'error') ||
      (issueFilter.value === 'warnings' && row.status === 'warn')

    if (!passesFilter) {
      return false
    }

    if (!term) {
      return true
    }

    return (
      String(row.row).includes(term) ||
      row.participant.toLowerCase().includes(term) ||
      row.message.toLowerCase().includes(term)
    )
  })
})

const canPreview = computed(() => Boolean(selectedFile.value) && !previewLoading.value && retryAfterSeconds.value === 0)
const canApply = computed(
  () => Boolean(selectedFile.value) && Boolean(report.value) && !applyLoading.value && retryAfterSeconds.value === 0
)

const pageTitle = computed(() => `${t('admin.import.title')} - ${event.value?.name ?? t('common.event')}`)

const formatBytes = (size: number) => {
  if (size < 1024) {
    return `${size} B`
  }
  if (size < 1024 * 1024) {
    return `${(size / 1024).toFixed(1)} KB`
  }
  return `${(size / (1024 * 1024)).toFixed(1)} MB`
}

const clearRetryTimer = () => {
  if (retryTimer) {
    globalThis.clearInterval(retryTimer)
    retryTimer = null
  }
}

const startRetryCountdown = (seconds: number) => {
  clearRetryTimer()
  retryAfterSeconds.value = Math.max(seconds, 1)
  retryTimer = globalThis.setInterval(() => {
    retryAfterSeconds.value -= 1
    if (retryAfterSeconds.value <= 0) {
      retryAfterSeconds.value = 0
      clearRetryTimer()
    }
  }, 1000)
}

const loadEvent = async () => {
  loading.value = true
  loadError.value = null
  try {
    event.value = await apiGet<EventDto>(`/api/events/${eventId.value}`)
  } catch (err) {
    loadError.value = err instanceof Error ? err.message : t('errors.eventDetail.load')
  } finally {
    loading.value = false
  }
}

const validateFile = (file: File) => {
  const extension = file.name.split('.').pop()?.toLowerCase()
  if (!extension || !['csv', 'xlsx'].includes(extension)) {
    fileError.value = t('admin.import.fileTypeError')
    return false
  }

  if (file.size > MAX_FILE_SIZE) {
    fileError.value = t('admin.import.fileSizeError')
    return false
  }

  fileError.value = null
  return true
}

const setFile = (file: File | null) => {
  if (!file) {
    selectedFile.value = null
    fileError.value = null
    return
  }

  if (!validateFile(file)) {
    selectedFile.value = null
    return
  }

  selectedFile.value = file
}

const onFileSelected = (eventTarget: globalThis.Event) => {
  const input = eventTarget.target as HTMLInputElement
  const file = input.files?.[0] ?? null
  setFile(file)
}

const onDrop = (eventTarget: DragEvent) => {
  eventTarget.preventDefault()
  dragActive.value = false
  const file = eventTarget.dataTransfer?.files?.[0] ?? null
  setFile(file)
}

const removeFile = () => {
  selectedFile.value = null
  fileError.value = null
}

const downloadTemplate = async () => {
  try {
    const blob = await apiDownload(
      `/api/events/${eventId.value}/participants/import/template?format=${selectedFormat.value}`
    )
    const url = globalThis.URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `participants_template.${selectedFormat.value}`
    anchor.click()
    globalThis.URL.revokeObjectURL(url)
  } catch {
    pushToast({ key: 'errors.generic', tone: 'error' })
  }
}

const buildFormData = () => {
  const formData = new FormData()
  if (selectedFile.value) {
    formData.append('file', selectedFile.value)
  }
  return formData
}

const extractRetryAfter = (error: ApiError) => {
  if (!error.payload || typeof error.payload !== 'object') {
    return 0
  }

  const payload = error.payload as { retryAfterSeconds?: unknown }
  const value = Number(payload.retryAfterSeconds)
  if (!Number.isFinite(value) || value <= 0) {
    return 0
  }

  return value
}

const extractReport = (error: ApiError): ParticipantImportReport | null => {
  if (!error.payload || typeof error.payload !== 'object') {
    return null
  }

  const payload = error.payload as ParticipantImportReport
  if (typeof payload.totalRows !== 'number' || !Array.isArray(payload.errors) || !Array.isArray(payload.warnings)) {
    return null
  }

  return payload
}

const runPreview = async () => {
  if (!selectedFile.value || !canPreview.value) {
    return
  }

  previewLoading.value = true
  reportError.value = null

  try {
    const response = await apiPostForm<ParticipantImportReport>(
      `/api/events/${eventId.value}/participants/import?mode=dryrun`,
      buildFormData()
    )
    report.value = response
    finalReport.value = null
    pushToast({ key: 'admin.import.previewReadyToast', tone: 'success' })
  } catch (err) {
    const apiError = err as ApiError
    const payloadReport = extractReport(apiError)
    if (payloadReport) {
      report.value = payloadReport
      finalReport.value = null
      reportError.value = t('admin.import.previewBlocked')
      return
    }
    if (apiError.status === 429) {
      const seconds = extractRetryAfter(apiError) || 60
      startRetryCountdown(seconds)
      reportError.value = t('admin.import.rateLimited', { seconds })
    } else {
      reportError.value = err instanceof Error ? err.message : t('errors.generic')
    }
  } finally {
    previewLoading.value = false
  }
}

const applyImport = async () => {
  if (!selectedFile.value || !canApply.value) {
    return
  }

  applyLoading.value = true
  reportError.value = null

  try {
    const response = await apiPostForm<ParticipantImportReport>(
      `/api/events/${eventId.value}/participants/import?mode=apply`,
      buildFormData()
    )
    finalReport.value = response
    report.value = response
    reportError.value = null
    pushToast({ key: 'admin.import.applySuccessToast', tone: 'success' })
  } catch (err) {
    const apiError = err as ApiError
    const payloadReport = extractReport(apiError)
    if (payloadReport) {
      finalReport.value = payloadReport
      report.value = payloadReport
      reportError.value = t('admin.import.applyBlocked')
      pushToast({ key: 'admin.import.applyErrorToast', tone: 'error' })
      return
    }
    if (apiError.status === 429) {
      const seconds = extractRetryAfter(apiError) || 60
      startRetryCountdown(seconds)
      reportError.value = t('admin.import.rateLimited', { seconds })
    } else {
      reportError.value = err instanceof Error ? err.message : t('errors.generic')
      pushToast({ key: 'admin.import.applyErrorToast', tone: 'error' })
    }
  } finally {
    applyLoading.value = false
  }
}

const downloadReportCsv = () => {
  const current = finalReport.value ?? report.value
  if (!current) {
    return
  }

  const lines: string[] = []
  lines.push('metric,value')
  lines.push(`totalRows,${current.totalRows}`)
  lines.push(`imported,${current.imported}`)
  lines.push(`created,${current.created}`)
  lines.push(`updated,${current.updated}`)
  lines.push(`failed,${current.failed}`)
  lines.push('')
  lines.push('row,status,tcNo,message')

  for (const row of issueRows.value) {
    const message = row.message.split('"').join('""')
    lines.push(`${row.row},${row.status},${row.tcNo ?? ''},"${message}"`)
  }

  const csv = lines.join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
  const url = globalThis.URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `participants_import_report_${eventId.value}.csv`
  anchor.click()
  globalThis.URL.revokeObjectURL(url)
}

const backToParticipants = async () => {
  await router.push({
    path: `/admin/events/${eventId.value}`,
    query: { imported: String(Date.now()) },
  })
}

const mapWarningRow = (warning: ParticipantImportWarning): ImportIssueRow => ({
  row: warning.row,
  status: 'warn',
  tcNo: warning.tcNo ?? undefined,
  participant: warning.tcNo ? `TC ${warning.tcNo}` : t('common.noData'),
  message: translateImportWarning(warning),
})

const mapErrorRow = (error: ParticipantImportError): ImportIssueRow => ({
  row: error.row,
  status: 'error',
  tcNo: error.tcNo ?? undefined,
  participant: error.tcNo ? `TC ${error.tcNo}` : t('common.noData'),
  message: translateImportError(error),
})

const translateImportWarning = (warning: ParticipantImportWarning) => {
  if (warning.code === 'duplicate_tcno') {
    return t('admin.import.messages.duplicateTcNo')
  }
  if (warning.code === 'legacy_template_detected') {
    return t('admin.import.messages.legacyTemplateDetected')
  }

  return warning.message
}

const translateImportError = (error: ParticipantImportError) => {
  const message = error.message.toLowerCase()
  if (message.includes('duplicate tcno in file')) return t('admin.import.messages.duplicateTcNo')
  if (message.includes('matches multiple existing participants')) return t('admin.import.messages.tcNoAmbiguous')
  if (message.includes('full_name required')) return t('admin.import.messages.fullNameRequired')
  if (message.includes('phone required')) return t('admin.import.messages.phoneRequired')
  if (message.includes('tc_no must be 11 digits')) return t('admin.import.messages.tcNoInvalid')
  if (message.includes('birth_date invalid')) return t('admin.import.messages.birthDateInvalid')
  if (message.includes('gender invalid')) return t('admin.import.messages.genderInvalid')
  if (message.includes('hotel_check_in_date invalid')) return t('admin.import.messages.hotelCheckInInvalid')
  if (message.includes('hotel_check_out_date invalid')) return t('admin.import.messages.hotelCheckOutInvalid')
  if (message.includes('arrival_departure_time invalid')) return t('admin.import.messages.arrivalDepartureInvalid')
  if (message.includes('arrival_arrival_time invalid')) return t('admin.import.messages.arrivalArrivalInvalid')
  if (message.includes('return_departure_time invalid')) return t('admin.import.messages.returnDepartureInvalid')
  if (message.includes('return_arrival_time invalid')) return t('admin.import.messages.returnArrivalInvalid')
  return error.message
}

void loadEvent()

onBeforeUnmount(() => {
  clearRetryTimer()
})
</script>

<template>
  <div class="mx-auto max-w-6xl space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <RouterLink
          class="text-sm text-slate-600 underline-offset-2 hover:text-slate-900 hover:underline"
          :to="`/admin/events/${eventId}`"
        >
          {{ t('admin.import.backToEvent') }}
        </RouterLink>
        <h1 class="mt-1 text-2xl font-semibold text-slate-900">{{ pageTitle }}</h1>
      </div>
    </div>

    <LoadingState v-if="loading" message-key="common.loading" />
    <ErrorState
      v-else-if="loadError"
      :message="loadError"
      @retry="loadEvent"
    />

    <template v-else>
      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.import.stepTemplate') }}</h2>
        <p class="mt-1 text-sm text-slate-500">{{ t('admin.import.templateNote') }}</p>

        <div class="mt-4 grid gap-4 sm:grid-cols-2">
          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.import.fileFormat') }}</span>
            <select
              v-model="selectedFormat"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            >
              <option value="xlsx">XLSX</option>
              <option value="csv">CSV</option>
            </select>
          </label>

          <label class="grid gap-1 text-sm">
            <span class="text-slate-600">{{ t('admin.import.headerLanguage') }}</span>
            <select
              v-model="headerLanguage"
              class="rounded border border-slate-200 bg-white px-3 py-2 text-sm focus:border-slate-400 focus:outline-none"
            >
              <option value="auto">{{ t('admin.import.headerLangAuto') }}</option>
              <option value="tr">{{ t('admin.import.headerLangTr') }}</option>
              <option value="en">{{ t('admin.import.headerLangEn') }}</option>
            </select>
          </label>
        </div>

        <div class="mt-4 flex flex-wrap items-center gap-3">
          <button
            class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
            type="button"
            @click="downloadTemplate"
          >
            {{ t('admin.import.downloadTemplate') }}
          </button>
          <span class="text-xs text-slate-500">
            {{ t('admin.import.autoDetectRecommended', { language: t(`admin.import.headerLang${headerLanguage.charAt(0).toUpperCase()}${headerLanguage.slice(1)}`) }) }}
          </span>
        </div>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.import.stepUpload') }}</h2>

        <label
          class="mt-4 block cursor-pointer rounded-2xl border-2 border-dashed p-6 text-center transition"
          :class="dragActive ? 'border-slate-400 bg-slate-50' : 'border-slate-300 hover:border-slate-400'"
          @dragover.prevent="dragActive = true"
          @dragleave.prevent="dragActive = false"
          @drop="onDrop"
        >
          <input class="hidden" type="file" accept=".csv,.xlsx" @change="onFileSelected" />
          <p class="text-sm text-slate-700">{{ t('admin.import.dropzoneTitle') }}</p>
          <p class="mt-1 text-xs text-slate-500">{{ t('admin.import.dropzoneHint') }}</p>
        </label>

        <div v-if="selectedFile" class="mt-4 flex flex-wrap items-center justify-between gap-2 rounded border border-slate-200 bg-slate-50 px-3 py-2 text-sm">
          <div>
            <div class="font-medium text-slate-800">{{ selectedFile.name }}</div>
            <div class="text-xs text-slate-500">{{ formatBytes(selectedFile.size) }}</div>
          </div>
          <button class="rounded border border-slate-200 bg-white px-2 py-1 text-xs" type="button" @click="removeFile">
            {{ t('common.remove') }}
          </button>
        </div>

        <p v-if="fileError" class="mt-3 text-xs text-rose-600">{{ fileError }}</p>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.import.stepPreview') }}</h2>
          <button
            class="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            :disabled="!canPreview"
            type="button"
            @click="runPreview"
          >
            {{ previewLoading ? t('common.loading') : t('admin.import.previewImport') }}
          </button>
        </div>

        <div v-if="retryAfterSeconds > 0" class="mt-4 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          {{ t('admin.import.rateLimited', { seconds: retryAfterSeconds }) }}
        </div>

        <p v-if="reportError" class="mt-3 text-sm text-rose-600">{{ reportError }}</p>

        <template v-if="summary">
          <div class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <div class="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2">
              <div class="text-xs text-slate-500">{{ t('admin.import.summary.totalRows') }}</div>
              <div class="text-lg font-semibold">{{ summary.totalRows }}</div>
            </div>
            <div class="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2">
              <div class="text-xs text-slate-500">{{ t('admin.import.summary.validRows') }}</div>
              <div class="text-lg font-semibold">{{ summary.validRows }}</div>
            </div>
            <div class="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2">
              <div class="text-xs text-slate-500">{{ t('admin.import.summary.importedWouldBe') }}</div>
              <div class="text-lg font-semibold">{{ summary.importedWouldBe }}</div>
            </div>
            <div class="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2">
              <div class="text-xs text-slate-500">{{ t('admin.import.summary.updatedWouldBe') }}</div>
              <div class="text-lg font-semibold">{{ summary.updatedWouldBe }}</div>
            </div>
            <div class="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2">
              <div class="text-xs text-slate-500">{{ t('admin.import.summary.skippedRows') }}</div>
              <div class="text-lg font-semibold">{{ summary.skippedRows }}</div>
            </div>
            <div class="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2">
              <div class="text-xs text-slate-500">{{ t('admin.import.summary.errorRows') }}</div>
              <div class="text-lg font-semibold">{{ summary.errorRows }}</div>
            </div>
          </div>

          <div class="mt-4 flex flex-wrap items-center gap-2">
            <button class="rounded border px-3 py-1 text-xs" :class="issueFilter === 'all' ? 'border-slate-900 text-slate-900' : 'border-slate-200 text-slate-600'" @click="issueFilter = 'all'">
              {{ t('admin.import.filters.all') }}
            </button>
            <button class="rounded border px-3 py-1 text-xs" :class="issueFilter === 'errors' ? 'border-rose-500 text-rose-700' : 'border-slate-200 text-slate-600'" @click="issueFilter = 'errors'">
              {{ t('admin.import.filters.errors') }}
            </button>
            <button class="rounded border px-3 py-1 text-xs" :class="issueFilter === 'warnings' ? 'border-amber-500 text-amber-700' : 'border-slate-200 text-slate-600'" @click="issueFilter = 'warnings'">
              {{ t('admin.import.filters.warnings') }}
            </button>

            <input
              v-model.trim="search"
              class="ml-auto w-full max-w-xs rounded border border-slate-200 bg-white px-3 py-1.5 text-xs focus:border-slate-400 focus:outline-none"
              :placeholder="t('admin.import.searchPlaceholder')"
              type="text"
            />
          </div>

          <div class="mt-4 overflow-hidden rounded-xl border border-slate-200">
            <div class="max-h-80 overflow-auto">
              <table class="min-w-full text-sm">
                <thead class="sticky top-0 bg-slate-100 text-left text-xs uppercase tracking-wide text-slate-600">
                  <tr>
                    <th class="px-3 py-2">{{ t('admin.import.table.row') }}</th>
                    <th class="px-3 py-2">{{ t('admin.import.table.status') }}</th>
                    <th class="px-3 py-2">{{ t('admin.import.table.participant') }}</th>
                    <th class="px-3 py-2">{{ t('admin.import.table.message') }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="(row, index) in filteredIssueRows"
                    :key="`${row.status}-${row.row}-${index}`"
                    :class="index % 2 === 0 ? 'bg-white' : 'bg-slate-50'"
                  >
                    <td class="px-3 py-2 font-mono text-xs">{{ row.row }}</td>
                    <td class="px-3 py-2">
                      <span
                        class="inline-flex rounded-full px-2 py-0.5 text-xs"
                        :class="
                          row.status === 'error'
                            ? 'bg-rose-100 text-rose-700'
                            : row.status === 'warn'
                              ? 'bg-amber-100 text-amber-700'
                              : 'bg-emerald-100 text-emerald-700'
                        "
                      >
                        {{ t(`admin.import.table.status${row.status.charAt(0).toUpperCase()}${row.status.slice(1)}`) }}
                      </span>
                    </td>
                    <td class="px-3 py-2 text-xs text-slate-700">{{ row.participant }}</td>
                    <td class="px-3 py-2 text-xs text-slate-700 whitespace-pre-wrap">{{ row.message }}</td>
                  </tr>
                  <tr v-if="filteredIssueRows.length === 0">
                    <td class="px-3 py-4 text-xs text-slate-500" colspan="4">{{ t('admin.import.noRows') }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div v-if="previewRows.length > 0" class="mt-6 space-y-2">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <h3 class="text-sm font-semibold text-slate-900">{{ t('admin.import.previewTitle') }}</h3>
              <span v-if="previewTruncated && previewLimit" class="text-xs text-slate-500">
                {{ t('admin.import.previewTruncated', { count: previewLimit }) }}
              </span>
            </div>
            <div class="overflow-hidden rounded-xl border border-slate-200">
              <div class="max-h-96 overflow-auto">
                <table class="min-w-full text-sm">
                  <thead class="sticky top-0 bg-slate-100 text-left text-xs uppercase tracking-wide text-slate-600">
                    <tr>
                      <th class="px-3 py-2">{{ t('admin.import.previewTable.row') }}</th>
                      <th class="px-3 py-2">{{ t('admin.import.previewTable.name') }}</th>
                      <th class="px-3 py-2">{{ t('admin.import.previewTable.tcNo') }}</th>
                      <th class="px-3 py-2">{{ t('admin.import.previewTable.birthDate') }}</th>
                      <th class="px-3 py-2">{{ t('admin.import.previewTable.arrivalTime') }}</th>
                      <th class="px-3 py-2">{{ t('admin.import.previewTable.returnTime') }}</th>
                      <th class="px-3 py-2">{{ t('admin.import.previewTable.arrivalBaggage') }}</th>
                      <th class="px-3 py-2">{{ t('admin.import.previewTable.returnBaggage') }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="row in previewRows"
                      :key="`preview-${row.rowIndex}`"
                      class="odd:bg-white even:bg-slate-50"
                    >
                      <td class="px-3 py-2 font-mono text-xs">{{ row.rowIndex }}</td>
                      <td class="px-3 py-2 text-xs text-slate-700">{{ row.fullName ?? t('common.noData') }}</td>
                      <td class="px-3 py-2 text-xs text-slate-700">{{ row.tcNo ?? t('common.noData') }}</td>
                      <td class="px-3 py-2 text-xs text-slate-700">{{ formatDate(row.birthDate) }}</td>
                      <td class="px-3 py-2 text-xs text-slate-700">
                        {{ formatTime(row.arrivalDepartureTime) }} → {{ formatTime(row.arrivalArrivalTime) }}
                      </td>
                      <td class="px-3 py-2 text-xs text-slate-700">
                        {{ formatTime(row.returnDepartureTime) }} → {{ formatTime(row.returnArrivalTime) }}
                      </td>
                      <td class="px-3 py-2 text-xs text-slate-700">
                        {{ formatBaggage(row.arrivalBaggagePieces, row.arrivalBaggageTotalKg) }}
                      </td>
                      <td class="px-3 py-2 text-xs text-slate-700">
                        {{ formatBaggage(row.returnBaggagePieces, row.returnBaggageTotalKg) }}
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <div class="mt-4 flex flex-wrap items-center gap-2">
            <button class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700" type="button" @click="downloadReportCsv">
              {{ t('admin.import.downloadReport') }}
            </button>
          </div>
        </template>
      </section>

      <section class="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <h2 class="text-lg font-semibold text-slate-900">{{ t('admin.import.stepApply') }}</h2>
          <button
            class="rounded bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-500 disabled:cursor-not-allowed disabled:opacity-60"
            :disabled="!canApply"
            type="button"
            @click="applyImport"
          >
            {{ applyLoading ? t('common.saving') : t('admin.import.applyImport') }}
          </button>
        </div>

        <div v-if="finalReport" class="mt-4 rounded border border-emerald-200 bg-emerald-50 px-3 py-3 text-sm text-emerald-700">
          {{ t('admin.import.applyDone') }}
        </div>

        <div v-if="finalReport" class="mt-4">
          <button class="rounded border border-slate-200 bg-white px-3 py-2 text-sm text-slate-700 hover:border-slate-300" @click="backToParticipants">
            {{ t('admin.import.backToParticipants') }}
          </button>
        </div>
      </section>
    </template>
  </div>
</template>
