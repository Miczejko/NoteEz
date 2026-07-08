<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'

const props = defineProps({
  strokesJson: { type: String, default: null },
  readonly: { type: Boolean, default: false },
})

const emit = defineEmits(['save'])

const canvasRef = ref(null)
const strokes = ref([])
const isDrawing = ref(false)
const currentStroke = ref(null)
const isCanvasReady = ref(false)

const colors = ['#54428e', '#8963ba', '#afe3c0', '#90c290', '#2d2640']
const selectedColor = ref(colors[0])
const strokeWidth = ref(3)

let ctx = null
let lastPointTime = 0

function parseStrokes(json) {
  if (!json) return []
  try {
    // Handle case where json might already be an object (shouldn't happen, but be safe)
    if (typeof json === 'object') {
      return json.strokes || []
    }
    // Handle case where json might be double-encoded
    let data = JSON.parse(json)
    // If we get a string instead of an object, it was double-encoded
    if (typeof data === 'string') {
      data = JSON.parse(data)
    }
    return data.strokes || []
  } catch (e) {
    console.warn('Failed to parse strokes:', e, 'Input:', json)
    return []
  }
}

function getPos(e) {
  const canvas = canvasRef.value
  const rect = canvas.getBoundingClientRect()
  const scaleX = canvas.width / rect.width
  const scaleY = canvas.height / rect.height
  const clientX = e.touches ? e.touches[0].clientX : e.clientX
  const clientY = e.touches ? e.touches[0].clientY : e.clientY
  return {
    x: (clientX - rect.left) * scaleX,
    y: (clientY - rect.top) * scaleY,
  }
}

function drawStroke(stroke) {
  if (!ctx || stroke.points.length < 2) return
  ctx.beginPath()
  ctx.strokeStyle = stroke.color
  ctx.lineWidth = stroke.width
  ctx.lineCap = 'round'
  ctx.lineJoin = 'round'
  ctx.moveTo(stroke.points[0].x, stroke.points[0].y)
  for (let i = 1; i < stroke.points.length; i++) {
    ctx.lineTo(stroke.points[i].x, stroke.points[i].y)
  }
  ctx.stroke()
}

function redraw() {
  // Guard against premature redraw before canvas is ready
  if (!ctx || !canvasRef.value || !isCanvasReady.value) return
  ctx.clearRect(0, 0, canvasRef.value.width, canvasRef.value.height)
  ctx.fillStyle = '#ffffff'
  ctx.fillRect(0, 0, canvasRef.value.width, canvasRef.value.height)
  strokes.value.forEach(drawStroke)
  if (currentStroke.value) drawStroke(currentStroke.value)
}

function resizeCanvas() {
  const canvas = canvasRef.value
  if (!canvas) return
  const container = canvas.parentElement

  // Get optimal dimensions based on drawing content
  const { width, height, padding, bounds } = getOptimalCanvasDimensions()

  // Set container size
  container.style.width = `${width}px`
  container.style.minWidth = `${width}px`

  // Set canvas dimensions
  canvas.width = width
  canvas.height = height
  canvas.style.width = `${width}px`
  canvas.style.height = `${height}px`

  ctx = canvas.getContext('2d')
  isCanvasReady.value = true

  // Now that canvas is ready, redraw any loaded strokes
  redraw()
}

function startDraw(e) {
  if (props.readonly) return
  e.preventDefault()
  isDrawing.value = true
  const pos = getPos(e)
  currentStroke.value = {
    color: selectedColor.value,
    width: strokeWidth.value,
    points: [pos],
  }
  lastPointTime = Date.now()
}

function moveDraw(e) {
  if (!isDrawing.value || !currentStroke.value) return
  e.preventDefault()
  const now = Date.now()
  if (now - lastPointTime < 16) return
  lastPointTime = now
  const pos = getPos(e)
  currentStroke.value.points.push(pos)
  redraw()
}

function endDraw() {
  if (!isDrawing.value || !currentStroke.value) return
  isDrawing.value = false
  if (currentStroke.value.points.length >= 2) {
    strokes.value.push({ ...currentStroke.value })
  }
  currentStroke.value = null
  redraw()
}

function undo() {
  strokes.value.pop()
  redraw()
}

function clear() {
  strokes.value = []
  redraw()
}

