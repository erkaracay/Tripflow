import type { AuditLogItem } from '../types'

type AuditLogTranslator = (key: string, params?: Record<string, unknown>) => string

export type AuditLogDetailItem = {
  label: string
  value: string
}

export type AuditLogFormattedDetails = {
  items: AuditLogDetailItem[]
  changes: AuditLogDetailItem[]
  prettyJson: string | null
}

type ExtraRecord = Record<string, unknown>

const knownFieldKeys: Record<string, string> = {
  name: 'admin.auditLog.changedFields.name',
  startDate: 'admin.auditLog.changedFields.startDate',
  endDate: 'admin.auditLog.changedFields.endDate',
  eventAccessCode: 'admin.auditLog.changedFields.eventAccessCode',
  contacts: 'admin.auditLog.changedFields.contacts',
  portal: 'admin.auditLog.changedFields.portal',
  guides: 'admin.auditLog.changedFields.guides',
  roomAssignments: 'admin.auditLog.changedFields.roomAssignments',
  flightSegments: 'admin.auditLog.changedFields.flightSegments',
  willNotAttend: 'admin.auditLog.changedFields.willNotAttend',
  participants: 'admin.auditLog.changedFields.participants',
  title: 'admin.auditLog.changedFields.title',
  note: 'admin.auditLog.changedFields.note',
}

const humanize = (value: string) =>
  value
    .replace(/[._]+/g, ' ')
    .replace(/\b\w/g, (letter) => letter.toUpperCase())

const parseExtra = (raw: string | null | undefined): ExtraRecord | null => {
  if (!raw) return null

  try {
    const parsed = JSON.parse(raw) as unknown
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? (parsed as ExtraRecord)
      : null
  } catch {
    return null
  }
}

const getStringValue = (extra: ExtraRecord | null, key: string): string | null =>
  typeof extra?.[key] === 'string' ? (extra[key] as string) : null

const formatBoolean = (value: boolean, t: AuditLogTranslator) =>
  value ? t('common.yes') : t('common.no')

const formatValue = (value: unknown): string => {
  if (value === null || value === undefined) return '—'
  if (typeof value === 'string') return value
  if (typeof value === 'number' || typeof value === 'boolean') return String(value)
  if (Array.isArray(value)) {
    return value.map((entry) => formatValue(entry)).join(', ')
  }

  return JSON.stringify(value, null, 2)
}

const getReasonLabel = (reason: string, t: AuditLogTranslator) => {
  const key = `admin.auditLog.reasons.${reason}`
  const translated = t(key)
  return translated === key ? humanize(reason) : translated
}

const getChangedFieldLabel = (field: string, t: AuditLogTranslator) => {
  const key = knownFieldKeys[field] ?? `admin.auditLog.changedFields.${field}`
  const translated = t(key)
  return translated === key ? humanize(field) : translated
}

export const formatAuditLogExtra = (item: AuditLogItem, t: AuditLogTranslator): AuditLogFormattedDetails => {
  const extra = parseExtra(item.extraJson)
  const items: AuditLogDetailItem[] = []
  const changes: AuditLogDetailItem[] = []

  if (extra) {
    if (typeof extra.reason === 'string') {
      items.push({ label: t('admin.auditLog.fields.reason'), value: getReasonLabel(extra.reason, t) })
    }

    if (typeof extra.attemptedEmail === 'string') {
      items.push({ label: t('admin.auditLog.fields.attemptedEmail'), value: extra.attemptedEmail })
    }

    if (typeof extra.attemptedTcNoMasked === 'string') {
      items.push({ label: t('admin.auditLog.fields.tcNo'), value: extra.attemptedTcNoMasked })
    }

    if (typeof extra.eventAccessCode === 'string') {
      items.push({ label: t('admin.auditLog.fields.eventAccessCode'), value: extra.eventAccessCode })
    }

    if (typeof extra.fileType === 'string') {
      items.push({ label: t('admin.auditLog.fields.fileType'), value: extra.fileType })
    }

    if (typeof extra.rowCount === 'number') {
      items.push({ label: t('admin.auditLog.fields.rowCount'), value: String(extra.rowCount) })
    }

    if (typeof extra.createdCount === 'number') {
      items.push({ label: t('admin.auditLog.fields.createdCount'), value: String(extra.createdCount) })
    }

    if (typeof extra.updatedCount === 'number') {
      items.push({ label: t('admin.auditLog.fields.updatedCount'), value: String(extra.updatedCount) })
    }

    if (typeof extra.deletedCount === 'number') {
      items.push({ label: t('admin.auditLog.fields.deletedCount'), value: String(extra.deletedCount) })
    }

    if (typeof extra.warningCount === 'number') {
      items.push({ label: t('admin.auditLog.fields.warningCount'), value: String(extra.warningCount) })
    }

    if (typeof extra.errorCount === 'number') {
      items.push({ label: t('admin.auditLog.fields.errorCount'), value: String(extra.errorCount) })
    }

    if (typeof extra.dryRun === 'boolean') {
      items.push({ label: t('admin.auditLog.fields.dryRun'), value: formatBoolean(extra.dryRun, t) })
    }

    if (Array.isArray(extra.changedFields) && extra.changedFields.length > 0) {
      items.push({
        label: t('admin.auditLog.fields.changedFields'),
        value: extra.changedFields
          .filter((field): field is string => typeof field === 'string')
          .map((field) => getChangedFieldLabel(field, t))
          .join(', '),
      })
    }

    if (extra.changes && typeof extra.changes === 'object' && !Array.isArray(extra.changes)) {
      for (const [field, rawDelta] of Object.entries(extra.changes as ExtraRecord)) {
        if (!rawDelta || typeof rawDelta !== 'object' || Array.isArray(rawDelta)) continue
        const delta = rawDelta as ExtraRecord
        if (!('before' in delta) && !('after' in delta)) continue

        changes.push({
          label: getChangedFieldLabel(field, t),
          value: `${formatValue(delta.before)} → ${formatValue(delta.after)}`,
        })
      }
    }
  }

  return {
    items,
    changes,
    prettyJson: item.extraJson
      ? (() => {
          try {
            return JSON.stringify(JSON.parse(item.extraJson), null, 2)
          } catch {
            return item.extraJson
          }
        })()
      : null,
  }
}

export const getAuditLogParticipantName = (item: AuditLogItem) =>
  getStringValue(parseExtra(item.extraJson), 'participantName')

export const getAuditLogParticipantTcNoMasked = (item: AuditLogItem) =>
  getStringValue(parseExtra(item.extraJson), 'participantTcNoMasked')
