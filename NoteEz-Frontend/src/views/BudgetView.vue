<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useBudgetStore } from '../stores/budget'
import { useGroupsStore } from '../stores/groups'
import AppLayout from '../components/AppLayout.vue'

const route = useRoute()
const budgetStore = useBudgetStore()
const groupsStore = useGroupsStore()

// ?groupId=... -> budzet grupowy, brak parametru -> budzet osobisty zalogowanego usera
const groupId = computed(() => route.query.groupId || null)
const group = computed(() => groupsStore.groups.find((g) => g.id === groupId.value))

const showAddForm = ref(false)
const showAddCategory = ref(false)
const newCategoryName = ref('')
const submitting = ref(false)
const formError = ref(null)
const pendingRecurringId = ref(null)

const form = ref({
  type: 'Expense',
  categoryId: '',
  amount: '',
  description: '',
  date: new Date().toISOString().slice(0, 10),
})

const showRecurringForm = ref(false)
const recurringSubmitting = ref(false)
const recurringForm = ref({
  type: 'Expense',
  categoryId: '',
  amount: '',
  description: '',
  frequency: 'Monthly',
  nextDueDate: new Date().toISOString().slice(0, 10),
})

async function loadAll() {
  await Promise.all([
    budgetStore.fetchCategories(groupId.value),
    budgetStore.fetchEntries(groupId.value),
    budgetStore.fetchStats(groupId.value),
    budgetStore.fetchRecurring(groupId.value),
    budgetStore.fetchDueSoon(groupId.value),
  ])
}

onMounted(() => {
  if (groupId.value) groupsStore.fetchGroups()
  loadAll()
})

watch(groupId, loadAll)

async function handleCreateCategory() {
  if (!newCategoryName.value.trim()) return
  const category = await budgetStore.createCategory(groupId.value, { name: newCategoryName.value.trim() })
  form.value.categoryId = category.id
  newCategoryName.value = ''
  showAddCategory.value = false
}

function openAddForm() {
  pendingRecurringId.value = null
  form.value = { type: 'Expense', categoryId: '', amount: '', description: '', date: new Date().toISOString().slice(0, 10) }
  showAddForm.value = true
}

// Klikniecie w baner "nadchodzi" otwiera ten sam formularz, ale wypelniony danymi
// z cyklicznego wpisu - uzytkownik moze je poprawic przed zatwierdzeniem.
function openAddFormFromRecurring(r) {
  pendingRecurringId.value = r.id
  form.value = {
    type: r.type,
    categoryId: r.categoryId,
    amount: String(r.amount),
    description: r.description || '',
    date: new Date().toISOString().slice(0, 10),
  }
  showAddForm.value = true
}

async function handleSubmit() {
  formError.value = null
  if (!form.value.categoryId) {
    formError.value = 'Wybierz kategorię.'
    return
  }
  if (!form.value.amount || Number(form.value.amount) <= 0) {
    formError.value = 'Podaj kwotę większą od zera.'
    return
  }

  submitting.value = true
  try {
    if (pendingRecurringId.value) {
      await budgetStore.confirmRecurring(pendingRecurringId.value, {
        amount: Number(form.value.amount),
        description: form.value.description || null,
        date: form.value.date,
      })
      // Zatwierdzenie przesuwa NextDueDate po stronie backendu - doczytaj swiezy stan,
      // zeby lista cyklicznych wpisow nie pokazywala nieaktualnej daty.
      await budgetStore.fetchRecurring(groupId.value)
    } else {
      await budgetStore.createEntry(groupId.value, {
        categoryId: form.value.categoryId,
        type: form.value.type,
        amount: Number(form.value.amount),
        description: form.value.description || null,
        date: form.value.date,
      })
    }
    await budgetStore.fetchStats(groupId.value)
    pendingRecurringId.value = null
    form.value.amount = ''
    form.value.description = ''
    showAddForm.value = false
  } catch (e) {
    formError.value = e.response?.data?.error || 'Nie udało się dodać wpisu.'
  } finally {
    submitting.value = false
  }
}

async function handleDelete(entryId) {
  await budgetStore.deleteEntry(entryId)
  await budgetStore.fetchStats(groupId.value)
}

async function handleCreateRecurring() {
  if (!recurringForm.value.categoryId || !recurringForm.value.amount) return
  recurringSubmitting.value = true
  try {
    await budgetStore.createRecurring(groupId.value, {
      categoryId: recurringForm.value.categoryId,
      type: recurringForm.value.type,
      amount: Number(recurringForm.value.amount),
      description: recurringForm.value.description || null,
      frequency: recurringForm.value.frequency,
      nextDueDate: recurringForm.value.nextDueDate,
    })
    await budgetStore.fetchDueSoon(groupId.value)
    recurringForm.value.amount = ''
    recurringForm.value.description = ''
    showRecurringForm.value = false
  } finally {
    recurringSubmitting.value = false
  }
}

