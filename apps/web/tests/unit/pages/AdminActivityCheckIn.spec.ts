import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { i18n } from '../../../src/i18n'
import AdminActivityCheckIn from '../../../src/pages/admin/AdminActivityCheckIn.vue'

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/admin/events/:eventId/activities/checkin', name: 'AdminActivityCheckIn', component: AdminActivityCheckIn },
    { path: '/:pathMatch(.*)*', component: { template: '<div />' } },
  ],
})

vi.mock('../../../src/lib/api', () => ({
  apiGet: vi.fn(),
  apiPostWithPayload: vi.fn(),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn(), removeToast: vi.fn() })),
}))

describe('AdminActivityCheckIn', () => {
  beforeEach(async () => {
    const { apiGet } = await import('../../../src/lib/api')
    vi.mocked(apiGet).mockReset()
    vi.mocked(apiGet)
      .mockResolvedValueOnce({
        id: 'ev-1',
        name: 'Test Event',
        startDate: '2026-02-08',
        endDate: '2026-02-09',
        isDeleted: false,
      })
      .mockResolvedValueOnce([
        {
          id: 'act-1',
          eventDayId: 'day-1',
          title: 'Activity 1',
          type: 'Session',
          checkInEnabled: true,
          requiresCheckIn: true,
          checkInMode: 'QR',
        },
      ])
  })

  it('mounts and renders when API returns event and activities', async () => {
    await router.push({ path: '/admin/events/ev-1/activities/checkin' })
    const wrapper = mount(AdminActivityCheckIn, {
      global: {
        plugins: [i18n, router],
        stubs: {
          QrScannerModal: true,
          LoadingState: true,
          ErrorState: true,
        },
      },
    })
    await flushPromises()
    expect(wrapper.exists()).toBe(true)
  })
})
