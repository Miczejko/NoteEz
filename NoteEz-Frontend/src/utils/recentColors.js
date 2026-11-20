import { ref } from 'vue'

// Ostatnio uzywane kolory - tylko po stronie przegladarki (bez backendu).
// localStorage, zeby przetrwaly odswiezenie/zamkniecie karty; kazdy dostep
// owiniety w try/catch (tryb prywatny, zablokowane dane strony).
const STORAGE_KEY = 'noteez:recent-colors'
const MAX_RECENT = 8

function load() {
  try {
    const parsed = JSON.parse(localStorage.getItem(STORAGE_KEY))
    if (Array.isArray(parsed)) {
      return parsed.filter((c) => typeof c === 'string' && /^#[0-9a-f]{6}$/i.test(c)).slice(0, MAX_RECENT)
    }
  } catch {
    // brak/uszkodzony zapis - zaczynamy od pustej listy
  }
  return []
}

// Wspolny stan modulu - kazdy edytor/komponent widzi te sama liste.
const recentColors = ref(load())

export function useRecentColors() {
  function addRecentColor(color) {
    if (!color) return
    const normalized = color.toLowerCase()
    recentColors.value = [normalized, ...recentColors.value.filter((c) => c !== normalized)].slice(0, MAX_RECENT)
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(recentColors.value))
    } catch {
      // bez zapisu dziala dalej, tylko w ramach biezacej sesji strony
    }
  }

  return { recentColors, addRecentColor }
}
