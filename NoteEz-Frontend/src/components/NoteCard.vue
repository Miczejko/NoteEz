<script setup>
import { computed } from 'vue'
import { tiptapToPlainText } from '../utils/tiptapText'

const props = defineProps({
  note: { type: Object, required: true },
})

const previewText = computed(() => tiptapToPlainText(props.note.textContent))

function formatDate(dateStr) {
  return new Date(dateStr).toLocaleDateString('pl-PL', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}
</script>

<template>
  <router-link :to="`/notes/${note.id}`" class="note-card card">
    <div class="note-card-header">
      <h3 class="note-title">{{ note.title || 'Bez tytułu' }}</h3>
      <time class="note-date">{{ formatDate(note.updatedAt) }}</time>
    </div>
    <p v-if="previewText" class="note-preview">{{ previewText }}</p>
    <div class="note-badges">
      <span v-if="note.drawings?.length" class="badge badge-drawing">🖊 Rysunek</span>
      <span v-if="note.audioClips?.length" class="badge badge-audio">🎙 Głosówka</span>
    </div>
  </router-link>
</template>

<style scoped>
.note-card {
  display: block;
  padding: 1.125rem 1.25rem;
  text-decoration: none;
  color: inherit;
  transition: transform 0.15s, box-shadow 0.15s;
}

.note-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-lg);
  text-decoration: none;
}

.note-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.note-title {
  font-size: 1.0625rem;
  font-weight: 600;
  color: var(--color-primary);
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.note-date {
  font-size: 0.75rem;
  color: var(--color-text-muted);
  white-space: nowrap;
  flex-shrink: 0;
}

.note-preview {
  font-size: 0.875rem;
  color: var(--color-text-muted);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 0.625rem;
}

.note-badges {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.badge {
  font-size: 0.75rem;
  padding: 0.2rem 0.625rem;
  border-radius: 999px;
  font-weight: 600;
}

.badge-drawing {
  background: rgba(175, 227, 192, 0.5);
  color: #3a7a5a;
}

.badge-audio {
  background: rgba(144, 194, 144, 0.4);
  color: #3a6a3a;
}
</style>
