import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { i18n } from '../../../src/i18n'
import AdminParticipantRoomOps from '../../../src/pages/admin/AdminParticipantRoomOps.vue'

vi.mock('../../../src/lib/api', () => ({
  apiGet: vi.fn(),
  getAccommodationSegments: vi.fn(),
  getAccommodationSegmentParticipants: vi.fn(),
  bulkApplyAccommodationSegmentParticipants: vi.fn(),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn() })),
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/admin/events/:eventId/participants/rooms', component: AdminParticipantRoomOps },
    { path: '/:pathMatch(.*)*', component: { template: '<div />' } },
  ],
})

describe('AdminParticipantRoomOps', () => {
  beforeEach(async () => {
    const {
      apiGet,
      getAccommodationSegments,
      getAccommodationSegmentParticipants,
      bulkApplyAccommodationSegmentParticipants,
    } = await import('../../../src/lib/api')
    vi.mocked(apiGet).mockReset()
    vi.mocked(getAccommodationSegments).mockReset()
    vi.mocked(getAccommodationSegmentParticipants).mockReset()
    vi.mocked(bulkApplyAccommodationSegmentParticipants).mockReset()

    vi.mocked(apiGet).mockImplementation(async (url: string) => {
      if (url === '/api/events/ev-1') {
        return {
          id: 'ev-1',
          name: 'Test Event',
          startDate: '2026-03-01',
          endDate: '2026-03-03',
          guideUserIds: [],
          isDeleted: false,
        }
      }
      if (url === '/api/events/ev-1/docs/tabs') {
        return [
          {
            id: 'hotel-1',
            eventId: 'ev-1',
            title: 'Konaklama 1',
            type: 'Hotel',
            sortOrder: 1,
            isActive: true,
            content: {},
          },
        ]
      }
      throw new Error(`Unexpected url: ${url}`)
    })

    vi.mocked(getAccommodationSegments).mockResolvedValue([
      {
        id: 'segment-1',
        defaultAccommodationDocTabId: 'hotel-1',
        defaultAccommodationTitle: 'Konaklama 1',
        startDate: '2026-03-01',
        endDate: '2026-03-03',
        sortOrder: 1,
      },
    ])

    vi.mocked(getAccommodationSegmentParticipants).mockResolvedValue({
      page: 1,
      pageSize: 100,
      total: 1,
      items: [
        {
          participantId: 'p-1',
          fullName: 'Ayse Demir',
          tcNo: '10000000001',
          effectiveAccommodationDocTabId: 'hotel-1',
          effectiveAccommodationTitle: 'Konaklama 1',
          usesOverride: false,
          roomNo: '101',
          roomType: 'Twin',
          boardType: 'BB',
          personNo: '1',
          warnings: [],
        },
      ],
    })

    vi.mocked(bulkApplyAccommodationSegmentParticipants).mockResolvedValue({
      affectedCount: 1,
      createdCount: 0,
      updatedCount: 1,
      deletedCount: 0,
      unchangedCount: 0,
      errors: [],
    })
  })

  it('builds rowUpdates payload from local draft edits', async () => {
    await router.push('/admin/events/ev-1/participants/rooms')

    const wrapper = mount(AdminParticipantRoomOps, {
      global: {
        plugins: [i18n, router],
        stubs: {
          LoadingState: true,
          ErrorState: true,
        },
      },
    })

    await flushPromises()

    const roomNoInputs = wrapper.findAll('tbody input[type="text"]')
    await roomNoInputs[0]!.setValue('202')
    await flushPromises()

    const applyButton = wrapper
      .findAll('button')
      .find((button) =>
        button.text().includes('Taslak değişiklikleri uygula')
        || button.text().includes('Apply draft changes'))

    expect(applyButton).toBeDefined()
    await applyButton!.trigger('click')
    await flushPromises()

    const { bulkApplyAccommodationSegmentParticipants } = await import('../../../src/lib/api')
    expect(vi.mocked(bulkApplyAccommodationSegmentParticipants)).toHaveBeenCalledTimes(1)
    expect(vi.mocked(bulkApplyAccommodationSegmentParticipants).mock.calls[0]?.[0]).toBe('ev-1')
    expect(vi.mocked(bulkApplyAccommodationSegmentParticipants).mock.calls[0]?.[1]).toBe('segment-1')
    expect(vi.mocked(bulkApplyAccommodationSegmentParticipants).mock.calls[0]?.[2]).toMatchObject({
      rowUpdates: [
        {
          participantId: 'p-1',
          accommodationMode: 'default',
          overrideAccommodationDocTabId: null,
          roomNo: '202',
          roomType: 'Twin',
          boardType: 'BB',
          personNo: '1',
        },
      ],
    })
  })
})
