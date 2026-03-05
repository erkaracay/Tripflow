import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { i18n } from '../../../src/i18n'
import AdminEventDocsTabs from '../../../src/pages/admin/AdminEventDocsTabs.vue'
import type { Event, EventDocTabDto } from '../../../src/types'

vi.mock('../../../src/lib/api', () => ({
  apiDelete: vi.fn(),
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn() })),
}))

const createRouterForTest = () =>
  createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin/events/:eventId/docs', component: AdminEventDocsTabs },
      { path: '/:pathMatch(.*)*', component: { template: '<div />' } },
    ],
  })

const eventFixture: Event = {
  id: 'event-1',
  name: 'Test Event',
  startDate: '2026-03-03',
  endDate: '2026-03-05',
  timeZoneId: 'Europe/Istanbul',
  guideUserIds: [],
  eventAccessCode: 'ABC12345',
  isDeleted: false,
}

const tabsFixture: EventDocTabDto[] = [
  {
    id: 'hotel-1',
    eventId: 'event-1',
    title: 'Hotel',
    type: 'Hotel',
    sortOrder: 1,
    isActive: true,
    content: {},
  },
  {
    id: 'insurance-1',
    eventId: 'event-1',
    title: 'Insurance',
    type: 'Insurance',
    sortOrder: 2,
    isActive: false,
    content: {},
  },
]

describe('AdminEventDocsTabs', () => {
  beforeEach(async () => {
    vi.clearAllMocks()
    const { apiGet } = await import('../../../src/lib/api')
    vi.mocked(apiGet).mockReset()
  })

  it('keeps insurance locked but allows accommodation active toggle', async () => {
    const { apiGet } = await import('../../../src/lib/api')
    vi.mocked(apiGet)
      .mockResolvedValueOnce(eventFixture)
      .mockResolvedValueOnce(tabsFixture)

    const router = createRouterForTest()
    await router.push('/admin/events/event-1/docs')

    const wrapper = mount(AdminEventDocsTabs, {
      global: {
        plugins: [i18n, router],
        stubs: {
          AppCombobox: true,
          AppSegmentedControl: true,
          EventDocPreviewDrawer: true,
          LoadingState: true,
          ErrorState: true,
          AppModalShell: {
            props: ['open'],
            template: '<div><slot v-if="open" name="default" :panelClass="\'\'" /></div>',
          },
          RouterLink: true,
        },
      },
    })

    await flushPromises()

    const rowBlocks = wrapper.findAll('.divide-y > div')
    const accommodationRow = rowBlocks.find((row) => {
      const text = row.text()
      return text.includes('Hotel') || text.includes('Konaklama') || text.includes('Accommodation')
    })
    const insuranceRow = rowBlocks.find((row) => {
      const text = row.text()
      return text.includes('Insurance') || text.includes('Sigorta')
    })

    expect(accommodationRow).toBeDefined()
    expect(insuranceRow).toBeDefined()
    expect(accommodationRow!.text()).toMatch(/Sil|Delete/)
    expect(insuranceRow!.text()).not.toMatch(/Sil|Delete/)
    expect(insuranceRow!.text()).toMatch(/Sistem sekmesi|System tab/)
  })

  it('starts create form sort order with max + 1', async () => {
    const { apiGet } = await import('../../../src/lib/api')
    vi.mocked(apiGet)
      .mockResolvedValueOnce(eventFixture)
      .mockResolvedValueOnce([
        { ...tabsFixture[0], sortOrder: 4 },
        { ...tabsFixture[1], sortOrder: 7 },
      ])

    const router = createRouterForTest()
    await router.push('/admin/events/event-1/docs')

    const wrapper = mount(AdminEventDocsTabs, {
      global: {
        plugins: [i18n, router],
        stubs: {
          AppSegmentedControl: true,
          EventDocPreviewDrawer: true,
          LoadingState: true,
          ErrorState: true,
          AppModalShell: {
            props: ['open'],
            template: '<div><slot v-if="open" name="default" :panelClass="\'\'" /></div>',
          },
          RouterLink: true,
        },
      },
    })

    await flushPromises()

    const createButton = wrapper
      .findAll('button')
      .find((button) => {
        const text = button.text()
        return text.includes('Konaklama ekle') || text.includes('Add accommodation')
      })
    expect(createButton).toBeDefined()
    await createButton!.trigger('click')
    await flushPromises()

    const sortInput = wrapper.get('input[type="number"]')
    expect((sortInput.element as HTMLInputElement).value).toBe('8')
  })
})
