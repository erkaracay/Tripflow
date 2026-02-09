<script setup lang="ts">
import { watch } from 'vue'
import { ref } from 'vue'
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import Placeholder from '@tiptap/extension-placeholder'
import { TextStyle } from '@tiptap/extension-text-style'
import { Color } from '@tiptap/extension-color'
import Highlight from '@tiptap/extension-highlight'

const model = defineModel<string>({ default: '' })

const props = withDefaults(
  defineProps<{
    disabled?: boolean
    placeholder?: string
    minHeight?: string
  }>(),
  {
    disabled: false,
    placeholder: '',
    minHeight: '6rem',
  }
)

function toEditorContent(value: string | undefined | null): string {
  if (!value || !value.trim()) return '<p></p>'
  const trimmed = value.trim()
  if (trimmed.includes('<') && trimmed.includes('>')) return trimmed
  return `<p>${trimmed.replace(/\n/g, '</p><p>')}</p>`
}

const editor = useEditor({
  content: toEditorContent(model.value),
  extensions: [
    StarterKit.configure({
      heading: { levels: [1, 2, 3] },
    }),
    Placeholder.configure({ placeholder: props.placeholder }),
    TextStyle,
    Color,
    Highlight.configure({ multicolor: true }),
  ],
  editable: !props.disabled,
  editorProps: {
    attributes: {
      class: 'prose prose-sm max-w-none focus:outline-none min-h-0 px-3 py-2',
    },
  },
  onUpdate: ({ editor }) => {
    model.value = editor.getHTML()
  },
})

watch(
  () => model.value,
  (newVal) => {
    const ed = editor.value
    if (!ed) return
    const html = ed.getHTML()
    const normalized = toEditorContent(newVal)
    if (html === normalized) return
    ed.commands.setContent(normalized, { emitUpdate: false })
  }
)

watch(
  () => props.disabled,
  (disabled) => {
    editor.value?.setEditable(!disabled)
  }
)

const TEXT_COLORS = [
  { label: 'Metin rengi', value: null },
  { label: 'Siyah', value: '#1e293b' },
  { label: 'Kırmızı', value: '#dc2626' },
  { label: 'Mavi', value: '#2563eb' },
  { label: 'Yeşil', value: '#16a34a' },
  { label: 'Turuncu', value: '#ea580c' },
  { label: 'Mor', value: '#9333ea' },
]

const HIGHLIGHT_COLORS = [
  { label: 'Vurgu yok', value: null },
  { label: 'Sarı', value: '#fef08a' },
  { label: 'Yeşil', value: '#86efac' },
  { label: 'Mavi', value: '#93c5fd' },
  { label: 'Pembe', value: '#f9a8d4' },
]

const showTextColor = ref(false)
const showHighlight = ref(false)
</script>

