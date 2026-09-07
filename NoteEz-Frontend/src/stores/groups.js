import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '../api/client'

export const useGroupsStore = defineStore('groups', () => {
  const groups = ref([])
  const pendingInvites = ref([])
  const members = ref({}) // groupId -> GroupMemberDto[]
  const loading = ref(false)
  const error = ref(null)

  async function fetchGroups() {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.get('/groups')
      groups.value = data
      return data
    } catch (e) {
      error.value = e.response?.data || 'Nie udało się pobrać grup'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function createGroup(name) {
    const { data } = await api.post('/groups', { name })
    groups.value.unshift(data)
    return data
  }

  async function fetchMembers(groupId) {
    const { data } = await api.get(`/groups/${groupId}/members`)
    members.value = { ...members.value, [groupId]: data }
    return data
  }

  async function inviteToGroup(groupId, username) {
    const { data } = await api.post(`/groups/${groupId}/invites`, { username })
    return data
  }

  async function respondToInvite(inviteId, accept) {
    await api.post(`/groups/invites/${inviteId}/respond`, { accept })
    pendingInvites.value = pendingInvites.value.filter((i) => i.id !== inviteId)
    if (accept) {
      await fetchGroups()
    }
  }

  async function leaveGroup(groupId) {
    await api.delete(`/groups/${groupId}/members/me`)
    groups.value = groups.value.filter((g) => g.id !== groupId)
  }

  async function removeMember(groupId, memberUserId) {
    await api.delete(`/groups/${groupId}/members/${memberUserId}`)
    if (members.value[groupId]) {
      members.value[groupId] = members.value[groupId].filter((m) => m.userId !== memberUserId)
    }
  }

  async function fetchPendingInvites() {
    const { data } = await api.get('/groups/invites/pending')
    pendingInvites.value = data
    return data
  }

  // Wolane przy wylogowaniu, zeby dane poprzedniego uzytkownika nie zostaly w pamieci SPA.
  function $reset() {
    groups.value = []
    pendingInvites.value = []
    members.value = {}
    loading.value = false
    error.value = null
  }

  return {
    groups,
    pendingInvites,
    members,
    loading,
    error,
    fetchGroups,
    createGroup,
    fetchMembers,
    inviteToGroup,
    respondToInvite,
    leaveGroup,
    removeMember,
    fetchPendingInvites,
    $reset,
  }
})
