import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
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
  apiPatchWithPayload: vi.fn(),
  apiPost: vi.fn(),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn(), removeToast: vi.fn() })),
}))

const originalMatchMedia = window.matchMedia

const mockMatchMedia = () => {
  window.matchMedia = vi.fn().mockImplementation(() => ({
    matches: false,
    media: '(max-width: 767px)',
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    addListener: vi.fn(),
    removeListener: vi.fn(),
    onchange: null,
    dispatchEvent: vi.fn(),
  })) as typeof window.matchMedia
}

describe('AdminActivityCheckIn', () => {
  beforeEach(async () => {
    mockMatchMedia()
    const { apiGet } = await import('../../../src/lib/api')
    vi.mocked(apiGet).mockReset()
    vi.mocked(apiGet).mockImplementation(async (url: string) => {
      if (url === '/api/events/ev-1') {
        return {
          id: 'ev-1',
          name: 'Test Event',
          startDate: '2026-02-08',
          endDate: '2026-02-09',
          guideUserIds: [],
          isDeleted: false,
        }
      }
      if (url === '/api/events/ev-1/days') {
        return [
          {
            id: 'day-1',
            date: '2026-02-08',
            title: 'Day 1',
            sortOrder: 1,
            isActive: true,
            activityCount: 1,
          },
        ]
      }
      if (url === '/api/events/ev-1/activities/for-checkin') {
        return [
          {
            id: 'act-1',
            eventDayId: 'day-1',
            title: 'Activity 1',
            type: 'Session',
            startTime: '09:00:00',
            checkInEnabled: true,
            requiresCheckIn: true,
            checkInMode: 'QR',
          },
        ]
      }
      if (url.includes('/participants/table')) {
        return {
          page: 1,
          pageSize: 50,
          total: 0,
          items: [],
        }
      }
      return {
        id: 'ev-1',
      }
    })
  })

  afterEach(() => {
    window.matchMedia = originalMatchMedia
  })

  it('renders grouped activity options in the combobox', async () => {
    await router.push({ path: '/admin/events/ev-1/activities/checkin' })
    const wrapper = mount(AdminActivityCheckIn, {
      global: {
        plugins: [i18n, router],
        stubs: {
          QrScannerModal: true,
          LoadingState: true,
          ErrorState: true,
          ConfirmDialog: true,
        },
      },
    })
    await flushPromises()

    await wrapper.get('.app-combobox-trigger').trigger('click')

    expect(wrapper.text()).toContain('Day 1')
    expect(wrapper.text()).toContain('09:00 - Activity 1')
  })
})
