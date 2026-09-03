<script setup>
import { ref, watch, onMounted, onBeforeUnmount } from 'vue'
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import { TextStyle, Color } from '@tiptap/extension-text-style'
import TaskList from '@tiptap/extension-task-list'
import TaskItem from '@tiptap/extension-task-item'
import { DrawingBlock } from '../tiptap/drawingBlock'
import ToolbarIcon from './ToolbarIcon.vue'

const props = defineProps({
  modelValue: { type: [String, Object], default: null },
  noteId: { type: String, default: null },
  mentionCandidates: { type: Array, default: () => [] },
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

// Oznaczanie: "@nazwaUzytkownika" jest zwyklym tekstem - backend rozpoznaje je
// regexem przy zapisie i wysyla powiadomienie. Ten przycisk to tylko wygoda
// (dropdown z czlonkami grupy), zeby nie trzeba bylo pamietac/przepisywac nazw.
const showMentionMenu = ref(false)

function toggleMentionMenu() {
  showMentionMenu.value = !showMentionMenu.value
}

function insertMention(username) {
  editor.value?.chain().focus().insertContent(`@${username} `).run()
  showMentionMenu.value = false
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

// position:sticky (below) computes its offset against the layout viewport, but on
// mobile, when the on-screen keyboard opens, browsers resize/pan the visual viewport
// instead - sticky doesn't track that, so the toolbar can end up scrolled out of the
// visible area while the keyboard is up (the meta viewport interactive-widget hint
// isn't honored on all mobile browsers). Detect that case via the VisualViewport API
// and switch the toolbar to position:fixed pinned to the actually-visible top edge.
const keyboardOpen = ref(false)
// How far the visual viewport's top edge has scrolled from the layout viewport's -
// non-zero while the browser's own address bar is still partly shown, so pinning
// the toolbar to a hardcoded top:0 leaves it partially hidden underneath that bar.
const keyboardOffsetTop = ref(0)

function updateKeyboardState() {
  const vv = window.visualViewport
  if (!vv || window.innerWidth > 640) {
    keyboardOpen.value = false
    keyboardOffsetTop.value = 0
    return
  }
  keyboardOpen.value = window.innerHeight - vv.height > 120
  keyboardOffsetTop.value = vv.offsetTop
}

onMounted(() => {
  const vv = window.visualViewport
  if (!vv) return
  vv.addEventListener('resize', updateKeyboardState)
  vv.addEventListener('scroll', updateKeyboardState)
  // Address bar collapsing on normal page scroll also shifts visualViewport.offsetTop,
  // but doesn't always fire the visualViewport's own events - window scroll catches that.
  window.addEventListener('scroll', updateKeyboardState, { passive: true })
})

onBeforeUnmount(() => {
  const vv = window.visualViewport
  window.removeEventListener('scroll', updateKeyboardState)
  if (!vv) return
  vv.removeEventListener('resize', updateKeyboardState)
  vv.removeEventListener('scroll', updateKeyboardState)
})
</script>

<template>
  <div class="note-editor">
    <div
      v-if="editor"
      class="toolbar"
      :class="{ 'toolbar-keyboard-fixed': keyboardOpen }"
      :style="keyboardOpen ? { top: `${keyboardOffsetTop}px` } : null"
    >
      <button type="button" title="Pogrubienie" :class="{ active: editor.isActive('bold') }" @click="editor.chain().focus().toggleBold().run()"><ToolbarIcon name="bold" /></button>
      <button type="button" title="Kursywa" :class="{ active: editor.isActive('italic') }" @click="editor.chain().focus().toggleItalic().run()"><ToolbarIcon name="italic" /></button>
      <button type="button" title="Przekreślenie" :class="{ active: editor.isActive('strike') }" @click="editor.chain().focus().toggleStrike().run()"><ToolbarIcon name="strike" /></button>
      <span class="divider" />
      <button type="button" title="Nagłówek" :class="{ active: editor.isActive('heading', { level: 2 }) }" @click="editor.chain().focus().toggleHeading({ level: 2 }).run()"><ToolbarIcon name="heading" /></button>
      <button type="button" title="Lista punktowana" :class="{ active: editor.isActive('bulletList') }" @click="editor.chain().focus().toggleBulletList().run()"><ToolbarIcon name="bulletList" /></button>
      <button type="button" title="Lista numerowana" :class="{ active: editor.isActive('orderedList') }" @click="editor.chain().focus().toggleOrderedList().run()"><ToolbarIcon name="orderedList" /></button>
      <button type="button" title="Checkbox" :class="{ active: editor.isActive('taskList') }" @click="editor.chain().focus().toggleTaskList().run()"><ToolbarIcon name="checkbox" /></button>
      <span class="divider" />
      <button type="button" title="Cytat" :class="{ active: editor.isActive('blockquote') }" @click="editor.chain().focus().toggleBlockquote().run()"><ToolbarIcon name="quote" /></button>
      <button type="button" title="Blok kodu" :class="{ active: editor.isActive('codeBlock') }" @click="editor.chain().focus().toggleCodeBlock().run()"><ToolbarIcon name="code" /></button>
      <button type="button" title="Wstaw rysunek" @click="insertDrawing"><ToolbarIcon name="drawing" /></button>
      <span v-if="mentionCandidates.length" class="mention-wrap">
        <button type="button" title="Oznacz osobę (@)" @click="toggleMentionMenu">@</button>
        <div v-if="showMentionMenu" class="mention-backdrop" @click="showMentionMenu = false" />
        <div v-if="showMentionMenu" class="mention-menu card">
          <button
            v-for="username in mentionCandidates"
            :key="username"
            type="button"
            class="mention-item"
            @click="insertMention(username)"
          >
            @{{ username }}
          </button>
        </div>
      </span>
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
    <div class="keyboard-spacer" aria-hidden="true" />
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
  position: sticky;
  /* Sits just below AppLayout's own sticky header (4rem tall). */
  top: 4rem;
  z-index: 10;
  background: var(--color-bg);
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--color-border);
}

/* Mobile keyboard open (detected via VisualViewport, see script) - sticky can't
   track the keyboard-shrunk visible area on all mobile browsers, so pin the
   toolbar with position:fixed to the real top of the screen instead. Overlaps
   AppLayout's header (that's fine - the header isn't useful while typing). */
.toolbar.toolbar-keyboard-fixed {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 200;
  padding: 0.5rem 1rem;
  border-radius: 0;
  box-shadow: var(--shadow);
}

.toolbar button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.85rem;
  height: 1.85rem;
  padding: 0;
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

.mention-wrap {
  position: relative;
  display: inline-flex;
}

.mention-backdrop {
  position: fixed;
  inset: 0;
  z-index: 150;
}

.mention-menu {
  position: absolute;
  top: calc(100% + 0.375rem);
  left: 0;
  z-index: 151;
  min-width: 160px;
  max-height: 220px;
  overflow-y: auto;
  padding: 0.375rem;
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.toolbar .mention-item {
  width: 100%;
  height: auto;
  justify-content: flex-start;
  text-align: left;
  padding: 0.375rem 0.5rem;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--color-text);
  font-size: 0.875rem;
}

.toolbar .mention-item:hover {
  background: var(--color-surface-alt);
  border-color: transparent;
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

.keyboard-spacer {
  /* Always-present extra scroll room, so there's room to scroll text clear
     of an on-screen keyboard even when it covers text near the bottom of
     the note - not conditional on any focus/viewport event, since those
     don't fire reliably on every browser (e.g. embedded kiosk browsers). */
  height: 50vh;
}

.editor-content :deep(.ProseMirror) {
  min-height: 200px;
  outline: none;
  line-height: 1.6;
  padding: 0.75rem 0.875rem;
  /* min 16px, inaczej iOS Safari samoczynnie przybliza strone przy focusie */
  font-size: max(1rem, 16px);
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
  display: inline-flex;
}

.editor-content :deep(.ProseMirror ul[data-type='taskList'] li > div) {
  flex: 1;
}

.editor-content :deep(.ProseMirror ul[data-type='taskList'] li > label input[type='checkbox']) {
  appearance: none;
  -webkit-appearance: none;
  width: 1.25rem;
  height: 1.25rem;
  margin: 0;
  border: 2px solid var(--color-border);
  border-radius: 6px;
  background: transparent;
  cursor: pointer;
  position: relative;
  transition: background 0.15s, border-color 0.15s;
}

.editor-content :deep(.ProseMirror ul[data-type='taskList'] li > label input[type='checkbox']:hover) {
  border-color: var(--color-secondary);
}

.editor-content :deep(.ProseMirror ul[data-type='taskList'] li > label input[type='checkbox']:checked) {
  background: var(--color-secondary);
  border-color: var(--color-secondary);
}

.editor-content :deep(.ProseMirror ul[data-type='taskList'] li > label input[type='checkbox']:checked::after) {
  content: '';
  position: absolute;
  left: 0.35rem;
  top: 0.1rem;
  width: 0.3rem;
  height: 0.6rem;
  border: solid #fff;
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
}

.editor-content :deep(.ProseMirror ul[data-type='taskList'] li[data-checked='true'] > div) {
  color: var(--color-text-muted);
  text-decoration: line-through;
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
