<script setup lang="ts">
import InforaIcon, { type InforaIconName } from '../components/icons/InforaIcon.vue'
import AppChip from '../components/ui/AppChip.vue'
import AppEyebrow from '../components/ui/AppEyebrow.vue'
import AppHairline from '../components/ui/AppHairline.vue'

const surfaces = [
  { key: 'paper', label: 'paper' },
  { key: 'paper-soft', label: 'paper-soft' },
  { key: 'paper-warm', label: 'paper-warm' },
  { key: 'card', label: 'card' },
  { key: 'line', label: 'line' },
  { key: 'line-strong', label: 'line-strong' },
]

const textTones = [
  { key: 'ink', label: 'ink' },
  { key: 'ink-soft', label: 'ink-soft' },
  { key: 'mute', label: 'mute' },
  { key: 'mute-soft', label: 'mute-soft' },
]

const accents = [
  { key: 'ember', label: 'ember' },
  { key: 'ember-deep', label: 'ember-deep' },
  { key: 'ember-soft', label: 'ember-soft' },
  { key: 'ember-line', label: 'ember-line' },
]

const statuses = [
  { key: 'sage', label: 'sage' },
  { key: 'sage-soft', label: 'sage-soft' },
  { key: 'coral', label: 'coral' },
  { key: 'coral-soft', label: 'coral-soft' },
  { key: 'sky', label: 'sky' },
  { key: 'sky-soft', label: 'sky-soft' },
]

const iconNames: InforaIconName[] = [
  'calendar', 'doc', 'qr', 'info', 'map', 'clock', 'bell', 'user',
  'bed', 'plane', 'shield', 'bus', 'plus', 'arrow', 'arrowDown',
  'check', 'x', 'chevronR', 'chevronD', 'chevronU', 'more', 'moreV',
  'search', 'filter', 'fork', 'pin', 'copy', 'phone', 'download',
  'external', 'grid', 'list', 'speaker', 'flag', 'leaf', 'sun',
  'coffee', 'utensils', 'eye', 'eyeOff', 'lock', 'globe', 'whatsapp',
  'maximize', 'cam', 'print', 'alert', 'signal',
]

const chipTones = ['neutral', 'ember', 'sage', 'sky', 'coral', 'dark'] as const
const radii = [
  { key: 'r1', size: '4' },
  { key: 'r2', size: '8' },
  { key: 'r3', size: '12' },
  { key: 'r4', size: '16' },
  { key: 'r5', size: '24' },
]
</script>

