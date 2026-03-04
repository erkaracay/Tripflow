import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { i18n } from '../../../src/i18n'
import AdminUsers from '../../../src/pages/admin/AdminUsers.vue'

vi.mock('../../../src/lib/api', () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPostWithPayload: vi.fn(),
}))

vi.mock('../../../src/lib/auth', () => ({
  getSelectedOrgId: vi.fn(() => 'org-1'),
}))

vi.mock('../../../src/lib/toast', () => ({
  useToast: vi.fn(() => ({ pushToast: vi.fn() })),
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

describe('AdminUsers', () => {
  beforeEach(async () => {
    mockMatchMedia()
    const { apiGet } = await import('../../../src/lib/api')
    vi.mocked(apiGet).mockReset()
    vi.mocked(apiGet).mockResolvedValueOnce([])
  })

  afterEach(() => {
    window.matchMedia = originalMatchMedia
  })

  it('renders the role selector as a compact combobox', async () => {
    const wrapper = mount(AdminUsers, {
      global: {
        plugins: [i18n],
        stubs: {
          LoadingState: true,
          ErrorState: true,
          PasswordModal: true,
        },
      },
    })
    await flushPromises()

    expect(wrapper.find('.app-combobox-trigger-compact').exists()).toBe(true)
  })
})
