<script setup>
import { watch, onBeforeUnmount } from 'vue'
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import { TextStyle, Color } from '@tiptap/extension-text-style'
import TaskList from '@tiptap/extension-task-list'
import TaskItem from '@tiptap/extension-task-item'
import { DrawingBlock } from '../tiptap/drawingBlock'

const props = defineProps({
  modelValue: { type: [String, Object], default: null },
  noteId: { type: String, default: null },
})

const emit = defineEmits(['update:modelValue'])

function parseContent(value) {
  if (!value) return ''
  if (typeof value === 'object') return value
  try {
    return JSON.parse(value)
  } catch {
    // Legacy plain-text note: seed the editor with it as a single paragraph.
    return { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: value }] }] }
  }
}

const editor = useEditor({
  content: parseContent(props.modelValue),
  extensions: [
    StarterKit,
    TextStyle,
    Color,
    TaskList,
    TaskItem.configure({ nested: true }),
    DrawingBlock.configure({ noteId: props.noteId }),
  ],
  onUpdate: ({ editor }) => {
    emit('update:modelValue', JSON.stringify(editor.getJSON()))
  },
})

function insertDrawing() {
  editor.value?.chain().focus().insertContent({ type: 'drawingBlock', attrs: { drawingId: null } }).run()
}

watch(
  () => props.modelValue,
  (value) => {
    if (!editor.value) return
    const incoming = JSON.stringify(parseContent(value))
    const current = JSON.stringify(editor.value.getJSON())
    if (incoming !== current) {
      editor.value.commands.setContent(parseContent(value), { emitUpdate: false })
    }
  }
)

onBeforeUnmount(() => {
  editor.value?.destroy()
})
</script>

<template>
  <div class="note-editor">
    <div v-if="editor" class="toolbar">
      <button type="button" :class="{ active: editor.isActive('bold') }" @click="editor.chain().focus().toggleBold().run()">B</button>
      <button type="button" :class="{ active: editor.isActive('italic') }" @click="editor.chain().focus().toggleItalic().run()"><i>I</i></button>
      <button type="button" :class="{ active: editor.isActive('strike') }" @click="editor.chain().focus().toggleStrike().run()"><s>S</s></button>
      <span class="divider" />
      <button type="button" :class="{ active: editor.isActive('heading', { level: 2 }) }" @click="editor.chain().focus().toggleHeading({ level: 2 }).run()">H2</button>
      <button type="button" :class="{ active: editor.isActive('bulletList') }" @click="editor.chain().focus().toggleBulletList().run()">• Lista</button>
      <button type="button" :class="{ active: editor.isActive('orderedList') }" @click="editor.chain().focus().toggleOrderedList().run()">1. Lista</button>
      <button type="button" :class="{ active: editor.isActive('taskList') }" @click="editor.chain().focus().toggleTaskList().run()">☑ Checkbox</button>
      <span class="divider" />
      <button type="button" :class="{ active: editor.isActive('blockquote') }" @click="editor.chain().focus().toggleBlockquote().run()">" "</button>
      <button type="button" :class="{ active: editor.isActive('codeBlock') }" @click="editor.chain().focus().toggleCodeBlock().run()">{ }</button>
      <button type="button" @click="insertDrawing">🖊 Rysunek</button>
      <span class="divider" />
      <label class="color-picker" :style="{ '--swatch': editor.getAttributes('textStyle').color || 'transparent' }">
        A
        <input
          type="color"
          :value="editor.getAttributes('textStyle').color || '#000000'"
          @input="editor.chain().focus().setColor($event.target.value).run()"
        />
      </label>
      <button
        v-if="editor.getAttributes('textStyle').color"
        type="button"
        class="color-reset"
        title="Usuń kolor"
        @click="editor.chain().focus().unsetColor().run()"
      >
        ×
      </button>
    </div>
    <EditorContent :editor="editor" class="editor-content input-field" />
  </div>
</template>

<style scoped>
.note-editor {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  align-items: center;
}

.toolbar button {
  min-width: 2rem;
  padding: 0.3rem 0.5rem;
  font-size: 0.8125rem;
  font-weight: 600;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  transition: background 0.15s, color 0.15s, border-color 0.15s;
}

.toolbar button:hover {
  border-color: var(--color-secondary);
}

.toolbar button.active {
  background: var(--color-secondary);
  color: #fff;
  border-color: var(--color-secondary);
}

.toolbar .divider {
  width: 1px;
  height: 1.25rem;
  background: var(--color-border);
  margin: 0 0.25rem;
}

.color-picker {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 1.85rem;
  font-size: 0.8125rem;
  font-weight: 700;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  cursor: pointer;
  box-shadow: inset 0 -3px 0 0 var(--swatch, transparent);
}

.color-picker input[type='color'] {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
}

.color-reset {
  width: 1.5rem;
  min-width: 1.5rem;
  padding: 0.3rem;
  font-size: 0.9375rem;
  line-height: 1;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}

.editor-content {
  min-height: 200px;
}

.editor-content :deep(.ProseMirror) {
  min-height: 200px;
  outline: none;
  line-height: 1.6;
  padding: 0.75rem 0.875rem;
}

.editor-content :deep(.ProseMirror p) {
  margin: 0 0 0.5em;
}

.editor-content :deep(.ProseMirror ul),
.editor-content :deep(.ProseMirror ol) {
  padding-left: 1.25rem;
}

.editor-content :deep(.ProseMirror ul[data-type='taskList']) {
  list-style: none;
  padding-left: 0.25rem;
}

.editor-content :deep(.ProseMirror ul[data-type='taskList'] li) {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
}

.editor-content :deep(.ProseMirror ul[data-type='taskList'] li > label) {
  margin-top: 0.25rem;
  user-select: none;
}

.editor-content :deep(.ProseMirror ul[data-type='taskList'] li > div) {
  flex: 1;
}

.editor-content :deep(.ProseMirror blockquote) {
  border-left: 3px solid var(--color-border);
  padding-left: 0.75rem;
  color: var(--color-text-muted);
}

.editor-content :deep(.ProseMirror pre) {
  background: rgba(0, 0, 0, 0.05);
  padding: 0.5rem 0.75rem;
  border-radius: 6px;
  overflow-x: auto;
}
</style>
