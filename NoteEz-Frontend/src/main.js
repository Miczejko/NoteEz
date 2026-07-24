import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import './assets/main.css'

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.mount('#app')

// Blokuje overscroll/"odbicie" tylko przy samym dole strony (myslace na telefonie),
// ale zostawia normalne "pull to refresh" u gory.
function updateOverscrollGuard() {
  const scrollTop = window.scrollY || document.documentElement.scrollTop
  const scrollHeight = document.documentElement.scrollHeight
  const clientHeight = document.documentElement.clientHeight
  const atBottom = scrollTop + clientHeight >= scrollHeight - 1
  document.body.classList.toggle('at-bottom', atBottom)
}
window.addEventListener('scroll', updateOverscrollGuard, { passive: true })
window.addEventListener('resize', updateOverscrollGuard)
router.afterEach(() => setTimeout(updateOverscrollGuard, 0))
updateOverscrollGuard()
