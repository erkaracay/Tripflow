import { createRouter, createWebHistory } from 'vue-router'
import { clearToken, getSelectedOrgId, getToken, getTokenRole, isTokenExpired } from './lib/auth'
import AdminEvents from './pages/admin/AdminEvents.vue'
import AdminEventDetail from './pages/admin/AdminEventDetail.vue'
import AdminEventCheckIn from './pages/admin/AdminEventCheckIn.vue'
import AdminEventLogs from './pages/admin/AdminEventLogs.vue'
import AdminParticipantsImport from './pages/admin/AdminParticipantsImport.vue'
import AdminParticipantProfile from './pages/admin/AdminParticipantProfile.vue'
import AdminParticipantsTable from './pages/admin/AdminParticipantsTable.vue'
import AdminEventProgram from './pages/admin/AdminEventProgram.vue'
import AdminEventDocsTabs from './pages/admin/AdminEventDocsTabs.vue'
import AdminOrganizations from './pages/admin/AdminOrganizations.vue'
import AdminUsers from './pages/admin/AdminUsers.vue'
import AdminGuides from './pages/admin/AdminGuides.vue'
import GuideEvents from './pages/guide/GuideEvents.vue'
import GuideEventCheckIn from './pages/guide/GuideEventCheckIn.vue'
import GuideEventProgram from './pages/guide/GuideEventProgram.vue'
import Login from './pages/Login.vue'
import Forbidden from './pages/Forbidden.vue'
import EventPortal from './pages/portal/EventPortal.vue'
import PortalDocsPrint from './pages/portal/PortalDocsPrint.vue'
import PortalLogin from './pages/portal/PortalLogin.vue'
import type { UserRole } from './types'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/admin/events' },
    { path: '/login', component: Login },
    { path: '/forbidden', component: Forbidden },
    {
      path: '/admin/events',
      component: AdminEvents,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId',
      component: AdminEventDetail,
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/checkin',
      component: AdminEventCheckIn,
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/logs',
      component: AdminEventLogs,
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/program',
      component: AdminEventProgram,
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/docs/tabs',
      component: AdminEventDocsTabs,
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/participants/import',
      component: AdminParticipantsImport,
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/participants/table',
      component: AdminParticipantsTable,
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/participants/:participantId',
      component: AdminParticipantProfile,
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/orgs',
      component: AdminOrganizations,
      meta: { requiresAuth: true, roles: ['SuperAdmin'] },
    },
    {
      path: '/admin/users',
      component: AdminUsers,
      meta: { requiresAuth: true, roles: ['SuperAdmin'] },
    },
    {
      path: '/admin/guides',
      component: AdminGuides,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/guide/events',
      component: GuideEvents,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    {
      path: '/guide/events/:eventId/checkin',
      component: GuideEventCheckIn,
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    {
      path: '/guide/events/:eventId/program',
      component: GuideEventProgram,
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    { path: '/e/login', component: PortalLogin },
    { path: '/e/:eventId/docs/print', component: PortalDocsPrint, props: true },
    { path: '/e/:eventId', component: EventPortal, props: true },
  ],
})

router.beforeEach((to) => {
  if (!to.meta.requiresAuth) {
    return true
  }

  const token = getToken()
  if (!token || isTokenExpired(token)) {
    if (token) {
      clearToken()
    }
    return { path: '/login' }
  }

  const roles = to.meta.roles as UserRole[] | undefined
  if (roles && roles.length > 0) {
    const role = getTokenRole(token)
    if (!role || !roles.includes(role as UserRole)) {
      return { path: '/forbidden' }
    }
  }

  const role = getTokenRole(token)
  if (role === 'SuperAdmin') {
    const hasOrg = Boolean(getSelectedOrgId())
    if (!hasOrg && to.path.startsWith('/admin') && to.path !== '/admin/orgs') {
      return { path: '/admin/orgs' }
    }
  }

  return true
})

export default router
