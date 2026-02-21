import { describe, it, expect, beforeEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import PortalInfoTabs from '../../../src/components/portal/PortalInfoTabs.vue'
import { i18n, setLocale } from '../../../src/i18n'
import type { PortalDocsResponse } from '../../../src/types'

const createDocs = (): PortalDocsResponse => ({
  tabs: [],
  participantTravel: {
    arrival: {
      airline: 'AJET',
    },
    return: {
      airline: 'AJET',
    },
  },
})

const countOccurrences = (value: string, token: string) => value.split(token).length - 1

describe('PortalInfoTabs', () => {
  beforeEach(() => {
    setLocale('tr')
  })

  it('shows full name row in both outbound and return sections', async () => {
    const wrapper = mount(PortalInfoTabs, {
      props: {
        docs: createDocs(),
        participantName: 'Ayse Yilmaz',
      },
      global: {
        plugins: [i18n],
      },
    })

    await flushPromises()
    const text = wrapper.text()

    expect(countOccurrences(text, 'Ad soyad')).toBe(2)
    expect(countOccurrences(text, 'Ayse Yilmaz')).toBe(2)
  })

  it('hides full name row when participant name is empty', async () => {
    const wrapper = mount(PortalInfoTabs, {
      props: {
        docs: createDocs(),
        participantName: '   ',
      },
      global: {
        plugins: [i18n],
      },
    })

    await flushPromises()

    expect(wrapper.text()).not.toContain('Ad soyad')
  })

  it('shows full name row in print mode as well', async () => {
    const wrapper = mount(PortalInfoTabs, {
      props: {
        docs: createDocs(),
        participantName: 'Ayse Yilmaz',
        printMode: true,
      },
      global: {
        plugins: [i18n],
      },
    })

    await flushPromises()
    const text = wrapper.text()

    expect(countOccurrences(text, 'Ad soyad')).toBe(2)
    expect(countOccurrences(text, 'Ayse Yilmaz')).toBe(2)
  })
})
