<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

const { t } = useI18n()

const modalTitleId = 'import-template-help-title'
const previousBodyOverflow = ref<string | null>(null)

const generalItems = [
  'admin.import.helper.generalTemplateRule',
  'admin.import.helper.generalLanguageRule',
  'admin.import.helper.generalEmptyRowsRule',
] as const

const participantItems = [
  'admin.import.helper.participantsCoreRule',
  'admin.import.helper.participantsNoFlightRule',
  'admin.import.helper.participantsUpdateRule',
] as const

const segmentItems = [
  'admin.import.helper.segmentsRequiredRule',
  'admin.import.helper.segmentsDirectionRule',
  'admin.import.helper.segmentsIndexRule',
  'admin.import.helper.segmentsDateTimeRule',
  'admin.import.helper.segmentsBaggageRule',
] as const

const commonIssueItems = [
  'admin.import.helper.issueDirectionRule',
  'admin.import.helper.issueNotFoundRule',
  'admin.import.helper.issueLegacyIgnoredRule',
  'admin.import.helper.issuePreviewLimitRule',
] as const

const tipItems = [
  'admin.import.helper.tipTwoByTwoRule',
  'admin.import.helper.tipGroupByTcRule',
] as const

const closeModal = () => {
  emit('close')
}

const onKeydown = (event: KeyboardEvent) => {
  if (event.key !== 'Escape' || !props.open) {
    return
  }

  event.preventDefault()
  closeModal()
}

watch(
  () => props.open,
  (open) => {
    if (typeof document === 'undefined') {
      return
    }

    if (open) {
      previousBodyOverflow.value = document.body.style.overflow
      document.body.style.overflow = 'hidden'
      return
    }

    if (previousBodyOverflow.value !== null) {
      document.body.style.overflow = previousBodyOverflow.value
      previousBodyOverflow.value = null
    }
  }
)

onMounted(() => {
  globalThis.window?.addEventListener('keydown', onKeydown)
})

onUnmounted(() => {
  globalThis.window?.removeEventListener('keydown', onKeydown)
  if (typeof document !== 'undefined' && previousBodyOverflow.value !== null) {
    document.body.style.overflow = previousBodyOverflow.value
    previousBodyOverflow.value = null
  }
})
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 px-4 py-6"
    @click.self="closeModal"
  >
    <div
      role="dialog"
      aria-modal="true"
      :aria-labelledby="modalTitleId"
      class="w-full max-w-3xl overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-xl"
    >
      <div class="flex items-start justify-between gap-3 border-b border-slate-200 px-4 py-4 sm:px-5">
        <div>
          <h3 :id="modalTitleId" class="text-lg font-semibold text-slate-900">
            {{ t('admin.import.helper.title') }}
          </h3>
          <p class="mt-1 text-sm text-slate-600">
            {{ t('admin.import.helper.subtitle') }}
          </p>
        </div>

        <button
          type="button"
          class="rounded border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:border-slate-300"
          @click="closeModal"
        >
          {{ t('admin.import.helper.close') }}
        </button>
      </div>

      <div class="max-h-[75vh] overflow-y-auto px-4 py-4 sm:px-5">
        <section class="mb-5">
          <h4 class="text-sm font-semibold text-slate-900">{{ t('admin.import.helper.sectionGeneral') }}</h4>
          <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-700">
            <li v-for="key in generalItems" :key="key">{{ t(key) }}</li>
          </ul>
        </section>

        <section class="mb-5">
          <h4 class="text-sm font-semibold text-slate-900">{{ t('admin.import.helper.sectionParticipants') }}</h4>
          <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-700">
            <li v-for="key in participantItems" :key="key">{{ t(key) }}</li>
          </ul>
        </section>

        <section class="mb-5">
          <h4 class="text-sm font-semibold text-slate-900">{{ t('admin.import.helper.sectionSegments') }}</h4>
          <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-700">
            <li v-for="key in segmentItems" :key="key">{{ t(key) }}</li>
          </ul>

          <div class="mt-3 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2">
            <div class="text-xs font-semibold uppercase tracking-wide text-slate-500">
              {{ t('admin.import.helper.requiredColumnsLabel') }}
            </div>
            <code class="mt-1 block whitespace-pre-wrap text-xs text-slate-700">
              {{ t('admin.import.helper.requiredColumnsValue') }}
            </code>
          </div>
        </section>

        <section class="mb-5">
          <h4 class="text-sm font-semibold text-slate-900">{{ t('admin.import.helper.sectionCommonIssues') }}</h4>
          <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-700">
            <li v-for="key in commonIssueItems" :key="key">{{ t(key) }}</li>
          </ul>
        </section>

        <section>
          <h4 class="text-sm font-semibold text-slate-900">{{ t('admin.import.helper.sectionTips') }}</h4>
          <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-700">
            <li v-for="key in tipItems" :key="key">{{ t(key) }}</li>
          </ul>
        </section>
      </div>
    </div>
  </div>
</template>