<template>
  <div
    class="relative overflow-hidden rounded border border-slate-200 bg-white"
    :class="{ 'opacity-60 pointer-events-none': disabled }"
    :style="{ '--min-h': minHeight }"
  >
    <div
      v-if="showTextColor || showHighlight"
      class="fixed inset-0 z-[5]"
      aria-hidden="true"
      @click="showTextColor = false; showHighlight = false"
    />
    <div
      v-if="editor"
      class="flex flex-wrap gap-1 border-b border-slate-200 bg-slate-50 p-2"
    >
      <button
        type="button"
        class="rounded px-2 py-1 text-sm font-semibold hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('bold') }"
        :disabled="disabled"
        title="Bold"
        @click="editor.chain().focus().toggleBold().run()"
      >
        B
      </button>
      <button
        type="button"
        class="rounded px-2 py-1 text-sm italic hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('italic') }"
        :disabled="disabled"
        title="Italic"
        @click="editor.chain().focus().toggleItalic().run()"
      >
        I
      </button>
      <button
        type="button"
        class="rounded px-2 py-1 font-mono text-sm hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('code') }"
        :disabled="disabled"
        title="Code"
        @click="editor.chain().focus().toggleCode().run()"
      >
        &lt;/&gt;
      </button>
      <span class="mx-1 w-px self-stretch bg-slate-200" aria-hidden="true" />
      <button
        type="button"
        class="rounded px-2 py-1 text-xs hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('paragraph') }"
        :disabled="disabled"
        title="Paragraph"
        @click="editor.chain().focus().setParagraph().run()"
      >
        ¶
      </button>
      <button
        type="button"
        class="rounded px-2 py-1 text-sm font-bold hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('heading', { level: 1 }) }"
        :disabled="disabled"
        title="Heading 1"
        @click="editor.chain().focus().toggleHeading({ level: 1 }).run()"
      >
        H1
      </button>
      <button
        type="button"
        class="rounded px-2 py-1 text-sm font-semibold hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('heading', { level: 2 }) }"
        :disabled="disabled"
        title="Heading 2"
        @click="editor.chain().focus().toggleHeading({ level: 2 }).run()"
      >
        H2
      </button>
      <button
        type="button"
        class="rounded px-2 py-1 text-sm font-medium hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('heading', { level: 3 }) }"
        :disabled="disabled"
        title="Heading 3"
        @click="editor.chain().focus().toggleHeading({ level: 3 }).run()"
      >
        H3
      </button>
      <span class="mx-1 w-px self-stretch bg-slate-200" aria-hidden="true" />
      <button
        type="button"
        class="rounded px-2 py-1 text-sm hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('bulletList') }"
        :disabled="disabled"
        title="Bullet list"
        @click="editor.chain().focus().toggleBulletList().run()"
      >
        • List
      </button>
      <button
        type="button"
        class="rounded px-2 py-1 text-sm hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('orderedList') }"
        :disabled="disabled"
        title="Numbered list"
        @click="editor.chain().focus().toggleOrderedList().run()"
      >
        1. List
      </button>
      <button
        type="button"
        class="rounded px-2 py-1 font-mono text-xs hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('codeBlock') }"
        :disabled="disabled"
        title="Code block"
        @click="editor.chain().focus().toggleCodeBlock().run()"
      >
        &lt;&gt;
      </button>
      <button
        type="button"
        class="rounded px-2 py-1 text-sm hover:bg-slate-200"
        :class="{ 'bg-slate-200': editor.isActive('blockquote') }"
        :disabled="disabled"
        title="Blockquote"
        @click="editor.chain().focus().toggleBlockquote().run()"
      >
        "
      </button>
      <span class="mx-1 w-px self-stretch bg-slate-200" aria-hidden="true" />
      <div class="relative">
        <button
          type="button"
          class="flex items-center gap-1 rounded px-2 py-1 text-sm hover:bg-slate-200"
          :class="{ 'bg-slate-200': showTextColor }"
          :disabled="disabled"
          title="Metin rengi"
          @click="showTextColor = !showTextColor; showHighlight = false"
        >
          <span class="inline-block h-4 w-4 rounded border border-slate-300" style="background: linear-gradient(to bottom right, #dc2626, #2563eb)" aria-hidden="true" />
        </button>
        <div
          v-if="showTextColor"
          class="absolute left-0 top-full z-10 mt-1 flex flex-wrap gap-1 rounded border border-slate-200 bg-white p-2 shadow-lg"
        >
          <button
            v-for="c in TEXT_COLORS"
            :key="c.value ?? 'none'"
            type="button"
            class="h-6 w-6 rounded border border-slate-200 hover:ring-2 hover:ring-slate-400"
            :style="c.value ? { backgroundColor: c.value } : {}"
            :title="c.label"
            @click="c.value ? editor.chain().focus().setColor(c.value).run() : editor.chain().focus().unsetColor().run(); showTextColor = false"
          >
            <span v-if="!c.value" class="text-[10px] text-slate-400">×</span>
          </button>
        </div>
      </div>
      <div class="relative">
        <button
          type="button"
          class="flex items-center gap-1 rounded px-2 py-1 text-sm hover:bg-slate-200"
          :class="{ 'bg-slate-200': showHighlight }"
          :disabled="disabled"
          title="Vurgulama"
          @click="showHighlight = !showHighlight; showTextColor = false"
        >
          <span class="inline-block h-4 w-4 rounded bg-yellow-200" aria-hidden="true" />
        </button>
        <div
          v-if="showHighlight"
          class="absolute left-0 top-full z-10 mt-1 flex flex-wrap gap-1 rounded border border-slate-200 bg-white p-2 shadow-lg"
        >
          <button
            v-for="c in HIGHLIGHT_COLORS"
            :key="c.value ?? 'none'"
            type="button"
            class="h-6 w-6 rounded border border-slate-200 hover:ring-2 hover:ring-slate-400"
            :style="c.value ? { backgroundColor: c.value } : {}"
            :title="c.label"
            @click="c.value ? editor.chain().focus().toggleHighlight({ color: c.value }).run() : editor.chain().focus().unsetHighlight().run(); showHighlight = false"
          >
            <span v-if="!c.value" class="text-[10px] text-slate-400">×</span>
          </button>
        </div>
      </div>
    </div>
    <template v-if="editor">
      <EditorContent :editor="editor" />
    </template>
    <div v-else class="min-h-[var(--min-h,6rem)] animate-pulse rounded bg-slate-100 px-3 py-2" aria-hidden="true" />
  </div>
</template>

<style scoped>
:deep(.tiptap) {
  outline: none;
  min-height: var(--min-h, 6rem);
}

:deep(.tiptap p.is-editor-empty:first-child::before) {
  color: #94a3b8;
  content: attr(data-placeholder);
  float: left;
  height: 0;
  pointer-events: none;
}

:deep(.tiptap h1) {
  font-size: 1.5rem;
  font-weight: 700;
  margin: 0.5em 0;
}

:deep(.tiptap h2) {
  font-size: 1.25rem;
  font-weight: 600;
  margin: 0.5em 0;
}

:deep(.tiptap h3) {
  font-size: 1.125rem;
  font-weight: 600;
  margin: 0.5em 0;
}

:deep(.tiptap p) {
  margin: 0.25em 0;
}

:deep(.tiptap ul) {
  list-style-type: disc;
  padding-left: 1.5rem;
  margin: 0.25em 0;
}

:deep(.tiptap ol) {
  list-style-type: decimal;
  padding-left: 1.5rem;
  margin: 0.25em 0;
}

:deep(.tiptap li) {
  margin: 0.125em 0;
}

:deep(.tiptap blockquote) {
  border-left: 3px solid #cbd5e1;
  padding-left: 0.75rem;
  margin: 0.5em 0;
  color: #64748b;
}

:deep(.tiptap pre) {
  background: #1e293b;
  color: #e2e8f0;
  padding: 0.75rem;
  border-radius: 0.25rem;
  font-size: 0.875rem;
  overflow-x: auto;
}

:deep(.tiptap code) {
  background: #f1f5f9;
  padding: 0.125rem 0.25rem;
  border-radius: 0.125rem;
  font-size: 0.875em;
}

:deep(.tiptap pre code) {
  background: transparent;
  padding: 0;
}
</style>