async function handleDeleteRecurring(id) {
  await budgetStore.deleteRecurring(id)
}

function formatAmount(amount, currency) {
  return `${Number(amount).toFixed(2)} ${currency}`
}

function formatDate(dateStr) {
  return new Date(dateStr).toLocaleDateString('pl-PL', { day: 'numeric', month: 'short', year: 'numeric' })
}
</script>

<template>
  <AppLayout>
    <div class="budget-page">
      <div class="page-header">
        <div class="page-header-title">
          <router-link
            :to="groupId ? { name: 'group-detail', params: { id: groupId } } : { name: 'notes' }"
            class="btn btn-ghost btn-sm back-btn"
          >← Powrót</router-link>
          <h1>{{ group ? `Budżet grupy: ${group.name}` : 'Mój budżet' }}</h1>
        </div>
        <button class="btn btn-primary" @click="showAddForm ? (showAddForm = false) : openAddForm()">
          {{ showAddForm ? 'Anuluj' : '+ Dodaj wpis' }}
        </button>
      </div>

      <div v-if="budgetStore.dueSoon.length" class="due-soon-list">
        <div v-for="r in budgetStore.dueSoon" :key="r.id" class="due-soon-banner card">
          <span>
            Nadchodzi: <strong>{{ r.description || r.categoryName }}</strong>,
            {{ formatAmount(r.amount, r.currency) }} ({{ formatDate(r.nextDueDate) }})
          </span>
          <button class="btn btn-accent btn-sm" @click="openAddFormFromRecurring(r)">Dodaj</button>
        </div>
      </div>

      <div v-if="budgetStore.stats" class="stats-row">
        <div class="stat-card card">
          <span class="stat-label">Przychody</span>
          <span class="stat-value income">+{{ formatAmount(budgetStore.stats.totalIncome, 'PLN') }}</span>
        </div>
        <div class="stat-card card">
          <span class="stat-label">Wydatki</span>
          <span class="stat-value expense">-{{ formatAmount(budgetStore.stats.totalExpense, 'PLN') }}</span>
        </div>
        <div class="stat-card card">
          <span class="stat-label">Bilans</span>
          <span class="stat-value" :class="budgetStore.stats.balance >= 0 ? 'income' : 'expense'">
            {{ formatAmount(budgetStore.stats.balance, 'PLN') }}
          </span>
        </div>
      </div>

      <div v-if="showAddForm" class="add-form card">
        <p v-if="pendingRecurringId" class="recurring-hint">Zatwierdzasz cykliczny wpis - możesz poprawić dane przed dodaniem.</p>
        <div class="form-row">
          <select v-model="form.type" class="input-field">
            <option value="Expense">Wydatek</option>
            <option value="Income">Przychód</option>
          </select>

          <select v-model="form.categoryId" class="input-field">
            <option value="" disabled>Kategoria…</option>
            <option v-for="c in budgetStore.categories" :key="c.id" :value="c.id">{{ c.name }}</option>
          </select>
          <button type="button" class="btn btn-ghost btn-sm" @click="showAddCategory = !showAddCategory">
            + kategoria
          </button>
        </div>

        <div v-if="showAddCategory" class="form-row">
          <input
            v-model="newCategoryName"
            type="text"
            class="input-field"
            placeholder="Nazwa nowej kategorii…"
            maxlength="50"
            @keyup.enter="handleCreateCategory"
          />
          <button type="button" class="btn btn-accent btn-sm" @click="handleCreateCategory">Utwórz</button>
        </div>

        <div class="form-row">
          <input v-model="form.amount" type="number" min="0.01" step="0.01" class="input-field" placeholder="Kwota" />
          <input v-model="form.date" type="date" class="input-field" />
        </div>

        <div class="form-row">
          <input v-model="form.description" type="text" class="input-field" placeholder="Opis (opcjonalnie)" maxlength="500" />
        </div>

        <p v-if="formError" class="state-msg error-msg">{{ formError }}</p>

        <button class="btn btn-accent" :disabled="submitting" @click="handleSubmit">
          {{ submitting ? 'Dodawanie…' : 'Dodaj' }}
        </button>
      </div>

      <div v-if="budgetStore.loading" class="state-msg">Ładowanie…</div>
      <div v-else-if="budgetStore.error" class="state-msg error-msg">{{ budgetStore.error }}</div>
      <div v-else-if="!budgetStore.entries.length" class="empty-state card">
        <p>Brak wpisów. Dodaj pierwszy przychód lub wydatek.</p>
      </div>
      <ul v-else class="entries-list">
        <li v-for="entry in budgetStore.entries" :key="entry.id" class="entry-row card">
          <div class="entry-main">
            <span class="entry-category">{{ entry.categoryName }}</span>
            <span v-if="entry.description" class="entry-desc">{{ entry.description }}</span>
            <span v-if="groupId" class="entry-author">dodane przez: {{ entry.createdByUsername }}</span>
          </div>
          <div class="entry-side">
            <span class="entry-date">{{ formatDate(entry.date) }}</span>
            <span class="entry-amount" :class="entry.type === 'Income' ? 'income' : 'expense'">
              {{ entry.type === 'Income' ? '+' : '-' }}{{ formatAmount(entry.amount, entry.currency) }}
            </span>
            <button class="btn btn-ghost btn-sm" @click="handleDelete(entry.id)">Usuń</button>
          </div>
        </li>
      </ul>

      <div class="recurring-section">
        <div class="page-header">
          <h2>Cykliczne wydatki i przychody</h2>
          <button class="btn btn-ghost btn-sm" @click="showRecurringForm = !showRecurringForm">
            {{ showRecurringForm ? 'Anuluj' : '+ Dodaj cykliczny' }}
          </button>
        </div>

        <div v-if="showRecurringForm" class="add-form card">
          <div class="form-row">
            <select v-model="recurringForm.type" class="input-field">
              <option value="Expense">Wydatek</option>
              <option value="Income">Przychód</option>
            </select>
            <select v-model="recurringForm.categoryId" class="input-field">
              <option value="" disabled>Kategoria…</option>
              <option v-for="c in budgetStore.categories" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
            <select v-model="recurringForm.frequency" class="input-field">
              <option value="Monthly">Co miesiąc</option>
              <option value="Weekly">Co tydzień</option>
            </select>
          </div>
          <div class="form-row">
            <input v-model="recurringForm.amount" type="number" min="0.01" step="0.01" class="input-field" placeholder="Kwota" />
            <input v-model="recurringForm.nextDueDate" type="date" class="input-field" />
          </div>
          <div class="form-row">
            <input v-model="recurringForm.description" type="text" class="input-field" placeholder="Opis (np. Wypłata, Czynsz)" maxlength="500" />
          </div>
          <button class="btn btn-accent" :disabled="recurringSubmitting" @click="handleCreateRecurring">
            {{ recurringSubmitting ? 'Dodawanie…' : 'Dodaj cykliczny wpis' }}
          </button>
        </div>

        <p v-if="!budgetStore.recurring.length" class="state-msg">Brak cyklicznych wpisów.</p>
        <ul v-else class="entries-list">
          <li v-for="r in budgetStore.recurring" :key="r.id" class="entry-row card">
            <div class="entry-main">
              <span class="entry-category">{{ r.categoryName }}</span>
              <span v-if="r.description" class="entry-desc">{{ r.description }}</span>
              <span class="entry-desc">
                {{ r.frequency === 'Monthly' ? 'Co miesiąc' : 'Co tydzień' }}, najbliższy: {{ formatDate(r.nextDueDate) }}
              </span>
            </div>
            <div class="entry-side">
              <span class="entry-amount" :class="r.type === 'Income' ? 'income' : 'expense'">
                {{ r.type === 'Income' ? '+' : '-' }}{{ formatAmount(r.amount, r.currency) }}
              </span>
              <button class="btn btn-ghost btn-sm" @click="handleDeleteRecurring(r.id)">Usuń</button>
            </div>
          </li>
        </ul>
      </div>
    </div>
  </AppLayout>
