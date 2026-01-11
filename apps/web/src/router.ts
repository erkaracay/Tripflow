import { createRouter, createWebHistory } from 'vue-router'
import { clearToken, getToken, getTokenRole, isTokenExpired } from './lib/auth'
import AdminTours from './pages/admin/AdminTours.vue'
import AdminTourDetail from './pages/admin/AdminTourDetail.vue'
import AdminTourCheckIn from './pages/admin/AdminTourCheckIn.vue'
import GuideTours from './pages/guide/GuideTours.vue'
import GuideTourCheckIn from './pages/guide/GuideTourCheckIn.vue'
import Login from './pages/Login.vue'
import Forbidden from './pages/Forbidden.vue'
import TourPortal from './pages/portal/TourPortal.vue'
import type { UserRole } from './types'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/admin/tours' },
    { path: '/login', component: Login },
    { path: '/forbidden', component: Forbidden },
    {
      path: '/admin/tours',
      component: AdminTours,
      meta: { requiresAuth: true, roles: ['Admin'] },
    },
    {
      path: '/admin/tours/:tourId',
      component: AdminTourDetail,
      props: true,
      meta: { requiresAuth: true, roles: ['Admin'] },
    },
    {
      path: '/admin/tours/:tourId/checkin',
      component: AdminTourCheckIn,
      props: true,
      meta: { requiresAuth: true, roles: ['Admin'] },
    },
    {
      path: '/guide/tours',
      component: GuideTours,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    {
      path: '/guide/tours/:tourId/checkin',
      component: GuideTourCheckIn,
      props: true,
      meta: { requiresAuth: true, roles: ['Guide'] },
    },
    { path: '/t/:tourId', component: TourPortal, props: true },
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

  return true
})

export default router
