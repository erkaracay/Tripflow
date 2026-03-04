import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { i18n } from '../../../src/i18n'
import AdminEvents from '../../../src/pages/admin/AdminEvents.vue'

vi.mock('../../../src/lib/api', () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
}))

vi.mock('../../../src/lib/auth', () => ({
  getAuthRole: vi.fn(() => 'AgencyAdmin'),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn(), removeToast: vi.fn() })),
}))

const createTestRouter = () =>
  createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin/events', component: AdminEvents },
      { path: '/admin/events/:eventId', component: { template: '<div />' } },
      { path: '/:pathMatch(.*)*', component: { template: '<div />' } },
    ],
  })

describe('AdminEvents', () => {
  beforeEach(async () => {
    const { apiGet, apiPost } = await import('../../../src/lib/api')
    vi.mocked(apiGet).mockReset()
    vi.mocked(apiPost).mockReset()
  })

  it('shows the dev scenario panel when capabilities are available', async () => {
    const { apiGet } = await import('../../../src/lib/api')
    vi.mocked(apiGet)
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce({
        generalSeed: true,
        scenarioEventGenerator: true,
        presets: [],
      })

    const router = createTestRouter()
    await router.push('/admin/events')

    const wrapper = mount(AdminEvents, {
      global: {
        plugins: [i18n, router],
        stubs: {
          LoadingState: true,
          ErrorState: true,
          AppCombobox: true,
          DevScenarioGeneratorPanel: {
            template: '<div data-testid="scenario-panel">Scenario panel</div>',
          },
        },
      },
    })

    await flushPromises()

    expect(wrapper.find('[data-testid="scenario-panel"]').exists()).toBe(true)
  })

  it('hides the dev scenario panel when capability lookup fails', async () => {
    const { apiGet } = await import('../../../src/lib/api')
    vi.mocked(apiGet)
      .mockResolvedValueOnce([])
      .mockRejectedValueOnce(Object.assign(new Error('Not found'), { status: 404 }))

    const router = createTestRouter()
    await router.push('/admin/events')

    const wrapper = mount(AdminEvents, {
      global: {
        plugins: [i18n, router],
        stubs: {
          LoadingState: true,
          ErrorState: true,
          AppCombobox: true,
          DevScenarioGeneratorPanel: {
            template: '<div data-testid="scenario-panel">Scenario panel</div>',
          },
        },
      },
    })

    await flushPromises()

    expect(wrapper.find('[data-testid="scenario-panel"]').exists()).toBe(false)
  })
})
