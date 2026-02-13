import { createRouter, createWebHistory } from 'vue-router'
import { i18n } from './i18n'
import { clearToken, getSelectedOrgId, getToken, getTokenRole, isTokenExpired } from './lib/auth'
import { resetViewportZoom } from './lib/viewport'
import AdminEvents from './pages/admin/AdminEvents.vue'
import AdminEventDetail from './pages/admin/AdminEventDetail.vue'
import AdminEventCheckIn from './pages/admin/AdminEventCheckIn.vue'
import AdminEventLogs from './pages/admin/AdminEventLogs.vue'
import AdminParticipantsImport from './pages/admin/AdminParticipantsImport.vue'
import AdminParticipantProfile from './pages/admin/AdminParticipantProfile.vue'
import AdminParticipantsTable from './pages/admin/AdminParticipantsTable.vue'
import AdminEventProgram from './pages/admin/AdminEventProgram.vue'
import AdminActivityCheckIn from '@/pages/admin/AdminActivityCheckIn.vue'
import AdminEquipment from '@/pages/admin/AdminEquipment.vue'
import AdminEventDocsTabs from './pages/admin/AdminEventDocsTabs.vue'
import AdminOrganizations from './pages/admin/AdminOrganizations.vue'
import AdminUsers from './pages/admin/AdminUsers.vue'
import AdminGuides from './pages/admin/AdminGuides.vue'
import GuideEvents from './pages/guide/GuideEvents.vue'
import GuideEventCheckIn from './pages/guide/GuideEventCheckIn.vue'
import GuideActivityCheckIn from '@/pages/guide/GuideActivityCheckIn.vue'
import GuideEquipment from '@/pages/guide/GuideEquipment.vue'
import Login from './pages/Login.vue'
import Forbidden from './pages/Forbidden.vue'
import EventPortal from './pages/portal/EventPortal.vue'
import PortalDocsPrint from './pages/portal/PortalDocsPrint.vue'
import PortalLogin from './pages/portal/PortalLogin.vue'
import type { UserRole } from './types'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/e/login' },
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
      path: '/admin/events/:eventId/activities/checkin',
      component: AdminActivityCheckIn,
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/equipment',
      component: AdminEquipment,
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
      path: '/guide/events/:eventId/activities/checkin',
      component: GuideActivityCheckIn,
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    {
      path: '/guide/events/:eventId/equipment',
      component: GuideEquipment,
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    {
      path: '/guide/events/:eventId/program',
      component: () => import('./pages/guide/GuideEventProgram.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    {
      path: '/guide/events/:eventId/program/edit',
      component: AdminEventProgram,
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    { path: '/e/login', component: PortalLogin },
    { path: '/e/:eventId/docs/print', component: PortalDocsPrint, props: true },
    { path: '/e/:eventId', component: EventPortal, props: true },
  ],
})

/**
 * Checks if a valid portal session exists in localStorage for the given eventId
 * @param eventId - The event ID to check session for
 * @returns true if session exists and is valid (not expired), false otherwise
 */
function restorePortalSession(eventId: string): boolean {
  if (!eventId || typeof eventId !== 'string' || eventId.trim() === '') {
    return false
  }

  const tokenKey = `infora.portal.session.${eventId}`
  const expiryKey = `infora.portal.session.exp.${eventId}`

  try {
    const token = globalThis.localStorage?.getItem(tokenKey) ?? ''
    const expiry = globalThis.localStorage?.getItem(expiryKey) ?? ''

    if (!token || !expiry || typeof token !== 'string' || typeof expiry !== 'string') {
      return false
    }

    const expiresAt = new Date(expiry)
    if (Number.isNaN(expiresAt.getTime()) || expiresAt <= new Date()) {
      return false
    }

    return true
  } catch {
    return false
  }
}

const getPageTitleKey = (path: string): string => {
  if (path === '/login') return 'common.pageTitle.login'
  if (path === '/forbidden') return 'common.pageTitle.forbidden'
  if (path.startsWith('/admin')) return 'common.pageTitle.admin'
  if (path.startsWith('/guide')) return 'common.pageTitle.guide'
  if (path.startsWith('/e/')) return 'common.pageTitle.portal'
  return 'common.appName'
}

router.afterEach((to) => {
  const titleKey = getPageTitleKey(to.path)
  const title = titleKey === 'common.appName'
    ? i18n.global.t(titleKey)
    : `${i18n.global.t(titleKey)} | ${i18n.global.t('common.appName')}`
  document.title = title
  
  // Reset viewport zoom for portal routes
  if (to.path.startsWith('/e/')) {
    resetViewportZoom()
  }
})

router.beforeEach((to) => {
  // Home page "remember me" check - redirect to last used eventId if valid session exists
  if (to.path === '/' || to.path === '/e/login') {
    const lastEventId = globalThis.localStorage?.getItem('infora.portal.lastEventId')
    if (lastEventId && restorePortalSession(lastEventId)) {
      // Valid session exists for last used eventId, redirect there
      return { path: `/e/${lastEventId}`, replace: true }
    }
    // No valid session, allow login page
    if (to.path === '/') {
      return { path: '/e/login', replace: true }
    }
    return true
  }

  // Portal session check - must happen before admin/auth checks
  if (to.path.startsWith('/e/') && to.path !== '/e/login') {
    const eventId = to.params.eventId as string
    if (eventId && restorePortalSession(eventId)) {
      // Valid portal session exists, allow access
      return true
    }
    // No valid session, redirect to login with eventId as query param
    return { path: '/e/login', query: { eventId } }
  }

  // Admin/auth guard - only applies to routes with requiresAuth meta
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
