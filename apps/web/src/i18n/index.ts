import { createI18n } from 'vue-i18n'
import en from '../locales/en.json'
import tr from '../locales/tr.json'

const LOCALE_KEY = 'tripflow_locale'

const supportedLocales = ['en', 'tr'] as const
export type Locale = typeof supportedLocales[number]

const normalizeLocale = (value?: string | null): Locale | null => {
  if (!value) {
    return null
  }

  const normalized = value.toLowerCase()
  if (normalized.startsWith('tr')) {
    return 'tr'
  }

  if (normalized.startsWith('en')) {
    return 'en'
  }

  return null
}

const detectDefaultLocale = (): Locale => {
  const stored = normalizeLocale(globalThis.localStorage?.getItem(LOCALE_KEY))
  if (stored) {
    return stored
  }

  const browser = normalizeLocale(globalThis.navigator?.languages?.[0] ?? globalThis.navigator?.language)
  return browser ?? 'en'
}

export const i18n = createI18n({
  legacy: false,
  locale: 'en',
  fallbackLocale: 'en',
  messages: {
    en,
    tr,
  },
})

export const setLocale = (locale: Locale) => {
  i18n.global.locale.value = locale
  globalThis.localStorage?.setItem(LOCALE_KEY, locale)
}

export const initLocale = () => {
  const locale = detectDefaultLocale()
  setLocale(locale)
  return locale
}
