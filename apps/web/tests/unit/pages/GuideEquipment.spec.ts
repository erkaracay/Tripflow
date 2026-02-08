import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { i18n } from '../../../src/i18n'
import GuideEquipment from '../../../src/pages/guide/GuideEquipment.vue'

vi.mock('../../../src/lib/api', () => ({
  apiGet: vi.fn(),
  apiPostWithPayload: vi.fn(),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn(), removeToast: vi.fn() })),
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [{ path: '/guide/events/:eventId/equipment', component: GuideEquipment }],
})

describe('GuideEquipment', () => {
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
          id: 'item-1',
          type: 'Equipment',
          title: 'Item 1',
          name: 'Item 1',
          isActive: true,
          sortOrder: 0,
        },
      ])
  })

  it('mounts and renders when API returns event and items', async () => {
    await router.push({ path: '/guide/events/ev-1/equipment' })
    const wrapper = mount(GuideEquipment, {
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
