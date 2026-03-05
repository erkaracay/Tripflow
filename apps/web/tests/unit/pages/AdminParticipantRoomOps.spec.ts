import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { i18n } from '../../../src/i18n'
import AdminParticipantRoomOps from '../../../src/pages/admin/AdminParticipantRoomOps.vue'

vi.mock('../../../src/lib/api', () => ({
  apiGet: vi.fn(),
  bulkApplyParticipantRooms: vi.fn(),
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
    const { apiGet, bulkApplyParticipantRooms } = await import('../../../src/lib/api')
    vi.mocked(apiGet).mockReset()
    vi.mocked(bulkApplyParticipantRooms).mockReset()

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
      if (url.startsWith('/api/events/ev-1/participants/table')) {
        return {
          page: 1,
          pageSize: 200,
          total: 1,
          items: [
            {
              id: 'p-1',
              firstName: 'Ayse',
              lastName: 'Demir',
              fullName: 'Ayse Demir',
              phone: '555',
              email: 'a@a.com',
              tcNo: '10000000001',
              birthDate: '1990-01-01',
              gender: 'Female',
              checkInCode: 'ABC12345',
              arrived: false,
              details: {
                roomNo: '101',
                roomType: 'Twin',
                boardType: 'BB',
                personNo: '1',
                accommodationDocTabId: 'hotel-1',
              },
            },
          ],
        }
      }
      throw new Error(`Unexpected url: ${url}`)
    })

    vi.mocked(bulkApplyParticipantRooms).mockResolvedValue({
      affectedCount: 1,
      updatedCount: 1,
      skippedCount: 0,
      notFoundTcNoCount: 0,
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

    const { bulkApplyParticipantRooms } = await import('../../../src/lib/api')
    expect(vi.mocked(bulkApplyParticipantRooms)).toHaveBeenCalledTimes(1)
    expect(vi.mocked(bulkApplyParticipantRooms).mock.calls[0]?.[0]).toBe('ev-1')
    expect(vi.mocked(bulkApplyParticipantRooms).mock.calls[0]?.[1]).toMatchObject({
      overwriteMode: 'always',
      rowUpdates: [
        {
          participantId: 'p-1',
          tcNo: '10000000001',
          patch: {
            roomNo: '202',
          },
        },
      ],
    })
  })
})