function save() {
  const json = JSON.stringify({ strokes: strokes.value })
  emit('save', json)
}

function calculateDrawingBounds(strokesList) {
  if (!strokesList || strokesList.length === 0) {
    return { minX: 0, minY: 0, maxX: 400, maxY: 250 }
  }

  let minX = Infinity
  let minY = Infinity
  let maxX = -Infinity
  let maxY = -Infinity

  strokesList.forEach((stroke) => {
    stroke.points.forEach((point) => {
      minX = Math.min(minX, point.x)
      minY = Math.min(minY, point.y)
      maxX = Math.max(maxX, point.x)
      maxY = Math.max(maxY, point.y)
    })
  })

  // If no valid bounds found, use defaults
  if (!isFinite(minX)) {
    return { minX: 0, minY: 0, maxX: 400, maxY: 250 }
  }

  return { minX, minY, maxX, maxY }
}

function getOptimalCanvasDimensions() {
  const bounds = calculateDrawingBounds(strokes.value)
  const padding = 20 // Add padding around drawing

  const width = Math.max(100, bounds.maxX - bounds.minX + padding * 2)
  const height = Math.max(250, bounds.maxY - bounds.minY + padding * 2)

  return { width, height, padding, bounds }
}

watch(
  () => props.strokesJson,
  (val) => {
    strokes.value = parseStrokes(val)
    redraw()
  },
  { immediate: true }
)

onMounted(() => {
  resizeCanvas()
  window.addEventListener('resize', resizeCanvas)
})

onUnmounted(() => {
  window.removeEventListener('resize', resizeCanvas)
})
</script>

<template>
  <div class="drawing-canvas">
    <div v-if="!readonly" class="toolbar">
      <div class="color-picker">
        <button
          v-for="color in colors"
          :key="color"
          class="color-btn"
          :class="{ active: selectedColor === color }"
          :style="{ background: color }"
          :title="color"
          @click="selectedColor = color"
        />
      </div>
      <div class="width-picker">
        <label>Szerokość:</label>
        <input v-model.number="strokeWidth" type="range" min="1" max="12" />
        <span>{{ strokeWidth }}px</span>
      </div>
      <div class="toolbar-actions">
        <button class="btn btn-ghost btn-sm" @click="undo" :disabled="!strokes.length">Cofnij</button>
        <button class="btn btn-ghost btn-sm" @click="clear" :disabled="!strokes.length">Wyczyść</button>
        <button class="btn btn-accent btn-sm" @click="save">Zapisz rysunek</button>
      </div>
    </div>
    <div class="canvas-wrap">
      <canvas
        ref="canvasRef"
        class="canvas"
        :class="{ readonly }"
        @mousedown="startDraw"
        @mousemove="moveDraw"
        @mouseup="endDraw"
        @mouseleave="endDraw"
        @touchstart="startDraw"
        @touchmove="moveDraw"
        @touchend="endDraw"
      />
    </div>
  </div>
</template>

<style scoped>
.drawing-canvas {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: rgba(175, 227, 192, 0.15);
  border-radius: var(--radius-sm);
}

.color-picker {
  display: flex;
  gap: 0.375rem;
}

.color-btn {
  width: 1.75rem;
  height: 1.75rem;
  border-radius: 50%;
  border: 2px solid transparent;
  cursor: pointer;
  transition: transform 0.1s;
}

.color-btn.active {
  border-color: var(--color-text);
  transform: scale(1.15);
}

.width-picker {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.width-picker input {
  width: 80px;
  accent-color: var(--color-primary);
}

.toolbar-actions {
  display: flex;
  gap: 0.375rem;
  margin-left: auto;
  flex-wrap: wrap;
}

.canvas-wrap {
  border: 2px solid var(--color-border);
  border-radius: var(--radius-sm);
  overflow: hidden;
  background: white;
  touch-action: none;
}

.canvas {
  display: block;
  cursor: crosshair;
}

.canvas.readonly {
  cursor: default;
}

@media (max-width: 640px) {
  .toolbar {
    flex-direction: column;
    align-items: stretch;
  }

  .toolbar-actions {
    margin-left: 0;
    justify-content: stretch;
  }

  .toolbar-actions .btn {
    flex: 1;
  }
}
</style>
