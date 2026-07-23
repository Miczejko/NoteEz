<script setup>
import { computed, ref } from 'vue'
import { nodeViewProps, NodeViewWrapper } from '@tiptap/vue-3'
import DrawingCanvas from './DrawingCanvas.vue'
import { useNotesStore } from '../stores/notes'

const props = defineProps(nodeViewProps)
const notesStore = useNotesStore()

const noteId = computed(() => props.extension.options.noteId)
const drawing = computed(() =>
  notesStore.currentNote?.drawings?.find((d) => d.id === props.node.attrs.drawingId)
)

// A freshly inserted, not-yet-saved drawing starts in edit mode.
const editing = ref(!props.node.attrs.drawingId)
const saving = ref(false)

async function handleSave(strokesJson) {
  saving.value = true
  try {
    if (props.node.attrs.drawingId) {
      await notesStore.updateDrawing(noteId.value, props.node.attrs.drawingId, strokesJson)
    } else {
      const created = await notesStore.addDrawing(noteId.value, strokesJson)
      props.updateAttributes({ drawingId: created.id })
    }
    editing.value = false
  } finally {
    saving.value = false
  }
}

function handleCancel() {
  if (!props.node.attrs.drawingId) {
    props.deleteNode()
  } else {
    editing.value = false
  }
}

async function handleDelete() {
  if (!confirm('Usunąć ten rysunek?')) return
  if (props.node.attrs.drawingId) {
    await notesStore.deleteDrawing(noteId.value, props.node.attrs.drawingId)
  }
  props.deleteNode()
}

function handleEdit() {
  editing.value = true
}
</script>

<template>
  <NodeViewWrapper class="drawing-node" contenteditable="false">
    <div class="drawing-node-inner" :class="{ saving }">
      <DrawingCanvas
        v-if="editing"
        :strokes-json="drawing?.strokesJson"
        @save="handleSave"
      />
      <div v-else class="drawing-readonly">
        <DrawingCanvas :strokes-json="drawing?.strokesJson" readonly />
        <div class="drawing-node-actions">
          <button type="button" class="btn btn-secondary btn-sm" @click="handleEdit">✎ Edytuj</button>
          <button type="button" class="btn btn-ghost btn-sm delete-drawing" @click="handleDelete">Usuń</button>
        </div>
      </div>
      <button
        v-if="editing"
        type="button"
        class="btn btn-ghost btn-sm cancel-drawing"
        @click="handleCancel"
      >
        Anuluj
      </button>
    </div>
  </NodeViewWrapper>
</template>

<style scoped>
.drawing-node {
  margin: 0.75rem 0;
}

.drawing-node-inner {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-width: fit-content;
  padding: 0.75rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: rgba(175, 227, 192, 0.08);
}

.drawing-node-inner.saving {
  opacity: 0.6;
  pointer-events: none;
}

.drawing-readonly {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.drawing-node-actions {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
}

.delete-drawing {
  color: var(--color-danger);
}

.cancel-drawing {
  align-self: flex-end;
}
</style>
