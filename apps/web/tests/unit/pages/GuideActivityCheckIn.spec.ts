import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { i18n } from '../../../src/i18n'
import GuideActivityCheckIn from '../../../src/pages/guide/GuideActivityCheckIn.vue'

vi.mock('../../../src/lib/api', () => ({
  apiGet: vi.fn(),
  apiPostWithPayload: vi.fn(),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn(), removeToast: vi.fn() })),
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/guide/events/:eventId/activities/checkin', component: GuideActivityCheckIn },
    { path: '/:pathMatch(.*)*', component: { template: '<div />' } },
  ],
})

describe('GuideActivityCheckIn', () => {
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
    await router.push({ path: '/guide/events/ev-1/activities/checkin' })
    const wrapper = mount(GuideActivityCheckIn, {
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
