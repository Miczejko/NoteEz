import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const routes = [
  {
    path: '/',
    name: 'home',
    component: () => import('../views/HomeView.vue'),
  },
  {
    path: '/polityka-prywatnosci',
    name: 'privacy-policy',
    component: () => import('../views/PrivacyPolicyView.vue'),
  },
  {
    path: '/regulamin',
    name: 'terms-of-service',
    component: () => import('../views/TermsOfServiceView.vue'),
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('../views/LoginView.vue'),
    meta: { guest: true },
  },
  {
    path: '/register',
    name: 'register',
    component: () => import('../views/RegisterView.vue'),
    meta: { guest: true },
  },
  {
    path: '/reset-password',
    name: 'reset-password',
    component: () => import('../views/ResetPasswordView.vue'),
  },
  {
    path: '/account',
    name: 'account',
    component: () => import('../views/AccountView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/notes',
    name: 'notes',
    component: () => import('../views/NotesView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/notes/:id',
    name: 'note-detail',
    component: () => import('../views/NoteDetailView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/devices',
    name: 'devices',
    component: () => import('../views/DevicesView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/calendar',
    name: 'calendar',
    component: () => import('../views/CalendarView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/groups',
    name: 'groups',
    component: () => import('../views/GroupsView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/groups/:id',
    name: 'group-detail',
    component: () => import('../views/GroupDetailView.vue'),
    meta: { requiresAuth: true },
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()

  // Access token zyje tylko w pamieci (nie w localStorage), wiec po twardym
  // odswiezeniu strony trzeba go raz odzyskac z refresh-token cookie, zanim
  // zdecydujemy, czy uzytkownik jest zalogowany.
  if (to.meta.requiresAuth || to.meta.guest) {
    await auth.initialize()
  }

  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    return { name: 'login' }
  }
  if (to.meta.guest && auth.isAuthenticated) {
    return { name: 'notes' }
  }
})

export default router
