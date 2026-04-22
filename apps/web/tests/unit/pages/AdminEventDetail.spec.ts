import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent } from 'vue'
import { i18n } from '../../../src/i18n'
import AdminEventDetail from '../../../src/pages/admin/AdminEventDetail.vue'
import type { Event, EventPortalInfo, Participant, UserListItem } from '../../../src/types'

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

const guidesFixture: UserListItem[] = [
  { id: 'guide-1', email: 'ayse@example.com', fullName: 'Ayse Demir', role: 'Guide' },
  { id: 'guide-2', email: 'mert@example.com', fullName: 'Mert Kaya', role: 'Guide' },
]

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

    const ConfirmDialogStub = defineComponent({
      props: {
        open: { type: Boolean, required: true },
      },
      emits: ['confirm', 'cancel', 'update:open'],
      template: `
        <div v-if="open" data-test="confirm-dialog">
          <button
            class="confirm-dialog-submit"
            type="button"
            @click="$emit('update:open', false); $emit('confirm')"
          >
            confirm
          </button>
        </div>
      `,
    })

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
          AppMultiCombobox: true,
          ConfirmDialog: ConfirmDialogStub,
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

    const confirmButton = wrapper.find('.confirm-dialog-submit')
    expect(confirmButton.exists()).toBe(true)
    await confirmButton.trigger('click')
    await flushPromises()

    expect(apiDelete).toHaveBeenCalledWith(`/api/dev/scenario-events/${eventFixture.id}`)
    expect(pushToast).toHaveBeenCalledWith({ key: 'toast.scenarioEventDeleted', tone: 'success' })
    expect(router.currentRoute.value.fullPath).toBe('/admin/events')
  })

  it('updates guide selections locally and persists them with the existing save action', async () => {
    const { apiGet, apiPut } = await import('../../../src/lib/api')
    vi.mocked(apiGet)
      .mockResolvedValueOnce({ ...eventFixture, guideUserIds: ['guide-1'] })
      .mockResolvedValueOnce([] satisfies Participant[])
      .mockResolvedValueOnce(portalFixture)
      .mockResolvedValueOnce(guidesFixture)
    vi.mocked(apiPut).mockResolvedValue(undefined)

    const AppMultiComboboxStub = defineComponent({
      props: {
        modelValue: {
          type: Array,
          required: true,
        },
      },
      emits: ['update:modelValue'],
      template: `
        <div>
          <span v-for="value in modelValue" :key="String(value)" class="guide-chip">{{ value }}</span>
          <button class="guide-select" type="button" @click="$emit('update:modelValue', ['guide-1', 'guide-2'])">
            select
          </button>
        </div>
      `,
    })

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
          AppMultiCombobox: AppMultiComboboxStub,
          ConfirmDialog: true,
          WhatsAppIcon: true,
          RichTextEditor: true,
        },
      },
    })

    await flushPromises()

    const saveButton = wrapper
      .findAll('button')
      .find((button) => button.text().includes('Save guides'))

    expect(saveButton).toBeDefined()
    expect(saveButton!.attributes('disabled')).toBeDefined()
    expect(wrapper.findAll('.guide-chip')).toHaveLength(1)

    await wrapper.get('.guide-select').trigger('click')
    await flushPromises()

    expect(wrapper.findAll('.guide-chip')).toHaveLength(2)
    expect(saveButton!.attributes('disabled')).toBeUndefined()

    await saveButton!.trigger('click')
    await flushPromises()

    expect(apiPut).toHaveBeenCalledWith(`/api/events/${eventFixture.id}/guides`, {
      guideUserIds: ['guide-1', 'guide-2'],
    })
    expect(pushToast).toHaveBeenCalledWith({ key: 'toast.guidesAssigned', tone: 'success' })
    expect(saveButton!.attributes('disabled')).toBeDefined()
  })
})
