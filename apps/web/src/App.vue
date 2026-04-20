<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import ToastHost from './components/ui/ToastHost.vue'
import AppSegmentedControl from './components/ui/AppSegmentedControl.vue'
import { clearToken, getAuthRole, orgState } from './lib/auth'
import { setLocale, type Locale } from './i18n'
import { useI18n } from 'vue-i18n'
import {
    portalHeaderEndDate,
    portalHeaderLogoUrl,
    portalHeaderName,
    portalHeaderStartDate,
} from './lib/portalHeader'

const router = useRouter()
const route = useRoute()
const { locale, t } = useI18n()
const mobileMenuOpen = ref(false)
const userRole = computed(() => {
  return getAuthRole()
})
const showOrgLink = computed(() => userRole.value === 'SuperAdmin')
const showAuthActions = computed(() => Boolean(userRole.value))
const showUsersLink = computed(() => userRole.value === 'SuperAdmin')
const showGuidesLink = computed(() => userRole.value === 'AgencyAdmin')
const showAuditLogLink = computed(() => userRole.value === 'SuperAdmin' || userRole.value === 'AgencyAdmin')
const shellMaxWidthClass = computed(() => {
  if (route.path.startsWith('/admin') || route.path.startsWith('/guide')) {
    return 'max-w-5xl'
  }

  return 'max-w-3xl'
})
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

const isPortalRoute = computed(() => route.path.startsWith('/e'))
const localeOptions = computed(() => [
  { value: 'en', label: 'EN' },
  { value: 'tr', label: 'TR' },
])

const formatPortalDate = (value?: string) => {
    if (!value) {
        return ''
    }
    const trimmed = value.trim()
    if (!trimmed) {
        return ''
    }
    const datePart = (trimmed.includes('T') ? trimmed.split('T')[0] : trimmed.split(' ')[0]) ?? ''
    const [year, month, day] = datePart.split('-')
    if (year && month && day) {
        return `${day}.${month}.${year}`
    }
    return trimmed
}

const portalDateRange = computed(() => {
    const start = formatPortalDate(portalHeaderStartDate.value)
    const end = formatPortalDate(portalHeaderEndDate.value)
    if (!start || !end) {
        return ''
    }
    return t('common.dateRange', { start, end })
})

const handleLogout = async () => {
  clearToken()
  mobileMenuOpen.value = false
  await router.push('/login')
}

const switchLocale = (value: Locale) => {
  if (locale.value === value) {
    return
  }

  setLocale(value)
}

watch(
  () => route.fullPath,
  () => {
    mobileMenuOpen.value = false
  }
)
</script>

