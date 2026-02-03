<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import ToastHost from './components/ui/ToastHost.vue'
import { clearToken, getTokenRole, isTokenExpired, orgState, tokenState } from './lib/auth'
import { setLocale, type Locale } from './i18n'
import { useI18n } from 'vue-i18n'

const router = useRouter()
const { locale, t } = useI18n()
const userRole = computed(() => {
  const token = tokenState.value
  if (!token || isTokenExpired(token)) {
    return null
  }

  return getTokenRole(token)
})
const showOrgLink = computed(() => userRole.value === 'SuperAdmin')
const showAuthActions = computed(() => Boolean(userRole.value))
const showUsersLink = computed(() => userRole.value === 'SuperAdmin')
const showGuidesLink = computed(() => userRole.value === 'AgencyAdmin')
const eventsPath = computed(() => {
  if (userRole.value === 'Guide') {
    return '/guide/events'
  }

  if (userRole.value === 'AgencyAdmin') {
    return '/admin/events'
  }

  if (userRole.value === 'SuperAdmin') {
    return orgState.value ? '/admin/events' : '/admin/orgs'
  }

  return ''
})

const handleLogout = async () => {
  clearToken()
  await router.push('/login')
}

const switchLocale = (value: Locale) => {
  if (locale.value === value) {
    return
  }

  setLocale(value)
}
</script>

<template>
    <div class="min-h-screen bg-slate-50 text-slate-900">
      <!-- Header -->
        <header class="sticky top-0 z-10 border-b border-slate-200 bg-white/90 backdrop-blur">
            <div class="mx-auto flex max-w-3xl items-center justify-between px-4 py-3 sm:px-6">
                <div class="leading-tight">
                    <div class="text-base font-semibold">{{ t('common.appName') }}</div>
                    <div class="text-[11px] uppercase tracking-wide text-slate-500">
                        {{ t('common.sprintLabel') }}
                    </div>
                </div>

                <nav class="flex items-center gap-3 text-sm text-slate-600">
                    <div class="flex items-center rounded-full border border-slate-200 bg-white p-0.5 text-[11px] font-semibold text-slate-600">
                        <button
                            class="rounded-full px-2.5 py-1 transition"
                            :class="locale === 'en' ? 'bg-slate-900 text-white' : 'hover:text-slate-900'"
                            type="button"
                            @click="switchLocale('en')"
                        >
                            EN
                        </button>
                        <button
                            class="rounded-full px-2.5 py-1 transition"
                            :class="locale === 'tr' ? 'bg-slate-900 text-white' : 'hover:text-slate-900'"
                            type="button"
                            @click="switchLocale('tr')"
                        >
                            TR
                        </button>
                    </div>
                    <RouterLink v-if="showOrgLink" class="hover:text-slate-900" to="/admin/orgs">
                        {{ t('nav.organizations') }}
                    </RouterLink>
                    <RouterLink v-if="eventsPath" class="hover:text-slate-900" :to="eventsPath">
                        {{ t('nav.events') }}
                    </RouterLink>
                    <RouterLink v-if="showUsersLink" class="hover:text-slate-900" to="/admin/users">
                        {{ t('nav.users') }}
                    </RouterLink>
                    <RouterLink v-if="showGuidesLink" class="hover:text-slate-900" to="/admin/guides">
                        {{ t('nav.guides') }}
                    </RouterLink>
                    <button
                        v-if="showAuthActions"
                        class="rounded border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300"
                        type="button"
                        @click="handleLogout"
                    >
                        {{ t('nav.logout') }}
                    </button>
                </nav>
            </div>
        </header>

        <!-- Content -->
        <main class="mx-auto w-full max-w-3xl px-4 py-4 sm:px-6 sm:py-6">
            <RouterView />
        </main>
    </div>
    <ToastHost />
</template>
