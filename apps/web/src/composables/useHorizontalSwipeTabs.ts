import type { Ref } from 'vue'

type SwipeConfig<T extends string> = {
  orderedIds: T[]
  activeId: Ref<T>
  setActiveId: (value: T) => void
  enabled?: () => boolean
  threshold?: number
  directionRatio?: number
}

type SwipeHandlers = {
  touchstart: (event: TouchEvent) => void
  touchmove: (event: TouchEvent) => void
  touchend: () => void
  touchcancel: () => void
}

const INTERACTIVE_SELECTOR = [
  'a',
  'input',
  'textarea',
  'select',
  '[contenteditable="true"]',
  '[data-no-swipe]',
].join(',')

type DirectionLock = 'undecided' | 'horizontal' | 'vertical'

type SwipeState = {
  startX: number
  startY: number
  deltaX: number
  deltaY: number
  active: boolean
  direction: DirectionLock
}

const createInitialState = (): SwipeState => ({
  startX: 0,
  startY: 0,
  deltaX: 0,
  deltaY: 0,
  active: false,
  direction: 'undecided',
})

export function useHorizontalSwipeTabs<T extends string>(config: SwipeConfig<T>): { bindSwipeHandlers: SwipeHandlers } {
  const threshold = config.threshold ?? 48
  const directionRatio = config.directionRatio ?? 1.5
  const state = createInitialState()

  const isEnabled = () => config.enabled?.() ?? true

  const reset = () => {
    state.startX = 0
    state.startY = 0
    state.deltaX = 0
    state.deltaY = 0
    state.active = false
    state.direction = 'undecided'
  }

  const startedOnInteractiveElement = (target: EventTarget | null) => {
    const element = target instanceof Element ? target : null
    return Boolean(element?.closest(INTERACTIVE_SELECTOR))
  }

  const onTouchstart = (event: TouchEvent) => {
    if (!isEnabled() || event.touches.length !== 1 || startedOnInteractiveElement(event.target)) {
      reset()
      return
    }

    const touch = event.touches[0]
    if (!touch) {
      reset()
      return
    }

    state.startX = touch.clientX
    state.startY = touch.clientY
    state.deltaX = 0
    state.deltaY = 0
    state.active = true
    state.direction = 'undecided'
  }

  const onTouchmove = (event: TouchEvent) => {
    if (!state.active || event.touches.length !== 1) {
      return
    }

    const touch = event.touches[0]
    if (!touch) {
      reset()
      return
    }

    state.deltaX = touch.clientX - state.startX
    state.deltaY = touch.clientY - state.startY

    const absX = Math.abs(state.deltaX)
    const absY = Math.abs(state.deltaY)

    if (state.direction === 'undecided') {
      if (absY > 8 && absY > absX) {
        state.direction = 'vertical'
        return
      }

      if (absX > 8 && absX >= absY * directionRatio) {
        state.direction = 'horizontal'
      }
    }

    if (state.direction === 'horizontal' && event.cancelable) {
      event.preventDefault()
    }
  }

  const onTouchend = () => {
    if (!state.active || state.direction !== 'horizontal') {
      reset()
      return
    }

    const absX = Math.abs(state.deltaX)
    const absY = Math.abs(state.deltaY)
    if (absX < threshold || absX < absY * directionRatio) {
      reset()
      return
    }

    const currentIndex = config.orderedIds.indexOf(config.activeId.value)
    if (currentIndex < 0) {
      reset()
      return
    }

    const nextIndex = state.deltaX < 0 ? currentIndex + 1 : currentIndex - 1
    if (nextIndex >= 0 && nextIndex < config.orderedIds.length) {
      const nextId = config.orderedIds[nextIndex]
      if (nextId) {
        config.setActiveId(nextId)
      }
    }

    reset()
  }

  return {
    bindSwipeHandlers: {
      touchstart: onTouchstart,
      touchmove: onTouchmove,
      touchend: onTouchend,
      touchcancel: reset,
    },
  }
}
