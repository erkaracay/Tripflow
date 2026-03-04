import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { i18n } from '../../../src/i18n'
import AdminEventDetail from '../../../src/pages/admin/AdminEventDetail.vue'
import type { Event, EventPortalInfo, Participant } from '../../../src/types'

const pushToast = vi.fn()

vi.mock('../../../src/lib/api', () => ({
  apiDelete: vi.fn(),
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPostWithHeaders: vi.fn(),
  apiPut: vi.fn(),
  apiPutWithHeaders: vi.fn(),
  buildUrl: vi.fn((path: string) => path),
}))

vi.mock('../../../src/lib/auth', () => ({
  getAuthRole: vi.fn(() => 'AgencyAdmin'),
  getSelectedOrgId: vi.fn(() => null),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast, removeToast: vi.fn() })),
}))

const eventFixture: Event = {
  id: 'dev-event-1',
  name: '[DEV] Flight Heavy Demo',
  startDate: '2026-03-10',
  endDate: '2026-03-12',
  timeZoneId: 'Europe/Istanbul',
  guideUserIds: [],
  eventAccessCode: 'ABC12345',
  isDeleted: false,
}

const portalFixture: EventPortalInfo = {
  meeting: {
    time: '09:00',
    place: 'Lobby',
    mapsUrl: 'https://maps.example.com',
    note: 'Meet here',
  },
  links: [{ label: 'WhatsApp', url: 'https://example.com' }],
  days: [{ day: 1, title: 'Day 1', items: ['Arrival'] }],
  notes: ['Bring your badge'],
  eventContacts: null,
}

const createTestRouter = () =>
  createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin/events', component: { template: '<div />' } },
      { path: '/admin/events/:eventId', component: AdminEventDetail, props: true },
      { path: '/:pathMatch(.*)*', component: { template: '<div />' } },
    ],
  })

describe('AdminEventDetail', () => {
  beforeEach(async () => {
    vi.clearAllMocks()
    vi.stubGlobal('confirm', vi.fn(() => true))

    const { apiGet, apiDelete, apiPost, apiPostWithHeaders, apiPut, apiPutWithHeaders } = await import('../../../src/lib/api')
    vi.mocked(apiGet).mockReset()
    vi.mocked(apiDelete).mockReset()
    vi.mocked(apiPost).mockReset()
    vi.mocked(apiPostWithHeaders).mockReset()
    vi.mocked(apiPut).mockReset()
    vi.mocked(apiPutWithHeaders).mockReset()
  })

  it('shows cleanup action for generated dev events and redirects after deletion', async () => {
    const { apiGet, apiDelete } = await import('../../../src/lib/api')
    vi.mocked(apiGet)
      .mockResolvedValueOnce(eventFixture)
      .mockResolvedValueOnce([] satisfies Participant[])
      .mockResolvedValueOnce(portalFixture)
      .mockResolvedValueOnce([])
    vi.mocked(apiDelete).mockResolvedValue({ eventId: eventFixture.id, deleted: true })

    const router = createTestRouter()
    await router.push(`/admin/events/${eventFixture.id}`)

    const wrapper = mount(AdminEventDetail, {
      props: { eventId: eventFixture.id },
      global: {
        plugins: [i18n, router],
        stubs: {
          Transition: false,
          LoadingState: true,
          ErrorState: true,
          ParticipantFlightsModal: true,
          AppModalShell: {
            template: '<div><slot /><slot name="default" :panelClass="\'\'" /></div>',
          },
          AppCombobox: true,
          ConfirmDialog: true,
          WhatsAppIcon: true,
          RichTextEditor: true,
        },
      },
    })

    await flushPromises()

    expect(wrapper.text()).toContain('Delete dev event')

    const deleteButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Delete dev event'))

    expect(deleteButton).toBeDefined()
    await deleteButton!.trigger('click')
    await flushPromises()

    expect(apiDelete).toHaveBeenCalledWith(`/api/dev/scenario-events/${eventFixture.id}`)
    expect(pushToast).toHaveBeenCalledWith({ key: 'toast.scenarioEventDeleted', tone: 'success' })
    expect(router.currentRoute.value.fullPath).toBe('/admin/events')
  })
})
