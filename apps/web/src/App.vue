<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import ToastHost from './components/ui/ToastHost.vue'
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
            <div class="mx-auto max-w-3xl px-4 py-3 sm:px-6">
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

                    <nav v-if="!isPortalRoute" class="hidden items-center gap-3 text-sm text-slate-600 md:flex">
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

                    <div v-if="!isPortalRoute" class="flex items-center gap-2 md:hidden">
                        <div class="flex items-center rounded-full border border-slate-200 bg-white p-0.5 text-[11px] font-semibold text-slate-600">
                            <button
                                class="rounded-full px-2 py-0.5 transition"
                                :class="locale === 'en' ? 'bg-slate-900 text-white' : 'hover:text-slate-900'"
                                type="button"
                                @click="switchLocale('en')"
                            >
                                EN
                            </button>
                            <button
                                class="rounded-full px-2 py-0.5 transition"
                                :class="locale === 'tr' ? 'bg-slate-900 text-white' : 'hover:text-slate-900'"
                                type="button"
                                @click="switchLocale('tr')"
                            >
                                TR
                            </button>
                        </div>

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

                <div v-if="mobileMenuOpen && !isPortalRoute" class="pt-3 md:hidden">
                    <nav class="space-y-1 rounded-xl border border-slate-200 bg-white p-2 text-sm text-slate-700 shadow-sm">
                        <RouterLink
                            v-if="showOrgLink"
                            class="block rounded-lg px-3 py-2 hover:bg-slate-50"
                            to="/admin/orgs"
                        >
                            {{ t('nav.organizations') }}
                        </RouterLink>
                        <RouterLink
                            v-if="eventsPath"
                            class="block rounded-lg px-3 py-2 hover:bg-slate-50"
                            :to="eventsPath"
                        >
                            {{ t('nav.events') }}
                        </RouterLink>
                        <RouterLink
                            v-if="showUsersLink"
                            class="block rounded-lg px-3 py-2 hover:bg-slate-50"
                            to="/admin/users"
                        >
                            {{ t('nav.users') }}
                        </RouterLink>
                        <RouterLink
                            v-if="showGuidesLink"
                            class="block rounded-lg px-3 py-2 hover:bg-slate-50"
                            to="/admin/guides"
                        >
                            {{ t('nav.guides') }}
                        </RouterLink>
                        <button
                            v-if="showAuthActions"
                            class="block w-full rounded-lg px-3 py-2 text-left hover:bg-slate-50"
                            type="button"
                            @click="handleLogout"
                        >
                            {{ t('nav.logout') }}
                        </button>
                    </nav>
                </div>
            </div>
        </header>

        <!-- Content -->
        <main class="mx-auto w-full max-w-3xl px-4 py-4 sm:px-6 sm:py-6">
            <RouterView />
        </main>
    </div>
    <ToastHost />
</template>
