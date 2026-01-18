<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'

defineProps<{ msg: string }>()

const { t } = useI18n()
const count = ref(0)
</script>

<template>
  <h1>{{ msg }}</h1>

  <div class="card">
    <button type="button" @click="count++">{{ t('helloWorld.count', { count }) }}</button>
    <p>
      {{ t('helloWorld.editPrefix') }}
      <code>components/HelloWorld.vue</code>
      {{ t('helloWorld.editSuffix') }}
    </p>
  </div>

  <p>
    {{ t('helloWorld.starterPrefix') }}
    <a href="https://vuejs.org/guide/quick-start.html#local" target="_blank">create-vue</a>,
    {{ t('helloWorld.starterSuffix') }}
  </p>
  <p>
    {{ t('helloWorld.idePrefix') }}
    <a href="https://vuejs.org/guide/scaling-up/tooling.html#ide-support" target="_blank">
      {{ t('helloWorld.ideLink') }}
    </a>.
  </p>
  <p class="read-the-docs">{{ t('helloWorld.docsHint') }}</p>
</template>

<style scoped>
.read-the-docs {
  color: #888;
}
</style>
