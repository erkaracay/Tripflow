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

const createAccommodationDocs = (): PortalDocsResponse => ({
  tabs: [
    {
      id: 'hotel-2',
      title: 'Konaklama 2',
      type: 'Hotel',
      sortOrder: 2,
      content: {
        hotelName: 'B Hotel',
        address: 'B Address',
      },
    },
    {
      id: 'hotel-1',
      title: 'Konaklama',
      type: 'Hotel',
      sortOrder: 1,
      content: {
        hotelName: 'A Hotel',
        address: 'A Address',
      },
    },
  ],
  participantTravel: {
    arrival: {
      airline: 'AJET',
    },
    return: {
      airline: 'AJET',
    },
    roomNo: '101',
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

  it('renders multiple accommodation cards in sort order', async () => {
    const wrapper = mount(PortalInfoTabs, {
      props: {
        docs: createAccommodationDocs(),
      },
      global: {
        plugins: [i18n],
      },
    })

    await flushPromises()

    await wrapper
      .findAll('button')
      .find((button) => button.text().includes('Konaklama'))
      ?.trigger('click')
    await flushPromises()

    const text = wrapper.text()
    expect(text).toContain('A Hotel')
    expect(text).toContain('B Hotel')
    expect(text.indexOf('A Hotel')).toBeLessThan(text.indexOf('B Hotel'))
    expect(text).toContain('1. Konaklama')
    expect(text).toContain('2. Konaklama 2')
  })

  it('renders multiple accommodation cards in print mode', async () => {
    const wrapper = mount(PortalInfoTabs, {
      props: {
        docs: createAccommodationDocs(),
        printMode: true,
      },
      global: {
        plugins: [i18n],
      },
    })

    await flushPromises()
    const text = wrapper.text()

    expect(text).toContain('A Hotel')
    expect(text).toContain('B Hotel')
    expect(text).toContain('1. Konaklama')
    expect(text).toContain('2. Konaklama 2')
  })
})
