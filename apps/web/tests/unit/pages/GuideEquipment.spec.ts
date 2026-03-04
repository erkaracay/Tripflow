import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { i18n } from '../../../src/i18n'
import GuideEquipment from '../../../src/pages/guide/GuideEquipment.vue'

vi.mock('../../../src/lib/api', () => ({
  apiGet: vi.fn(),
  apiPostWithPayload: vi.fn(),
  apiPut: vi.fn(),
  apiDelete: vi.fn(),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn(), removeToast: vi.fn() })),
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/guide/events/:eventId/equipment', component: GuideEquipment },
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

describe('GuideEquipment', () => {
  beforeEach(async () => {
    mockMatchMedia()
    const { apiGet } = await import('../../../src/lib/api')
    vi.mocked(apiGet).mockReset()
    vi.mocked(apiGet)
      .mockResolvedValueOnce({
        id: 'ev-1',
        name: 'Test Event',
        startDate: '2026-02-08',
        endDate: '2026-02-09',
        guideUserIds: [],
        isDeleted: false,
      })
      .mockResolvedValueOnce([
        {
          id: 'item-1',
          type: 'Headset',
          title: 'Headset',
          name: 'Headset',
          isActive: true,
          sortOrder: 0,
        },
      ])
      .mockResolvedValueOnce({
        page: 1,
        pageSize: 50,
        total: 0,
        items: [],
      })
  })

  afterEach(() => {
    window.matchMedia = originalMatchMedia
  })

  it('renders custom comboboxes for item and type selection', async () => {
    await router.push({ path: '/guide/events/ev-1/equipment' })
    const wrapper = mount(GuideEquipment, {
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

    expect(wrapper.findAll('.app-combobox-trigger').length).toBeGreaterThan(0)
  })
})
