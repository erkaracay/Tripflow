import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import RichTextContent from '../../../src/components/editor/RichTextContent.vue'

describe('RichTextContent', () => {
  it('preserves allowed markup', () => {
    const wrapper = mount(RichTextContent, {
      props: {
        content: '<p>Hello <strong>Tripflow</strong></p>',
      },
    })

    const html = wrapper.html()

    expect(html).toContain('<p>Hello <strong>Tripflow</strong></p>')
    expect(wrapper.find('.rich-text-content').exists()).toBe(true)
  })

  it('sanitizes disallowed tags and attributes', () => {
    const wrapper = mount(RichTextContent, {
      props: {
        content: '<p onclick="alert(1)">Safe</p><a href="javascript:alert(1)">Link</a>',
      },
    })

    const html = wrapper.html()

    expect(wrapper.text()).toContain('Safe')
    expect(wrapper.text()).toContain('Link')
    expect(html).not.toContain('onclick')
    expect(html).not.toContain('javascript:')
  })

  it('renders plain text without switching to html mode', () => {
    const wrapper = mount(RichTextContent, {
      props: {
        content: 'Hello\nTripflow',
      },
    })

    expect(wrapper.find('.rich-text-content').exists()).toBe(false)
    expect(wrapper.element.textContent).toBe('Hello\nTripflow')
  })
})
