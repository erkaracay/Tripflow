import { afterEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { h, nextTick } from 'vue'
import AppDrawerShell from '../../../src/components/ui/AppDrawerShell.vue'

const createPointerEvent = (type: string, clientY: number, pointerId = 1, pointerType = 'touch') => {
  const event = new Event(type, { bubbles: true, cancelable: true }) as PointerEvent
  Object.defineProperties(event, {
    clientY: { value: clientY },
    pointerId: { value: pointerId },
    pointerType: { value: pointerType },
  })
  return event
}

const mountDrawer = (props: Record<string, unknown>) =>
  mount(AppDrawerShell, {
    props: {
      open: true,
      ...props,
    },
    attachTo: document.body,
    slots: {
      default: ({ panelClass, labelledBy }: { panelClass: string[]; labelledBy: string }) =>
        h(
          'section',
          { class: panelClass, 'aria-labelledby': labelledBy },
          [h('div', { class: 'swipe-handle', 'data-drawer-swipe-handle': '' }), h('div', 'Body')]
        ),
    },
  })

describe('AppDrawerShell', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('does not close from swipe when swipeToClose is disabled', async () => {
    const wrapper = mountDrawer({ swipeToClose: false })

    const handle = document.querySelector('.swipe-handle')
    expect(handle).toBeTruthy()

    handle!.dispatchEvent(createPointerEvent('pointerdown', 0))
    window.dispatchEvent(createPointerEvent('pointermove', 120))
    window.dispatchEvent(createPointerEvent('pointerup', 120))
    await nextTick()

    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('does not start gesture when pointerdown is outside handle selector', async () => {
    const wrapper = mountDrawer({
      swipeToClose: true,
      swipeVelocityPxMs: 999,
    })

    const panel = document.querySelector('.app-drawer-panel')
    expect(panel).toBeTruthy()

    panel!.dispatchEvent(createPointerEvent('pointerdown', 0))
    window.dispatchEvent(createPointerEvent('pointermove', 120))
    window.dispatchEvent(createPointerEvent('pointerup', 120))
    await nextTick()

    expect(wrapper.emitted('close')).toBeUndefined()
  })

  it('emits close when swipe passes threshold', async () => {
    const wrapper = mountDrawer({
      swipeToClose: true,
      swipeThresholdPx: 88,
      swipeVelocityPxMs: 999,
    })

    const handle = document.querySelector('.swipe-handle')
    expect(handle).toBeTruthy()

    handle!.dispatchEvent(createPointerEvent('pointerdown', 0))
    window.dispatchEvent(createPointerEvent('pointermove', 120))
    window.dispatchEvent(createPointerEvent('pointerup', 120))
    await nextTick()

    expect(wrapper.emitted('close')).toBeTruthy()
  })
})
