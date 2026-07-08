<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import api from '../api/client'

const props = defineProps({
  noteId: { type: String, required: true },
  audioId: { type: String, required: true },
  durationSeconds: { type: Number, default: 0 },
})

const emit = defineEmits(['delete'])

const audioUrl = ref(null)
const loading = ref(true)
const error = ref(false)

function formatTime(sec) {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return `${m}:${s.toString().padStart(2, '0')}`
}

onMounted(async () => {
  try {
    const { data } = await api.get(
      `/notes/${props.noteId}/audio/${props.audioId}/stream`,
      { responseType: 'blob' }
    )
    audioUrl.value = URL.createObjectURL(data)
  } catch {
    error.value = true
  } finally {
    loading.value = false
  }
})

onUnmounted(() => {
  if (audioUrl.value) URL.revokeObjectURL(audioUrl.value)
})
</script>

<template>
  <div class="audio-player card">
    <div class="player-info">
      <span class="player-icon">🎙</span>
      <span class="player-duration">{{ formatTime(durationSeconds) }}</span>
    </div>
    <div v-if="loading" class="player-loading">Ładowanie…</div>
    <audio v-else-if="audioUrl" :src="audioUrl" controls class="player-audio" />
    <span v-else-if="error" class="error-msg">Błąd odtwarzania</span>
    <button class="btn btn-ghost btn-sm delete-btn" title="Usuń" @click="emit('delete')">🗑</button>
  </div>
</template>

<style scoped>
.audio-player {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
}

.player-info {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  flex-shrink: 0;
}

.player-icon {
  font-size: 1.25rem;
}

.player-duration {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
  font-variant-numeric: tabular-nums;
}

.player-audio {
  flex: 1;
  min-width: 0;
  height: 36px;
}

.player-loading {
  flex: 1;
  font-size: 0.875rem;
  color: var(--color-text-muted);
}

.delete-btn {
  flex-shrink: 0;
  opacity: 0.6;
}

.delete-btn:hover {
  opacity: 1;
}
</style>
