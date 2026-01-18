<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <div class="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 text-sm shadow-sm">
    <h1 class="text-xl font-semibold">{{ t('auth.forbidden.title') }}</h1>
    <p class="text-slate-600">{{ t('auth.forbidden.message') }}</p>
    <RouterLink class="text-slate-700 underline" to="/login">
      {{ t('auth.forbidden.back') }}
    </RouterLink>
  </div>
</template>
