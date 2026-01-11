import { createRouter, createWebHistory } from 'vue-router'
import AdminTours from './pages/admin/AdminTours.vue'
import AdminTourDetail from './pages/admin/AdminTourDetail.vue'
import AdminTourCheckIn from './pages/admin/AdminTourCheckIn.vue'
import TourPortal from './pages/portal/TourPortal.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/admin/tours' },
    { path: '/admin/tours', component: AdminTours },
    { path: '/admin/tours/:tourId', component: AdminTourDetail, props: true },
    { path: '/admin/tours/:tourId/checkin', component: AdminTourCheckIn, props: true },
    { path: '/t/:tourId', component: TourPortal, props: true },
  ],
})

export default router
