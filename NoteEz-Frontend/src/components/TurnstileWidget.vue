<script setup>
import { onBeforeUnmount, onMounted, ref } from 'vue'

const emit = defineEmits(['verified', 'expired'])

const container = ref(null)
let widgetId = null

function renderWidget() {
  if (!window.turnstile || !container.value) return

  widgetId = window.turnstile.render(container.value, {
    sitekey: import.meta.env.VITE_TURNSTILE_SITE_KEY,
    callback: (token) => emit('verified', token),
    'expired-callback': () => emit('expired'),
    'error-callback': () => emit('expired'),
  })
}

function reset() {
  if (window.turnstile && widgetId !== null) {
    window.turnstile.reset(widgetId)
  }
}

defineExpose({ reset })

onMounted(() => {
  if (window.turnstile) {
    renderWidget()
  } else {
    // skrypt Turnstile ladowany jest asynchronicznie (async/defer w index.html) -
    // czekamy az window.turnstile bedzie dostepne
    const interval = setInterval(() => {
      if (window.turnstile) {
        clearInterval(interval)
        renderWidget()
      }
    }, 100)
    onBeforeUnmount(() => clearInterval(interval))
  }
})

onBeforeUnmount(() => {
  if (window.turnstile && widgetId !== null) {
    window.turnstile.remove(widgetId)
  }
})
</script>

<template>
  <div ref="container" class="turnstile-widget"></div>
</template>

<style scoped>
.turnstile-widget {
  display: flex;
  justify-content: center;
  margin: 0.25rem 0;
}
</style>
