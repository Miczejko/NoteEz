<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { requestKioskFullscreen, toggleFullscreen } from '../utils/device'
import ToolbarIcon from './ToolbarIcon.vue'

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

const colors = [
  '#2d2640', '#54428e', '#8963ba', '#afe3c0', '#90c290',
  '#d64545', '#e08a2c', '#e0c93c', '#3468c0', '#3aa0a0',
]
const selectedColor = ref(colors[0])
const strokeWidth = ref(3)
const tool = ref('draw') // 'draw' | 'erase'
const eraserSize = ref(24)

// User-chosen canvas size (persisted alongside the strokes). Null means
// "auto-size to fit the drawn content", the previous fixed behavior.
const manualSize = ref(null)
const resizing = ref(false)

let ctx = null
let lastPointTime = 0
// Shift applied when rendering so the drawing's bounding box starts at
// (padding, padding) instead of being clipped by the trimmed canvas size.
let offsetX = 0
let offsetY = 0

function parseDrawingData(json) {
  if (!json) return { strokes: [], size: null }
  try {
    // Handle case where json might already be an object (shouldn't happen, but be safe)
    let data = typeof json === 'object' ? json : JSON.parse(json)
    // If we get a string instead of an object, it was double-encoded
    if (typeof data === 'string') {
      data = JSON.parse(data)
    }
    const size =
      Number.isFinite(data.canvasWidth) && Number.isFinite(data.canvasHeight)
        ? { width: data.canvasWidth, height: data.canvasHeight }
        : null
    return { strokes: data.strokes || [], size }
  } catch (e) {
    console.warn('Failed to parse strokes:', e, 'Input:', json)
    return { strokes: [], size: null }
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
    x: (clientX - rect.left) * scaleX - offsetX,
    y: (clientY - rect.top) * scaleY - offsetY,
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
  ctx.save()
  ctx.translate(offsetX, offsetY)
  strokes.value.forEach(drawStroke)
  if (currentStroke.value) drawStroke(currentStroke.value)
  ctx.restore()
}

function resizeCanvas() {
  const canvas = canvasRef.value
  if (!canvas) return
  const container = canvas.parentElement

  // Dimensions based on drawing content and any manual resize - this is the
  // drawing resolution (and the coordinate space strokes are stored in).
  const { width, height, padding, bounds } = getOptimalCanvasDimensions()

  canvas.width = width
  canvas.height = height
  offsetX = padding - bounds.minX
  offsetY = padding - bounds.minY

  // Cap the display size at the natural resolution, but let it shrink to
  // fit small screens instead of forcing the drawing off-screen.
  container.style.width = `${width}px`
  container.style.maxWidth = '100%'
  canvas.style.width = '100%'
  canvas.style.height = 'auto'
  canvas.style.aspectRatio = `${width} / ${height}`

  ctx = canvas.getContext('2d')
  isCanvasReady.value = true

  // Now that canvas is ready, redraw any loaded strokes
  redraw()
}

function distToSegment(p, a, b) {
  const dx = b.x - a.x
  const dy = b.y - a.y
  const lengthSq = dx * dx + dy * dy
  if (lengthSq === 0) return Math.hypot(p.x - a.x, p.y - a.y)
  let t = ((p.x - a.x) * dx + (p.y - a.y) * dy) / lengthSq
  t = Math.max(0, Math.min(1, t))
  const projX = a.x + t * dx
  const projY = a.y + t * dy
  return Math.hypot(p.x - projX, p.y - projY)
}

function strokeHit(stroke, pos, radius) {
  const threshold = radius + stroke.width / 2
  if (stroke.points.length < 2) {
    return Math.hypot(pos.x - stroke.points[0].x, pos.y - stroke.points[0].y) <= threshold
  }
  for (let i = 1; i < stroke.points.length; i++) {
    if (distToSegment(pos, stroke.points[i - 1], stroke.points[i]) <= threshold) return true
  }
  return false
}

function eraseAt(pos) {
  const before = strokes.value.length
  strokes.value = strokes.value.filter((stroke) => !strokeHit(stroke, pos, eraserSize.value / 2))
  if (strokes.value.length !== before) redraw()
}

// Some browsers (e.g. the Samsung Family Hub's built-in browser) reveal
// their own UI chrome on any scroll delta, even a tiny one absorbed by
// touch-action/preventDefault. Freezing the page in place for the duration
// of a touch drag removes that scroll delta entirely.
let savedScrollY = 0
let scrollLocked = false

function lockPageScroll() {
  if (scrollLocked) return
  scrollLocked = true
  savedScrollY = window.scrollY
  document.body.classList.add('drawing-scroll-lock')
  document.body.style.top = `-${savedScrollY}px`
}

function unlockPageScroll() {
  if (!scrollLocked) return
  scrollLocked = false
  document.body.classList.remove('drawing-scroll-lock')
  document.body.style.top = ''
  window.scrollTo(0, savedScrollY)
}

function startDraw(e) {
  if (props.readonly) return
  e.preventDefault()
  if (e.touches) {
    lockPageScroll()
    requestKioskFullscreen()
  }
  const pos = getPos(e)
  if (tool.value === 'erase') {
    isDrawing.value = true
    eraseAt(pos)
    return
  }
  isDrawing.value = true
  currentStroke.value = {
    color: selectedColor.value,
    width: strokeWidth.value,
    points: [pos],
  }
  lastPointTime = Date.now()
}

function moveDraw(e) {
  if (!isDrawing.value) return
  e.preventDefault()
  const pos = getPos(e)
  if (tool.value === 'erase') {
    eraseAt(pos)
    return
  }
  if (!currentStroke.value) return
  const now = Date.now()
  if (now - lastPointTime < 16) return
  lastPointTime = now
  currentStroke.value.points.push(pos)
  redraw()
}

function endDraw() {
  unlockPageScroll()
  if (!isDrawing.value) return
  isDrawing.value = false
  if (currentStroke.value && currentStroke.value.points.length >= 2) {
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
  const canvas = canvasRef.value
  const json = JSON.stringify({
    strokes: strokes.value,
    canvasWidth: canvas?.width,
    canvasHeight: canvas?.height,
  })
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

  // Content must always fit — this is the smallest the canvas can be.
  const minWidth = Math.max(100, bounds.maxX - bounds.minX + padding * 2)
  const minHeight = Math.max(250, bounds.maxY - bounds.minY + padding * 2)

  const width = manualSize.value ? Math.max(minWidth, manualSize.value.width) : minWidth
  const height = manualSize.value ? Math.max(minHeight, manualSize.value.height) : minHeight

  return { width, height, padding, bounds, minWidth, minHeight }
}

// How large the canvas is allowed to grow to, based on the actual space
// available in the surrounding editor — so it can never spill off-screen.
function getMaxCanvasWidth() {
  const canvas = canvasRef.value
  const bound = canvas?.closest('.ProseMirror') || canvas?.closest('.editor-content')
  if (bound) {
    const style = window.getComputedStyle(bound)
    const paddingX = parseFloat(style.paddingLeft || 0) + parseFloat(style.paddingRight || 0)
    return Math.max(150, bound.getBoundingClientRect().width - paddingX)
  }
  return Math.max(150, window.innerWidth - 48)
}

function getMaxCanvasHeight() {
  return Math.max(150, window.innerHeight * 0.75)
}

function startResize(e) {
  if (props.readonly) return
  e.preventDefault()
  const canvas = canvasRef.value
  if (!canvas) return
  const clientX = (evt) => (evt.touches ? evt.touches[0].clientX : evt.clientX)
  const clientY = (evt) => (evt.touches ? evt.touches[0].clientY : evt.clientY)
  const startX = clientX(e)
  const startY = clientY(e)
  const startWidth = canvas.width
  const startHeight = canvas.height
  const maxWidth = getMaxCanvasWidth()
  const maxHeight = getMaxCanvasHeight()
  const { minWidth, minHeight } = getOptimalCanvasDimensions()
  resizing.value = true
  if (e.touches) lockPageScroll()

  function onMove(moveEvent) {
    moveEvent.preventDefault()
    const newWidth = Math.min(maxWidth, Math.max(minWidth, startWidth + (clientX(moveEvent) - startX)))
    const newHeight = Math.min(maxHeight, Math.max(minHeight, startHeight + (clientY(moveEvent) - startY)))
    manualSize.value = { width: newWidth, height: newHeight }
    resizeCanvas()
  }

  function onEnd() {
    resizing.value = false
    unlockPageScroll()
    window.removeEventListener('mousemove', onMove)
    window.removeEventListener('mouseup', onEnd)
    window.removeEventListener('touchmove', onMove)
    window.removeEventListener('touchend', onEnd)
  }

  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', onEnd)
  window.addEventListener('touchmove', onMove, { passive: false })
  window.addEventListener('touchend', onEnd)
}

watch(
  () => props.strokesJson,
  (val) => {
    const parsed = parseDrawingData(val)
    strokes.value = parsed.strokes
    manualSize.value = parsed.size
    resizeCanvas()
  },
  { immediate: true }
)

onMounted(() => {
  resizeCanvas()
  window.addEventListener('resize', resizeCanvas)
})

onUnmounted(() => {
  window.removeEventListener('resize', resizeCanvas)
  unlockPageScroll()
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
        <label
          class="color-btn custom-color-btn"
          :class="{ active: !colors.includes(selectedColor) }"
          :style="{ background: !colors.includes(selectedColor) ? selectedColor : undefined }"
          title="Własny kolor"
        >
          <input type="color" :value="selectedColor" @input="selectedColor = $event.target.value" />
        </label>
      </div>
      <div class="width-picker" v-if="tool === 'draw'">
        <label>Szerokość:</label>
        <input v-model.number="strokeWidth" type="range" min="1" max="12" />
        <span>{{ strokeWidth }}px</span>
      </div>
      <div class="width-picker" v-else>
        <label>Rozmiar gumki:</label>
        <input v-model.number="eraserSize" type="range" min="10" max="60" />
        <span>{{ eraserSize }}px</span>
      </div>
      <div class="tool-picker">
        <button
          class="btn btn-sm"
          :class="tool === 'draw' ? 'btn-accent' : 'btn-ghost'"
          title="Rysuj"
          @click="tool = 'draw'"
        >
          <ToolbarIcon name="drawing" /> Rysuj
        </button>
        <button
          class="btn btn-sm"
          :class="tool === 'erase' ? 'btn-accent' : 'btn-ghost'"
          title="Gumka"
          @click="tool = 'erase'"
        >
          <ToolbarIcon name="eraser" /> Gumka
        </button>
      </div>
      <div class="toolbar-actions">
        <button class="btn btn-ghost btn-sm" @click="undo" :disabled="!strokes.length">Cofnij</button>
        <button class="btn btn-ghost btn-sm" @click="clear" :disabled="!strokes.length">Wyczyść</button>
        <button class="btn btn-ghost btn-sm" title="Pełny ekran" @click="toggleFullscreen"><ToolbarIcon name="fullscreen" /></button>
        <button class="btn btn-accent btn-sm" @click="save">Zapisz rysunek</button>
      </div>
    </div>
    <div class="canvas-wrap">
      <canvas
        ref="canvasRef"
        class="canvas"
        :class="{ readonly, erasing: tool === 'erase' && !readonly }"
        @mousedown="startDraw"
        @mousemove="moveDraw"
        @mouseup="endDraw"
        @mouseleave="endDraw"
        @touchstart="startDraw"
        @touchmove="moveDraw"
        @touchend="endDraw"
      />
      <div
        v-if="!readonly"
        class="resize-handle"
        :class="{ resizing }"
        title="Przeciągnij, aby zmienić rozmiar płótna"
        @mousedown="startResize"
        @touchstart="startResize"
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
  background: rgba(153, 209, 156, 0.1);
  border-radius: var(--radius-sm);
}

.color-picker {
  display: flex;
  flex-wrap: wrap;
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

.custom-color-btn {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: conic-gradient(red, yellow, lime, cyan, blue, magenta, red);
  font-size: 0.9375rem;
  line-height: 1;
  overflow: hidden;
}

.custom-color-btn::after {
  content: '+';
  color: #fff;
  text-shadow: 0 0 2px rgba(0, 0, 0, 0.6);
  pointer-events: none;
}

.custom-color-btn.active::after {
  content: '';
}

.custom-color-btn input[type='color'] {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  opacity: 0;
  cursor: pointer;
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

.tool-picker {
  display: flex;
  gap: 0.375rem;
}

.toolbar-actions {
  display: flex;
  gap: 0.375rem;
  margin-left: auto;
  flex-wrap: wrap;
}

.canvas-wrap {
  position: relative;
  border: 2px solid var(--color-border);
  border-radius: var(--radius-sm);
  overflow: hidden;
  background: white;
}

.resize-handle {
  position: absolute;
  right: 0;
  bottom: 0;
  width: 1.1rem;
  height: 1.1rem;
  background: linear-gradient(135deg, transparent 50%, var(--color-primary) 50%);
  cursor: nwse-resize;
  touch-action: none;
}

.resize-handle.resizing {
  filter: brightness(1.2);
}

.canvas {
  display: block;
  cursor: crosshair;
  /* Block panning/scrolling only while touching an editable canvas - a
     readonly drawing shown in the note body must not swallow page scroll. */
  touch-action: none;
}

.canvas.readonly {
  cursor: default;
  touch-action: auto;
}

.canvas.erasing {
  cursor: cell;
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
