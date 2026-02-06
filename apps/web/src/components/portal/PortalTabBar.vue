<script setup lang="ts">
type TabItem = {
  id: string
  label: string
}

const props = defineProps<{ tabs: TabItem[]; active: string }>()
const emit = defineEmits<{ (event: 'select', id: string): void }>()

const onSelect = (id: string) => {
  emit('select', id)
}

</script>

<template>
  <nav class="w-full border-t border-slate-200 bg-white/95 backdrop-blur md:hidden">
    <div class="flex items-center justify-between gap-2 px-4 py-3 sm:px-6">
      <button
        v-for="tab in props.tabs"
        :key="tab.id"
        class="flex w-full flex-1 flex-col items-center justify-center gap-1 rounded-xl px-2 py-2 text-xs font-medium"
        :class="
          props.active === tab.id
            ? 'bg-slate-900 text-white'
            : 'text-slate-600 hover:bg-slate-100'
        "
        type="button"
        @click="onSelect(tab.id)"
      >
        <span class="flex h-5 w-5 items-center justify-center">
          <svg
            v-if="tab.id === 'days'"
            viewBox="0 0 24 24"
            class="h-4 w-4"
            fill="none"
            stroke="currentColor"
            stroke-width="1.6"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M7 2v3M17 2v3M3 9h18" />
            <path d="M5 6h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z" />
          </svg>
          <svg
            v-else-if="tab.id === 'docs'"
            viewBox="0 0 24 24"
            class="h-4 w-4"
            fill="none"
            stroke="currentColor"
            stroke-width="1.6"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M7 2h7l5 5v13a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z" />
            <path d="M14 2v6h6" />
            <path d="M9 13h6M9 17h6" />
          </svg>
          <svg
            v-else-if="tab.id === 'qr'"
            viewBox="0 0 24 24"
            class="h-4 w-4"
            fill="none"
            stroke="currentColor"
            stroke-width="1.6"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <rect x="3" y="3" width="6" height="6" rx="1" />
            <rect x="15" y="3" width="6" height="6" rx="1" />
            <rect x="3" y="15" width="6" height="6" rx="1" />
            <rect x="13" y="13" width="2" height="2" rx="0.5" />
            <rect x="19" y="19" width="2" height="2" rx="0.5" />
            <rect x="15" y="17" width="2" height="2" rx="0.5" />
          </svg>
          <svg
            v-else
            viewBox="0 0 24 24"
            class="h-4 w-4"
            fill="none"
            stroke="currentColor"
            stroke-width="1.6"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <circle cx="12" cy="12" r="9" />
            <path d="M12 10v6" />
            <path d="M12 7h.01" />
          </svg>
        </span>
        <span>{{ tab.label }}</span>
      </button>
    </div>
  </nav>
</template>