</template>

<style scoped>
.budget-page {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.page-header-title {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.page-header h1 {
  font-size: 1.5rem;
  color: var(--color-primary);
}

.due-soon-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.due-soon-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.75rem 1.125rem;
  border-left: 3px solid var(--color-accent, #79c7c5);
  flex-wrap: wrap;
}

.recurring-hint {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
  margin: 0;
}

.recurring-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-top: 1rem;
}

.recurring-section h2 {
  font-size: 1.125rem;
  color: var(--color-primary);
}

.stats-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 1rem;
}

.stat-card {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 1rem 1.25rem;
}

.stat-label {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.stat-value {
  font-size: 1.375rem;
  font-weight: 700;
}

.income {
  color: var(--muted-teal, #3a8a6f);
}

.expense {
  color: #c0554a;
}

.add-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1.25rem;
}

.form-row {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.form-row .input-field {
  flex: 1;
  min-width: 140px;
}

.entries-list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0;
  margin: 0;
}

.entry-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.875rem 1.125rem;
  flex-wrap: wrap;
}

.entry-main {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 0;
}

.entry-category {
  font-weight: 600;
}

.entry-desc,
.entry-author {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.entry-side {
  display: flex;
  align-items: center;
  gap: 0.875rem;
}

.entry-date {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.entry-amount {
  font-weight: 700;
  min-width: 90px;
  text-align: right;
}

.empty-state {
  text-align: center;
  padding: 3rem 1.5rem;
  color: var(--color-text-muted);
}

.state-msg {
  text-align: center;
  padding: 1rem;
  color: var(--color-text-muted);
}

.error-msg {
  color: #c0554a;
}
</style>
