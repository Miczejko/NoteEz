<script setup>
import { ref, onUnmounted } from 'vue'

const emit = defineEmits(['recorded'])

const isRecording = ref(false)
const duration = ref(0)
const error = ref('')

let mediaRecorder = null
let chunks = []
let timer = null
let startTime = 0

async function startRecording() {
  error.value = ''
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    const mimeType = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
      ? 'audio/webm;codecs=opus'
      : 'audio/webm'

    mediaRecorder = new MediaRecorder(stream, { mimeType })
    chunks = []

    mediaRecorder.ondataavailable = (e) => {
      if (e.data.size > 0) chunks.push(e.data)
    }

    mediaRecorder.onstop = () => {
      stream.getTracks().forEach((t) => t.stop())
      const blob = new Blob(chunks, { type: mimeType })
      const seconds = Math.round((Date.now() - startTime) / 1000)
      emit('recorded', { blob, durationSeconds: Math.max(1, seconds) })
      duration.value = 0
    }

    mediaRecorder.start(250)
    startTime = Date.now()
    isRecording.value = true
    timer = setInterval(() => {
      duration.value = Math.floor((Date.now() - startTime) / 1000)
    }, 500)
  } catch {
    error.value = 'Brak dostępu do mikrofonu'
  }
}

function stopRecording() {
  if (mediaRecorder && mediaRecorder.state !== 'inactive') {
    mediaRecorder.stop()
  }
  isRecording.value = false
  clearInterval(timer)
}

function formatTime(sec) {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return `${m}:${s.toString().padStart(2, '0')}`
}

onUnmounted(() => {
  stopRecording()
})
</script>

<template>
  <div class="audio-recorder">
    <div v-if="error" class="error-msg">{{ error }}</div>
    <div class="recorder-controls">
      <button
        v-if="!isRecording"
        class="btn btn-secondary rec-btn"
        @click="startRecording"
      >
        🎙 Nagraj głosówkę
      </button>
      <template v-else>
        <span class="rec-indicator">
          <span class="rec-dot" />
          {{ formatTime(duration) }}
        </span>
        <button class="btn btn-danger rec-btn" @click="stopRecording">
          ⏹ Zatrzymaj
        </button>
      </template>
    </div>
  </div>
</template>

<style scoped>
.audio-recorder {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.recorder-controls {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.rec-btn {
  min-width: 160px;
}

.rec-indicator {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  color: var(--color-danger);
}

.rec-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--color-danger);
  animation: pulse 1s infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.3; }
}
</style>