<template>
  <div class="min-h-screen bg-paper text-ink">
    <div class="mx-auto max-w-5xl px-6 py-10">
      <header class="mb-10">
        <AppEyebrow tone="ember">Infora · Design System · Faz 0</AppEyebrow>
        <h1 class="mt-2 text-3xl font-semibold tracking-tight">Foundation</h1>
        <p class="mt-2 max-w-2xl text-sm text-mute">
          Tokens, typography, icons and atomic primitives. Dev-only route — production'a
          dahil edilmez. Mevcut slate-tabanlı ekranlar bu değişiklikten etkilenmez.
        </p>
      </header>

      <!-- Surfaces -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">Surfaces</AppEyebrow>
        <div class="grid grid-cols-3 gap-3 sm:grid-cols-6">
          <div v-for="s in surfaces" :key="s.key">
            <div
              class="h-20 rounded-r2 border border-line"
              :style="{ background: `var(--color-${s.key})` }"
            />
            <div class="mt-2 font-mono text-[11px] text-mute-soft">{{ s.label }}</div>
          </div>
        </div>
      </section>

      <!-- Text tones -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">Text tones</AppEyebrow>
        <div class="space-y-2 rounded-r3 border border-line bg-card p-5">
          <div v-for="t in textTones" :key="t.key" class="flex items-baseline justify-between">
            <span :style="{ color: `var(--color-${t.key})` }" class="text-lg">
              Seyahatiniz başlıyor — ŞĞİÇÖÜ
            </span>
            <span class="font-mono text-[11px] text-mute-soft">{{ t.label }}</span>
          </div>
        </div>
      </section>

      <!-- Accent -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">Accent · Ember</AppEyebrow>
        <div class="grid grid-cols-4 gap-3">
          <div v-for="a in accents" :key="a.key">
            <div
              class="h-16 rounded-r2 border border-line"
              :style="{ background: `var(--color-${a.key})` }"
            />
            <div class="mt-2 font-mono text-[11px] text-mute-soft">{{ a.label }}</div>
          </div>
        </div>
      </section>

      <!-- Status -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">Status</AppEyebrow>
        <div class="grid grid-cols-3 gap-3 sm:grid-cols-6">
          <div v-for="s in statuses" :key="s.key">
            <div
              class="h-16 rounded-r2 border border-line"
              :style="{ background: `var(--color-${s.key})` }"
            />
            <div class="mt-2 font-mono text-[11px] text-mute-soft">{{ s.label }}</div>
          </div>
        </div>
      </section>

      <!-- Typography -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">Typography · Inter + JetBrains Mono</AppEyebrow>
        <div class="space-y-4 rounded-r3 border border-line bg-card p-6">
          <div class="flex items-baseline justify-between gap-6 border-b border-line pb-4">
            <div class="text-4xl font-semibold tracking-tight">Keşfet, ağırla</div>
            <div class="font-mono text-[10.5px] uppercase tracking-[0.11em] text-mute-soft">Display · 36/600</div>
          </div>
          <div class="flex items-baseline justify-between gap-6 border-b border-line pb-4">
            <div class="text-[22px] font-semibold tracking-tight">Etkinlik başlığı</div>
            <div class="font-mono text-[10.5px] uppercase tracking-[0.11em] text-mute-soft">H1 · 22/600</div>
          </div>
          <div class="flex items-baseline justify-between gap-6 border-b border-line pb-4">
            <div class="text-lg font-semibold">Bölüm başlığı</div>
            <div class="font-mono text-[10.5px] uppercase tracking-[0.11em] text-mute-soft">H2 · 18/600</div>
          </div>
          <div class="flex items-baseline justify-between gap-6 border-b border-line pb-4">
            <div class="text-[15px]">Body — program, belgeler, sigorta, transfer. Uzun açıklamalar burada.</div>
            <div class="font-mono text-[10.5px] uppercase tracking-[0.11em] text-mute-soft">Body · 15/400</div>
          </div>
          <div class="flex items-baseline justify-between gap-6 border-b border-line pb-4">
            <div class="text-[13px] text-mute">Body Small — detay satırları ve açıklamalar.</div>
            <div class="font-mono text-[10.5px] uppercase tracking-[0.11em] text-mute-soft">Body Small · 13/400</div>
          </div>
          <div class="flex items-baseline justify-between gap-6 border-b border-line pb-4">
            <div class="font-mono text-[13px] font-medium tabular-nums">08:30 · ROOM 219</div>
            <div class="font-mono text-[10.5px] uppercase tracking-[0.11em] text-mute-soft">Mono · 13/500</div>
          </div>
          <div class="flex items-baseline justify-between gap-6">
            <div class="font-mono text-[10.5px] font-medium uppercase tracking-[0.11em] text-mute">
              Section label
            </div>
            <div class="font-mono text-[10.5px] uppercase tracking-[0.11em] text-mute-soft">Eyebrow · 10.5/500</div>
          </div>
        </div>
      </section>

      <!-- Radii -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">Radii</AppEyebrow>
        <div class="grid grid-cols-5 gap-3">
          <div v-for="r in radii" :key="r.key" class="flex flex-col items-center gap-2">
            <div
              class="h-16 w-16 border border-line bg-paper-warm"
              :style="{ borderRadius: `var(--radius-${r.key})` }"
            />
            <div class="font-mono text-[11px] text-mute-soft">{{ r.key }} · {{ r.size }}px</div>
          </div>
        </div>
      </section>

      <!-- Icons -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">Icons · {{ iconNames.length }} adet</AppEyebrow>
        <div class="grid grid-cols-4 gap-3 sm:grid-cols-6 md:grid-cols-8">
          <div
            v-for="n in iconNames"
            :key="n"
            class="flex flex-col items-center gap-2 rounded-r2 border border-line bg-card p-3"
          >
            <InforaIcon :name="n" :size="22" class="text-ink" />
            <div class="font-mono text-[10px] text-mute-soft">{{ n }}</div>
          </div>
        </div>
      </section>

      <!-- Chip -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">AppChip</AppEyebrow>
        <div class="flex flex-wrap gap-2 rounded-r3 border border-line bg-card p-6">
          <AppChip v-for="t in chipTones" :key="t" :tone="t">{{ t }}</AppChip>
        </div>
      </section>

      <!-- Eyebrow -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">AppEyebrow</AppEyebrow>
        <div class="space-y-3 rounded-r3 border border-line bg-card p-6">
          <AppEyebrow tone="mute">Mute · varsayılan</AppEyebrow>
          <AppEyebrow tone="mute-soft">Mute soft</AppEyebrow>
          <AppEyebrow tone="ember">Ember accent</AppEyebrow>
          <AppEyebrow tone="mute" size="md">Md size</AppEyebrow>
        </div>
      </section>

      <!-- Hairline -->
      <section class="mb-12">
        <AppEyebrow class="mb-3">AppHairline</AppEyebrow>
        <div class="rounded-r3 border border-line bg-card p-6">
          <div class="mb-3 text-[13px] text-ink-soft">Yatay ayraç</div>
          <AppHairline />
          <div class="mt-4 flex items-center gap-3">
            <span class="text-[13px] text-ink-soft">Dikey</span>
            <AppHairline vertical class="h-6" />
            <span class="text-[13px] text-ink-soft">ayraç</span>
          </div>
        </div>
      </section>

      <footer class="mt-16 border-t border-line pt-6 font-mono text-[11px] uppercase tracking-[0.11em] text-mute-soft">
        Faz 0 · Foundation · Sonraki: Faz 1 (Program ekranı)
      </footer>
    </div>
  </div>
</template>