<template>
    <div class="min-h-screen bg-slate-50 text-slate-900">
      <!-- Header -->
        <header class="sticky top-0 z-10 border-b border-slate-200 bg-white/90 backdrop-blur">
            <div :class="['mx-auto px-4 py-3 sm:px-6', shellMaxWidthClass]">
                <div class="flex items-center justify-between gap-3">
                    <div class="leading-tight">
                        <div v-if="isPortalRoute" class="flex items-center gap-3">
                            <img
                                v-if="portalHeaderLogoUrl"
                                :src="portalHeaderLogoUrl"
                                alt=""
                                class="h-10 w-10 rounded-full border border-slate-200 object-cover"
                            />
                            <div>
                                <div class="text-base font-semibold text-slate-900">
                                    {{ portalHeaderName || t('common.event') }}
                                </div>
                                <div v-if="portalDateRange" class="text-[11px] text-slate-500">
                                    {{ portalDateRange }}
                                </div>
                            </div>
                        </div>
                        <div v-else>
                            <div class="text-base font-semibold">{{ t('common.appName') }}</div>
                            <div class="text-[11px] uppercase tracking-wide text-slate-500">
                                {{ t('common.sprintLabel') }}
                            </div>
                        </div>
                    </div>

	                    <nav v-if="!isPortalRoute" class="hidden items-center gap-2.5 text-sm text-slate-600 md:flex">
                        <AppSegmentedControl
                            :model-value="locale"
                            :options="localeOptions"
                            size="sm"
                            aria-label="Locale"
                            class-name="min-w-[86px] text-[11px]"
                            @update:model-value="switchLocale($event as Locale)"
                        />
	                        <RouterLink
	                            v-if="showOrgLink"
	                            class="inline-flex w-[14.5ch] items-center justify-start gap-2 whitespace-nowrap transition-colors hover:text-slate-900"
	                            to="/admin/orgs"
	                        >
                            <svg class="h-4 w-4 shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M3 17h14M5 17V6l5-3 5 3v11M7.5 9.5h.01M12.5 9.5h.01M7.5 13h.01M12.5 13h.01" />
                            </svg>
                            {{ t('nav.organizations') }}
                        </RouterLink>
	                        <RouterLink
	                            v-if="eventsPath"
	                            class="inline-flex w-[10ch] items-center justify-start gap-2 whitespace-nowrap transition-colors hover:text-slate-900"
	                            :to="eventsPath"
	                        >
                            <svg class="h-4 w-4 shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M6 2.75v2.5M14 2.75v2.5M3 7.25h14M4.75 5h10.5A1.75 1.75 0 0 1 17 6.75v8.5A1.75 1.75 0 0 1 15.25 17H4.75A1.75 1.75 0 0 1 3 15.25v-8.5A1.75 1.75 0 0 1 4.75 5Z" />
                            </svg>
                            {{ t('nav.events') }}
                        </RouterLink>
	                        <RouterLink
	                            v-if="showAuditLogLink"
	                            class="inline-flex w-[14ch] items-center justify-start gap-2 whitespace-nowrap transition-colors hover:text-slate-900"
	                            to="/admin/audit-log"
	                        >
                            <svg class="h-4 w-4 shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M4.75 4.75h10.5A1.75 1.75 0 0 1 17 6.5v7A1.75 1.75 0 0 1 15.25 15.25H4.75A1.75 1.75 0 0 1 3 13.5v-7A1.75 1.75 0 0 1 4.75 4.75Z" />
                                <path stroke-linecap="round" stroke-linejoin="round" d="M6.5 8.25h7M6.5 11.5h4.5" />
                            </svg>
                            {{ t('nav.auditLog') }}
                        </RouterLink>
	                        <RouterLink
	                            v-if="showUsersLink"
	                            class="inline-flex w-[11ch] items-center justify-start gap-2 whitespace-nowrap transition-colors hover:text-slate-900"
	                            to="/admin/users"
	                        >
                            <svg class="h-4 w-4 shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M6.5 7a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm7 1a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Zm-12 8.5a5 5 0 0 1 10 0M11.5 16.5a4 4 0 0 1 7 0" />
                            </svg>
                            {{ t('nav.users') }}
                        </RouterLink>
	                        <RouterLink
	                            v-if="showGuidesLink"
	                            class="inline-flex w-[10.5ch] items-center justify-start gap-2 whitespace-nowrap transition-colors hover:text-slate-900"
	                            to="/admin/guides"
	                        >
                            <svg class="h-4 w-4 shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M10 17.25s5-2.8 5-7.1a5 5 0 1 0-10 0c0 4.3 5 7.1 5 7.1Zm0-5.5a1.75 1.75 0 1 0 0-3.5 1.75 1.75 0 0 0 0 3.5Z" />
                            </svg>
                            {{ t('nav.guides') }}
                        </RouterLink>
	                        <button
	                            v-if="showAuthActions"
	                            class="inline-flex min-w-[8.5ch] items-center justify-center gap-2 whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-2 text-xs font-medium text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50"
	                            type="button"
	                            @click="handleLogout"
	                        >
                            <svg class="h-4 w-4 shrink-0 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M7.75 4.75H5.5A1.75 1.75 0 0 0 3.75 6.5v7A1.75 1.75 0 0 0 5.5 15.25h2.25M11.5 13.5 14.75 10m0 0L11.5 6.5m3.25 3.5h-8.5" />
                            </svg>
                            {{ t('nav.logout') }}
                        </button>
                    </nav>

                    <div v-if="!isPortalRoute" class="flex items-center gap-2 md:hidden">
                        <AppSegmentedControl
                            :model-value="locale"
                            :options="localeOptions"
                            size="sm"
                            aria-label="Locale"
                            class-name="min-w-[78px] text-[11px]"
                            @update:model-value="switchLocale($event as Locale)"
                        />

                        <button
                            class="inline-flex items-center justify-center rounded border border-slate-200 bg-white p-2 text-slate-700 hover:border-slate-300"
                            type="button"
                            :aria-expanded="mobileMenuOpen"
                            aria-label="Menu"
                            @click="mobileMenuOpen = !mobileMenuOpen"
                        >
                            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path stroke-linecap="round" stroke-linejoin="round" d="M4 6h16M4 12h16M4 18h16" />
                            </svg>
                        </button>
                    </div>
                </div>

                <Transition name="app-menu">
                    <div v-if="mobileMenuOpen && !isPortalRoute" class="pt-3 md:hidden">
                        <nav class="space-y-1 rounded-xl border border-slate-200 bg-white p-2 text-sm text-slate-700 shadow-sm">
                            <RouterLink
                                v-if="showOrgLink"
                                class="flex items-center gap-2 rounded-lg px-3 py-2 transition-colors hover:bg-slate-50"
                                to="/admin/orgs"
                            >
                                <svg class="h-4 w-4 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M3 17h14M5 17V6l5-3 5 3v11M7.5 9.5h.01M12.5 9.5h.01M7.5 13h.01M12.5 13h.01" />
                                </svg>
                                {{ t('nav.organizations') }}
                            </RouterLink>
                            <RouterLink
                                v-if="eventsPath"
                                class="flex items-center gap-2 rounded-lg px-3 py-2 transition-colors hover:bg-slate-50"
                                :to="eventsPath"
                            >
                                <svg class="h-4 w-4 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M6 2.75v2.5M14 2.75v2.5M3 7.25h14M4.75 5h10.5A1.75 1.75 0 0 1 17 6.75v8.5A1.75 1.75 0 0 1 15.25 17H4.75A1.75 1.75 0 0 1 3 15.25v-8.5A1.75 1.75 0 0 1 4.75 5Z" />
                                </svg>
                                {{ t('nav.events') }}
                            </RouterLink>
                            <RouterLink
                                v-if="showAuditLogLink"
                                class="flex items-center gap-2 rounded-lg px-3 py-2 transition-colors hover:bg-slate-50"
                                to="/admin/audit-log"
                            >
                                <svg class="h-4 w-4 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M4.75 4.75h10.5A1.75 1.75 0 0 1 17 6.5v7A1.75 1.75 0 0 1 15.25 15.25H4.75A1.75 1.75 0 0 1 3 13.5v-7A1.75 1.75 0 0 1 4.75 4.75Z" />
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M6.5 8.25h7M6.5 11.5h4.5" />
                                </svg>
                                {{ t('nav.auditLog') }}
                            </RouterLink>
                            <RouterLink
                                v-if="showUsersLink"
                                class="flex items-center gap-2 rounded-lg px-3 py-2 transition-colors hover:bg-slate-50"
                                to="/admin/users"
                            >
                                <svg class="h-4 w-4 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M6.5 7a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm7 1a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Zm-12 8.5a5 5 0 0 1 10 0M11.5 16.5a4 4 0 0 1 7 0" />
                                </svg>
                                {{ t('nav.users') }}
                            </RouterLink>
                            <RouterLink
                                v-if="showGuidesLink"
                                class="flex items-center gap-2 rounded-lg px-3 py-2 transition-colors hover:bg-slate-50"
                                to="/admin/guides"
                            >
                                <svg class="h-4 w-4 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M10 17.25s5-2.8 5-7.1a5 5 0 1 0-10 0c0 4.3 5 7.1 5 7.1Zm0-5.5a1.75 1.75 0 1 0 0-3.5 1.75 1.75 0 0 0 0 3.5Z" />
                                </svg>
                                {{ t('nav.guides') }}
                            </RouterLink>
                            <button
                                v-if="showAuthActions"
                                class="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left transition-colors hover:bg-slate-50"
                                type="button"
                                @click="handleLogout"
                            >
                                <svg class="h-4 w-4 text-slate-400" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8">
                                    <path stroke-linecap="round" stroke-linejoin="round" d="M7.75 4.75H5.5A1.75 1.75 0 0 0 3.75 6.5v7A1.75 1.75 0 0 0 5.5 15.25h2.25M11.5 13.5 14.75 10m0 0L11.5 6.5m3.25 3.5h-8.5" />
                                </svg>
                                {{ t('nav.logout') }}
                            </button>
                        </nav>
                    </div>
                </Transition>
            </div>
        </header>

        <!-- Content -->
        <main :class="['mx-auto w-full px-4 py-4 sm:px-6 sm:py-6', shellMaxWidthClass]">
            <RouterView />
        </main>
    </div>
    <ToastHost />
</template>
