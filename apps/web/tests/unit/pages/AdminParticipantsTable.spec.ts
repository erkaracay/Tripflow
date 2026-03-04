import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { i18n } from '../../../src/i18n'
import AdminParticipantsTable from '../../../src/pages/admin/AdminParticipantsTable.vue'

vi.mock('../../../src/lib/api', () => ({
  apiGet: vi.fn(),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn() })),
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/admin/events/:eventId/participants', component: AdminParticipantsTable },
    { path: '/:pathMatch(.*)*', component: { template: '<div />' } },
  ],
})

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

describe('AdminParticipantsTable', () => {
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
      return {
        page: 1,
        pageSize: 50,
        total: 0,
        items: [],
      }
    })
  })

  afterEach(() => {
    window.matchMedia = originalMatchMedia
  })

  it('renders compact combobox filters for status and page size', async () => {
    await router.push({ path: '/admin/events/ev-1/participants' })
    const wrapper = mount(AdminParticipantsTable, {
      global: {
        plugins: [i18n, router],
        stubs: {
          LoadingState: true,
          ErrorState: true,
          ParticipantFlightsModal: true,
          FlightPanelHelperModal: true,
        },
      },
    })
    await flushPromises()

    expect(wrapper.findAll('.app-combobox-trigger-compact').length).toBeGreaterThanOrEqual(2)
  })
})
