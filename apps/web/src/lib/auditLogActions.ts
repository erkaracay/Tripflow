import type { AppComboboxOption } from '../types'

type AuditLogTranslator = (key: string, params?: Record<string, unknown>) => string

type AuditLogActionMeta = {
  labelKey: string
  category: string
}

const AUDIT_LOG_ACTIONS: Record<string, AuditLogActionMeta> = {
  'auth.login': { labelKey: 'admin.auditLog.actions.auth_login', category: 'auth' },
  'auth.logout': { labelKey: 'admin.auditLog.actions.auth_logout', category: 'auth' },
  'portal.login': { labelKey: 'admin.auditLog.actions.portal_login', category: 'portal' },
  'portal.logout': { labelKey: 'admin.auditLog.actions.portal_logout', category: 'portal' },
  'event.create': { labelKey: 'admin.auditLog.actions.event_create', category: 'event' },
  'event.update': { labelKey: 'admin.auditLog.actions.event_update', category: 'event' },
  'event.archive': { labelKey: 'admin.auditLog.actions.event_archive', category: 'event' },
  'event.restore': { labelKey: 'admin.auditLog.actions.event_restore', category: 'event' },
  'event.purge': { labelKey: 'admin.auditLog.actions.event_purge', category: 'event' },
  'event.access_code.update': { labelKey: 'admin.auditLog.actions.event_access_code_update', category: 'event' },
  'event.contacts.update': { labelKey: 'admin.auditLog.actions.event_contacts_update', category: 'event' },
  'event.guides.update': { labelKey: 'admin.auditLog.actions.event_guides_update', category: 'event' },
  'event.portal.update': { labelKey: 'admin.auditLog.actions.event_portal_update', category: 'event' },
  'event.checkin': { labelKey: 'admin.auditLog.actions.event_checkin', category: 'checkin' },
  'event.checkin.undo': { labelKey: 'admin.auditLog.actions.event_checkin_undo', category: 'checkin' },
  'event.checkin.reset_all': { labelKey: 'admin.auditLog.actions.event_checkin_reset_all', category: 'checkin' },
  'event_day.create': { labelKey: 'admin.auditLog.actions.event_day_create', category: 'program' },
  'event_day.update': { labelKey: 'admin.auditLog.actions.event_day_update', category: 'program' },
  'event_day.delete': { labelKey: 'admin.auditLog.actions.event_day_delete', category: 'program' },
  'event_activity.create': { labelKey: 'admin.auditLog.actions.event_activity_create', category: 'program' },
  'event_activity.update': { labelKey: 'admin.auditLog.actions.event_activity_update', category: 'program' },
  'event_activity.delete': { labelKey: 'admin.auditLog.actions.event_activity_delete', category: 'program' },
  'participant.create': { labelKey: 'admin.auditLog.actions.participant_create', category: 'participant' },
  'participant.update': { labelKey: 'admin.auditLog.actions.participant_update', category: 'participant' },
  'participant.delete': { labelKey: 'admin.auditLog.actions.participant_delete', category: 'participant' },
  'participant.delete_all': { labelKey: 'admin.auditLog.actions.participant_delete_all', category: 'participant' },
  'participant.import': { labelKey: 'admin.auditLog.actions.participant_import', category: 'participant' },
  'participant.rooms.bulk_apply': { labelKey: 'admin.auditLog.actions.participant_rooms_bulk_apply', category: 'participant' },
  'participant.flights.bulk_apply': { labelKey: 'admin.auditLog.actions.participant_flights_bulk_apply', category: 'participant' },
  'participant.will_not_attend.set': { labelKey: 'admin.auditLog.actions.participant_will_not_attend_set', category: 'participant' },
  'event_item.create': { labelKey: 'admin.auditLog.actions.event_item_create', category: 'event' },
  'event_item.update': { labelKey: 'admin.auditLog.actions.event_item_update', category: 'event' },
  'event_item.delete': { labelKey: 'admin.auditLog.actions.event_item_delete', category: 'event' },
  'event_item.action': { labelKey: 'admin.auditLog.actions.event_item_action', category: 'event' },
  'accommodation_segment.create': { labelKey: 'admin.auditLog.actions.accommodation_segment_create', category: 'accommodation' },
  'accommodation_segment.update': { labelKey: 'admin.auditLog.actions.accommodation_segment_update', category: 'accommodation' },
  'accommodation_segment.delete': { labelKey: 'admin.auditLog.actions.accommodation_segment_delete', category: 'accommodation' },
  'accommodation_segment.participants.bulk_apply': { labelKey: 'admin.auditLog.actions.accommodation_segment_participants_bulk_apply', category: 'accommodation' },
  'meal_group.create': { labelKey: 'admin.auditLog.actions.meal_group_create', category: 'meal' },
  'meal_group.update': { labelKey: 'admin.auditLog.actions.meal_group_update', category: 'meal' },
  'meal_group.delete': { labelKey: 'admin.auditLog.actions.meal_group_delete', category: 'meal' },
  'meal_option.create': { labelKey: 'admin.auditLog.actions.meal_option_create', category: 'meal' },
  'meal_option.update': { labelKey: 'admin.auditLog.actions.meal_option_update', category: 'meal' },
  'meal_option.delete': { labelKey: 'admin.auditLog.actions.meal_option_delete', category: 'meal' },
  'activity.checkin': { labelKey: 'admin.auditLog.actions.activity_checkin', category: 'checkin' },
  'activity.checkin.reset_all': { labelKey: 'admin.auditLog.actions.activity_checkin_reset_all', category: 'checkin' },
  'activity_participant.will_not_attend.set': { labelKey: 'admin.auditLog.actions.activity_participant_will_not_attend_set', category: 'checkin' },
}

