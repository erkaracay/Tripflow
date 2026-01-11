<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import ToastHost from './components/ui/ToastHost.vue'
import { clearToken, getToken, getTokenRole, isTokenExpired } from './lib/auth'

const router = useRouter()
const userRole = computed(() => {
  const token = getToken()
  if (!token || isTokenExpired(token)) {
    return null
  }

  return getTokenRole(token)
})
const showAuthActions = computed(() => Boolean(userRole.value))
const toursPath = computed(() => {
  if (userRole.value === 'Guide') {
    return '/guide/tours'
  }

  if (userRole.value === 'Admin') {
    return '/admin/tours'
  }

  return ''
})

const handleLogout = async () => {
  clearToken()
  await router.push('/login')
}
</script>

<template>
    <div class="min-h-screen bg-slate-50 text-slate-900">
      <!-- Header -->
        <header class="sticky top-0 z-10 border-b border-slate-200 bg-white/90 backdrop-blur">
            <div class="mx-auto flex max-w-3xl items-center justify-between px-4 py-3 sm:px-6">
                <div class="leading-tight">
                    <div class="text-base font-semibold">Tripflow</div>
                    <div class="text-[11px] uppercase tracking-wide text-slate-500">
                        Sprint 2 Demo
                    </div>
                </div>

                <nav class="flex items-center gap-3 text-sm text-slate-600">
                    <RouterLink v-if="toursPath" class="hover:text-slate-900" :to="toursPath">
                        Tours
                    </RouterLink>
                    <button
                        v-if="showAuthActions"
                        class="rounded border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-700 hover:border-slate-300"
                        type="button"
                        @click="handleLogout"
                    >
                        Logout
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
