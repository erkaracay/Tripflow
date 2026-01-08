<script setup lang="ts">
import { RouterLink, RouterView } from "vue-router";
</script>

<template>
    <div class="min-h-screen bg-slate-50 text-slate-900">
      <!-- Header -->
        <header class="sticky top-0 z-10 border-b border-slate-200 bg-white/90 backdrop-blur">
            <div class="mx-auto flex max-w-3xl items-center justify-between px-4 py-3 sm:px-6">
                <div class="leading-tight">
                    <div class="text-base font-semibold">Tripflow</div>
                    <div class="text-[11px] uppercase tracking-wide text-slate-500">
                        Sprint 1
                    </div>
                </div>

                <nav class="flex items-center gap-3 text-sm text-slate-600">
                    <RouterLink class="hover:text-slate-900" to="/admin/tours">
                        Tours
                    </RouterLink>
                </nav>
            </div>
        </header>

        <!-- Content -->
        <main class="mx-auto w-full max-w-3xl px-4 py-4 sm:px-6 sm:py-6">
            <RouterView />
        </main>
    </div>
</template>
