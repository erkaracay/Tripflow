<script setup lang="ts">
import { computed } from 'vue'

export type InforaIconName =
  | 'calendar' | 'doc' | 'qr' | 'info' | 'map' | 'clock' | 'bell' | 'user'
  | 'bed' | 'plane' | 'shield' | 'bus' | 'plus' | 'arrow' | 'arrowDown'
  | 'check' | 'x' | 'chevronR' | 'chevronD' | 'chevronU' | 'more' | 'moreV'
  | 'search' | 'filter' | 'fork' | 'pin' | 'copy' | 'phone' | 'download'
  | 'external' | 'grid' | 'list' | 'speaker' | 'flag' | 'leaf' | 'sun'
  | 'coffee' | 'utensils' | 'eye' | 'eyeOff' | 'lock' | 'globe' | 'whatsapp'
  | 'maximize' | 'cam' | 'print' | 'alert' | 'signal'

const props = withDefaults(defineProps<{
  name: InforaIconName
  size?: number
  strokeWidth?: number
  iconClass?: string
}>(), {
  size: 18,
  strokeWidth: 1.6,
})

const paths: Record<InforaIconName, string> = {
  calendar: '<rect x="3" y="4.5" width="14" height="13" rx="1.5"/><path d="M3 8h14M7 2.5v3M13 2.5v3"/>',
  doc: '<path d="M5 3h6l4 4v10a1 1 0 01-1 1H5a1 1 0 01-1-1V4a1 1 0 011-1z"/><path d="M11 3v4h4"/>',
  qr: '<rect x="3" y="3" width="5" height="5"/><rect x="12" y="3" width="5" height="5"/><rect x="3" y="12" width="5" height="5"/><path d="M12 12h2v2M16 12v1M12 16v1M14 14h3M14 16.5h3"/>',
  info: '<circle cx="10" cy="10" r="7.5"/><path d="M10 9v5M10 6.5v.1"/>',
  map: '<path d="M2.5 5l5-2 5 2 5-2v12l-5 2-5-2-5 2V5z"/><path d="M7.5 3v14M12.5 5v14"/>',
  clock: '<circle cx="10" cy="10" r="7.5"/><path d="M10 6v4l2.5 2.5"/>',
  bell: '<path d="M10 3a4.5 4.5 0 00-4.5 4.5v3l-1.5 3h12l-1.5-3v-3A4.5 4.5 0 0010 3z"/><path d="M8 16.5a2 2 0 004 0"/>',
  user: '<circle cx="10" cy="7" r="3"/><path d="M4 17c0-3 3-5 6-5s6 2 6 5"/>',
  bed: '<path d="M2.5 15v-5a2 2 0 012-2h11a2 2 0 012 2v5"/><path d="M2.5 15h15M2.5 12h15"/><circle cx="7" cy="10" r="1.5"/>',
  plane: '<path d="M17.5 11l-6 1.5L9 18H7l1.5-6L2 10.5v-1.5l7 1L12 3h2l-1 6 4.5 1z"/>',
  shield: '<path d="M10 2.5l6 2.5v5c0 4-2.8 7.5-6 8-3.2-.5-6-4-6-8V5l6-2.5z"/>',
  bus: '<rect x="3.5" y="5" width="13" height="9" rx="1.5"/><circle cx="7" cy="16" r="1.2"/><circle cx="13" cy="16" r="1.2"/><path d="M3.5 10h13M7 5v-1.5h6V5"/>',
  plus: '<path d="M10 4v12M4 10h12"/>',
  arrow: '<path d="M5 10h10M12 7l3 3-3 3"/>',
  arrowDown: '<path d="M10 4v12M6 11l4 4 4-4"/>',
  check: '<path d="M4.5 10l3.5 3.5L15.5 6.5"/>',
  x: '<path d="M5 5l10 10M15 5L5 15"/>',
  chevronR: '<path d="M8 5l5 5-5 5"/>',
  chevronD: '<path d="M5 8l5 5 5-5"/>',
  chevronU: '<path d="M5 12l5-5 5 5"/>',
  more: '<circle cx="5" cy="10" r=".8" fill="currentColor" stroke="none"/><circle cx="10" cy="10" r=".8" fill="currentColor" stroke="none"/><circle cx="15" cy="10" r=".8" fill="currentColor" stroke="none"/>',
  moreV: '<circle cx="10" cy="5" r=".8" fill="currentColor" stroke="none"/><circle cx="10" cy="10" r=".8" fill="currentColor" stroke="none"/><circle cx="10" cy="15" r=".8" fill="currentColor" stroke="none"/>',
  search: '<circle cx="9" cy="9" r="5"/><path d="M13 13l4 4"/>',
  filter: '<path d="M3.5 5h13M5.5 10h9M8 15h4"/>',
  fork: '<path d="M8 4v3l-3 3v6M12 4v3l3 3v6"/>',
  pin: '<path d="M10 2.5c-3 0-5 2-5 4.5 0 3.5 5 10 5 10s5-6.5 5-10c0-2.5-2-4.5-5-4.5z"/><circle cx="10" cy="7.5" r="1.8"/>',
  copy: '<rect x="6" y="6" width="10" height="10" rx="1.5"/><path d="M4 12V5a1 1 0 011-1h7"/>',
  phone: '<path d="M5 3h3l1.5 4-2 1.5c.8 2 2 3.2 4 4l1.5-2 4 1.5v3c0 .5-.5 1-1 1C8 16 4 12 4 4c0-.5.5-1 1-1z"/>',
  download: '<path d="M10 3v10M6 9l4 4 4-4"/><path d="M4 17h12"/>',
  external: '<path d="M8 4H4v12h12v-4"/><path d="M11 4h5v5M16 4l-7 7"/>',
  grid: '<rect x="3" y="3" width="6" height="6" rx="1"/><rect x="11" y="3" width="6" height="6" rx="1"/><rect x="3" y="11" width="6" height="6" rx="1"/><rect x="11" y="11" width="6" height="6" rx="1"/>',
  list: '<path d="M3 5h14M3 10h14M3 15h14"/>',
  speaker: '<path d="M4 8v4h3l4 3V5L7 8H4z"/><path d="M14 7c1.5 1.5 1.5 4.5 0 6"/>',
  flag: '<path d="M4 3v14"/><path d="M4 4h11l-2 3 2 3H4"/>',
  leaf: '<path d="M3 17c0-8 6-13 14-14-1 8-6 14-14 14z"/><path d="M3 17c4-4 7-7 11-11"/>',
  sun: '<circle cx="10" cy="10" r="3"/><path d="M10 3v2M10 15v2M3 10h2M15 10h2M5 5l1.5 1.5M13.5 13.5L15 15M5 15l1.5-1.5M13.5 6.5L15 5"/>',
  coffee: '<path d="M4 8h9v5a3 3 0 01-3 3H7a3 3 0 01-3-3V8z"/><path d="M13 10h2a2 2 0 010 4h-2"/><path d="M6 5v1M9 5v1"/>',
  utensils: '<path d="M6 3v14M4 3v4a2 2 0 002 2M8 3v4a2 2 0 01-2 2"/><path d="M14 3s-2 2-2 5 2 4 2 4v5"/>',
  eye: '<path d="M1.5 10S4 4 10 4s8.5 6 8.5 6-2.5 6-8.5 6-8.5-6-8.5-6z"/><circle cx="10" cy="10" r="2.5"/>',
  eyeOff: '<path d="M1.5 10S4 4 10 4c1.5 0 2.8.4 4 1M18.5 10S16 16 10 16c-1.5 0-2.8-.4-4-1"/><path d="M8 8a2.5 2.5 0 003.5 3.5M13 10.5a2.5 2.5 0 00-2-2"/><path d="M3 3l14 14"/>',
  lock: '<rect x="4" y="9" width="12" height="8" rx="1.5"/><path d="M6.5 9V6.5a3.5 3.5 0 017 0V9"/>',
  globe: '<circle cx="10" cy="10" r="7.5"/><path d="M2.5 10h15M10 2.5c2 2.5 3 5 3 7.5s-1 5-3 7.5c-2-2.5-3-5-3-7.5s1-5 3-7.5z"/>',
  whatsapp: '<path d="M16.5 3.5a9 9 0 00-14 11L2 18l3.7-.5a9 9 0 0010.8-14z"/><path d="M7 8c0 3 2 5 5 5l1.5-1.5-2-1-1 1c-.8-.4-1.6-1.2-2-2l1-1-1-2L7 8z" fill="currentColor" stroke="none"/>',
  maximize: '<path d="M4 8V4h4M16 8V4h-4M4 12v4h4M16 12v4h-4"/>',
  cam: '<rect x="3" y="5.5" width="14" height="10" rx="1.5"/><circle cx="10" cy="10.5" r="2.5"/><path d="M8 5.5l1-1.5h2l1 1.5"/>',
  print: '<path d="M6 4h8v4H6zM5 8h10v6h-3v3H8v-3H5z"/><circle cx="13" cy="11" r=".8" fill="currentColor" stroke="none"/>',
  alert: '<path d="M10 2.5L18 16H2L10 2.5z"/><path d="M10 8v3.5M10 13.5v.1"/>',
  signal: '<path d="M3 14v3M7 11v6M11 8v9M15 5v12"/>',
}

const current = computed(() => paths[props.name] ?? '<circle cx="10" cy="10" r="4"/>')
</script>

<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 20 20"
    fill="none"
    stroke="currentColor"
    :stroke-width="strokeWidth"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    :class="iconClass"
  >
    <g v-html="current" />
  </svg>
</template>
