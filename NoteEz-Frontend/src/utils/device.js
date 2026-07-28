// Detects Samsung Family Hub-style embedded browsers (Tizen-based fridge
// displays). Their browser has no visible chrome-hiding setting and swipes
// near the screen edge reveal OS/browser UI mid-gesture, so we only take the
// (fairly aggressive) step of forcing fullscreen mode on devices like this.
export function isLikelyKioskDevice() {
  const ua = navigator.userAgent || ''
  return /Tizen|SmartHub|Family ?Hub|SMART-TV/i.test(ua)
}

let fullscreenRequested = false

function getFullscreenRequestFn() {
  const el = document.documentElement
  return el.requestFullscreen || el.webkitRequestFullscreen || el.mozRequestFullScreen || null
}

export function isFullscreenApiAvailable() {
  return !!getFullscreenRequestFn()
}

// Must be called from within a user gesture handler (touchstart/click) -
// browsers reject Fullscreen API calls outside one.
export function requestKioskFullscreen() {
  if (fullscreenRequested) return
  if (!isLikelyKioskDevice()) return
  if (document.fullscreenElement) return
  fullscreenRequested = true
  const request = getFullscreenRequestFn()
  if (!request) return
  request.call(document.documentElement).catch(() => {
    // Ignore - some kiosk browsers reject this despite exposing the API.
    fullscreenRequested = false
  })
}

// Manual trigger for a visible "fullscreen" button, with feedback so we can
// tell whether the browser actually supports/allows it at all.
export function toggleFullscreen() {
  if (document.fullscreenElement) {
    document.exitFullscreen?.()
    return
  }
  const request = getFullscreenRequestFn()
  if (!request) {
    alert('Ta przeglądarka nie udostępnia trybu pełnoekranowego.')
    return
  }
  request.call(document.documentElement).catch(() => {
    alert('Przeglądarka odrzuciła próbę przejścia w tryb pełnoekranowy.')
  })
}
