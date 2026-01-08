<script setup lang="ts">
import { onMounted, ref } from "vue";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string;
const health = ref<string>("loading...");

onMounted(async () => {
    try {
        const res = await fetch(`${apiBaseUrl}/health`);
        const data = await res.json();
        health.value = data?.status ?? "unknown";
    } catch (e) {
        health.value = "error";
    }
});
</script>

<template>
    <div class="min-h-screen p-6">
        <div class="max-w-xl space-y-4">
            <h1 class="text-2xl font-semibold">Tripflow</h1>

            <div class="rounded border p-4">
                <div class="text-sm text-zinc-600">API Base URL</div>
                <div class="font-mono text-sm">{{ apiBaseUrl }}</div>

                <div class="mt-3 text-sm text-zinc-600">Health</div>
                <div class="text-lg">{{ health }}</div>
            </div>
        </div>
    </div>
</template>
