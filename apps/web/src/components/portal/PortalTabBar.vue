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
        <span>{{ tab.label }}</span>
      </button>
    </div>
  </nav>
</template>