const TARGET_TYPE_LABELS: Record<string, string> = {
  user: 'admin.auditLog.targetTypes.user',
  event: 'admin.auditLog.targetTypes.event',
  participant: 'admin.auditLog.targetTypes.participant',
  event_day: 'admin.auditLog.targetTypes.eventDay',
  event_activity: 'admin.auditLog.targetTypes.eventActivity',
  event_item: 'admin.auditLog.targetTypes.eventItem',
  meal_group: 'admin.auditLog.targetTypes.mealGroup',
  meal_option: 'admin.auditLog.targetTypes.mealOption',
  accommodation_segment: 'admin.auditLog.targetTypes.accommodationSegment',
}

const CATEGORY_FILTERS: Record<string, string> = {
  all: '',
  auth: 'auth.',
  event: 'event.,event_item.',
  participant: 'participant.',
  program: 'event_day.,event_activity.',
  accommodation: 'accommodation_segment.',
  meal: 'meal_group.,meal_option.',
  portal: 'portal.',
  checkin: 'event.checkin,activity.checkin,activity_participant.',
}

export const FAILED_LOGIN_FILTER = 'auth.login'
export const DELETE_ACTION_FILTER = [
  'event.purge',
  'event_day.delete',
  'event_activity.delete',
  'participant.delete',
  'participant.delete_all',
  'event_item.delete',
  'accommodation_segment.delete',
  'meal_group.delete',
  'meal_option.delete',
].join(',')

const humanize = (value: string) =>
  value
    .replace(/[._]+/g, ' ')
    .replace(/\b\w/g, (letter) => letter.toUpperCase())

export const getAuditLogCategoryOptions = (t: AuditLogTranslator): AppComboboxOption[] => [
  { value: 'all', label: t('admin.auditLog.filters.categories.all') },
  { value: 'auth', label: t('admin.auditLog.filters.categories.auth') },
  { value: 'event', label: t('admin.auditLog.filters.categories.event') },
  { value: 'participant', label: t('admin.auditLog.filters.categories.participant') },
  { value: 'program', label: t('admin.auditLog.filters.categories.program') },
  { value: 'accommodation', label: t('admin.auditLog.filters.categories.accommodation') },
  { value: 'meal', label: t('admin.auditLog.filters.categories.meal') },
  { value: 'portal', label: t('admin.auditLog.filters.categories.portal') },
  { value: 'checkin', label: t('admin.auditLog.filters.categories.checkin') },
]

export const getAuditLogCategoryFilter = (value: string) => CATEGORY_FILTERS[value] ?? ''

export const getAuditLogActionLabel = (action: string, t: AuditLogTranslator) => {
  const key = AUDIT_LOG_ACTIONS[action]?.labelKey
  if (!key) {
    return humanize(action)
  }

  const translated = t(key)
  return translated === key ? humanize(action) : translated
}

export const getAuditLogActionCategory = (action: string) => AUDIT_LOG_ACTIONS[action]?.category ?? 'all'

export const getAuditLogTargetTypeLabel = (targetType: string, t: AuditLogTranslator) => {
  const key = TARGET_TYPE_LABELS[targetType]
  if (!key) {
    return humanize(targetType)
  }

  const translated = t(key)
  return translated === key ? humanize(targetType) : translated
}

export const getAuditLogTargetLabel = (targetType: string, targetId: string | null | undefined, t: AuditLogTranslator) => {
  const typeLabel = getAuditLogTargetTypeLabel(targetType, t)
  return targetId ? `${typeLabel} · ${targetId}` : typeLabel
}

export const getAuditLogResultClass = (result: string) => {
  if (result === 'success') return 'bg-emerald-100 text-emerald-700'
  if (result === 'fail') return 'bg-rose-100 text-rose-700'
  if (result === 'blocked') return 'bg-amber-100 text-amber-800'
  return 'bg-slate-100 text-slate-700'
}

export const getAuditLogResultLabel = (result: string, t: AuditLogTranslator) => {
  const key = `admin.auditLog.results.${result}`
  const translated = t(key)
  return translated === key ? humanize(result) : translated
}
