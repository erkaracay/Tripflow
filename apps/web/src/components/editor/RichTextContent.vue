<script setup lang="ts">
import { computed } from 'vue'
import DOMPurify from 'dompurify'

const props = defineProps<{
  content?: string | null
}>()

const ALLOWED_TAGS = [
  'p',
  'br',
  'strong',
  'em',
  'code',
  'h1',
  'h2',
  'h3',
  'ul',
  'ol',
  'li',
  'blockquote',
  'pre',
  'a',
  'span',
  'mark',
]

const ALLOWED_ATTR = ['href', 'target', 'rel', 'style']

const isHtml = computed(() => {
  const c = props.content?.trim()
  return !!(c && c.includes('<') && c.includes('>'))
})

const sanitizedHtml = computed(() => {
  if (!props.content || !isHtml.value) return ''
  return DOMPurify.sanitize(props.content, {
    ALLOWED_TAGS,
    ALLOWED_ATTR,
  })
})

const plainText = computed(() => props.content ?? '')
</script>

<template>
  <div v-if="!content || !content.trim()" />
  <div
    v-else-if="isHtml"
    class="rich-text-content"
    v-html="sanitizedHtml"
  />
  <div v-else class="whitespace-pre-line">{{ plainText }}</div>
</template>

<style scoped>
.rich-text-content :deep(h1) {
  font-size: 1.5rem;
  font-weight: 700;
  margin: 0.5em 0;
}

.rich-text-content :deep(h2) {
  font-size: 1.25rem;
  font-weight: 600;
  margin: 0.5em 0;
}

.rich-text-content :deep(h3) {
  font-size: 1.125rem;
  font-weight: 600;
  margin: 0.5em 0;
}

.rich-text-content :deep(p) {
  margin: 0.25em 0;
}

/* Boş satırları görünür yap: <p></p> veya <p><br></p> */
.rich-text-content :deep(p:empty),
.rich-text-content :deep(p:has(> br:only-child)) {
  min-height: 1em;
}

.rich-text-content :deep(ul) {
  list-style-type: disc;
  padding-left: 1.5rem;
  margin: 0.25em 0;
}

.rich-text-content :deep(ol) {
  list-style-type: decimal;
  padding-left: 1.5rem;
  margin: 0.25em 0;
}

.rich-text-content :deep(li) {
  margin: 0.125em 0;
}

.rich-text-content :deep(blockquote) {
  border-left: 3px solid #cbd5e1;
  padding-left: 0.75rem;
  margin: 0.5em 0;
  color: #64748b;
}

.rich-text-content :deep(pre) {
  background: #1e293b;
  color: #e2e8f0;
  padding: 0.75rem;
  border-radius: 0.25rem;
  font-size: 0.875rem;
  overflow-x: auto;
}

.rich-text-content :deep(code) {
  background: #f1f5f9;
  padding: 0.125rem 0.25rem;
  border-radius: 0.125rem;
  font-size: 0.875em;
}

.rich-text-content :deep(pre code) {
  background: transparent;
  padding: 0;
}

.rich-text-content :deep(mark) {
  padding: 0 0.125rem;
}
</style>