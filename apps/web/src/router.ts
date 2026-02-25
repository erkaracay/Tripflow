import { createRouter, createWebHistory } from 'vue-router'
import { i18n } from './i18n'
import { checkAuth, checkPortalSession } from './lib/api'
import { clearToken, getSelectedOrgId, setAuthState } from './lib/auth'
import { resetViewportZoom } from './lib/viewport'
import type { UserRole } from './types'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/e/login' },
    { path: '/login', component: () => import('./pages/Login.vue') },
    { path: '/forbidden', component: () => import('./pages/Forbidden.vue') },
    {
      path: '/admin/events',
      component: () => import('./pages/admin/AdminEvents.vue'),
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId',
      component: () => import('./pages/admin/AdminEventDetail.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/checkin',
      component: () => import('./pages/admin/AdminEventCheckIn.vue'),
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/logs',
      component: () => import('./pages/admin/AdminEventLogs.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/program',
      component: () => import('./pages/admin/AdminEventProgram.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/activities/checkin',
      component: () => import('./pages/admin/AdminActivityCheckIn.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/equipment',
      component: () => import('./pages/admin/AdminEquipment.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/docs/tabs',
      component: () => import('./pages/admin/AdminEventDocsTabs.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/participants/import',
      component: () => import('./pages/admin/AdminParticipantsImport.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/participants/table',
      component: () => import('./pages/admin/AdminParticipantsTable.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/events/:eventId/participants/:participantId',
      component: () => import('./pages/admin/AdminParticipantProfile.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/admin/orgs',
      component: () => import('./pages/admin/AdminOrganizations.vue'),
      meta: { requiresAuth: true, roles: ['SuperAdmin'] },
    },
    {
      path: '/admin/users',
      component: () => import('./pages/admin/AdminUsers.vue'),
      meta: { requiresAuth: true, roles: ['SuperAdmin'] },
    },
    {
      path: '/admin/guides',
      component: () => import('./pages/admin/AdminGuides.vue'),
      meta: { requiresAuth: true, roles: ['AgencyAdmin', 'SuperAdmin'] },
    },
    {
      path: '/guide/events',
      component: () => import('./pages/guide/GuideEvents.vue'),
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    {
      path: '/guide/events/:eventId/checkin',
      component: () => import('./pages/guide/GuideEventCheckIn.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    {
      path: '/guide/events/:eventId/activities/checkin',
      component: () => import('./pages/guide/GuideActivityCheckIn.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    {
      path: '/guide/events/:eventId/equipment',
      component: () => import('./pages/guide/GuideEquipment.vue'),
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
      component: () => import('./pages/admin/AdminEventProgram.vue'),
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    { path: '/e/login', component: () => import('./pages/portal/PortalLogin.vue') },
    { path: '/e/:eventId/docs/print', component: () => import('./pages/portal/PortalDocsPrint.vue'), props: true },
    { path: '/e/:eventId', component: () => import('./pages/portal/EventPortal.vue'), props: true },
  ],
})

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
  // Home - redirect to login
  if (to.path === '/') {
    return { path: '/e/login', replace: true }
  }

  // Portal login page - check if session exists, if so redirect to event
  if (to.path === '/e/login') {
    return checkPortalSession()
      .then((me) => {
        if (me) return { path: `/e/${me.event.id}`, replace: true }
        return true // Allow navigation to login page
      })
      .catch(() => {
        // Network error or 401 (no session) - allow navigation to login page
        return true
      })
  }

  // Portal session check - cookie-based
  if (to.path.startsWith('/e/') && to.path !== '/e/login') {
    const eventId = to.params.eventId as string
    return checkPortalSession()
      .then((me) => {
        if (me) return true
        return { path: '/e/login', query: eventId ? { eventId } : {} }
      })
      .catch(() => {
        // Network error or server error - redirect to login
        return { path: '/e/login', query: eventId ? { eventId } : {} }
      })
  }

  // Admin/auth guard - only applies to routes with requiresAuth meta
  if (!to.meta.requiresAuth) {
    return true
  }

  return checkAuth()
    .then((me) => {
      if (!me) {
        clearToken()
        return { path: '/login' }
      }
      setAuthState(me)
      const role = me.role
      const roles = to.meta.roles as UserRole[] | undefined
      if (roles && roles.length > 0) {
        if (!role || !roles.includes(role as UserRole)) {
          return { path: '/forbidden' }
        }
      }
      if (role === 'SuperAdmin') {
        const hasOrg = Boolean(getSelectedOrgId())
        if (!hasOrg && to.path.startsWith('/admin') && to.path !== '/admin/orgs') {
          return { path: '/admin/orgs' }
        }
      }
      return true
    })
    .catch(() => {
      // Network error or server error - redirect to login
      clearToken()
      return { path: '/login' }
    })
})

export default router
