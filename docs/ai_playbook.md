# Infora AI Playbook (for Cursor / contributors)

This document explains how we implement features in Infora, what patterns we follow, and what “done” means.

## 1) Product surfaces

- Admin web: event setup, participants, imports, check-in, logs, docs tabs, program editor.
- Guide web: check-in + program view + operational quick actions.
- Participant portal: read-only tabs (Days/Docs/QR/Info), print/PDF views.

## 2) Roles and access patterns

- SuperAdmin: cross-org management, can create Admin/Guide users, must always be explicit about org scope.
- Admin: event-level management inside org scope.
- Guide: scoped to assigned events (and org), mostly read/operate flows (check-in, program view).

## 3) UX principles (non-negotiable)

- QoL is a feature: reduce friction, remove confusion, keep screens calm.
- Prevent data loss: editing modals/forms must not close by outside click; preserve inputs on errors.
- Clear feedback: result-aware toasts; no vague “failed” if we can classify (NotFound vs InvalidRequest).
- Mobile-first ergonomics: primary action visible, secondary actions in ⋯ menu, no button overflow.
- Consistency: formatting + i18n + layouts must feel the same across Admin/Guide/Portal.

## 4) UI conventions (apps/web)

### i18n

- Every label, toast, empty state, chip text must be in en.json and tr.json.
- Never leave raw keys visible. If you add a key, add both locales.

### Formatting

- UI dates: dd.MM.yyyy
- UI times: HH:mm
- Baggage: “{pieces} pc • {kg} kg” (use formatBaggage)
- Keep helpers in one place and reuse everywhere.

### Hide empty fields

- If a field is null/empty, do not render the row (e.g., PNR missing => row not shown).

### Clickable semantics (tel / maps / whatsapp)

- Phone-like values should open dialer (tel:).
- Address/meeting-point style values should open a maps link (Google/Apple) via a helper.
- WhatsApp actions should use wa.me with an encoded message.

### Forms

- Enter-to-submit where it makes sense (login/create/modals).
- Disable controls while saving; guard against double submit.
- Focus the first invalid field on error.
- On fetch failure, keep old data rendered + show error banner/toast.

### Lists / tables

- Debounced search.
- Paging does not unexpectedly scroll/jump.
- Filters should be obvious and easy to clear.
- Empty state explains what happened (filters, query) and how to recover.

### Mobile ergonomics

- Avoid too many visible buttons on a row.
- Keep primary action visible; secondary actions go into ⋯ menu.
- Modals should not close by outside click when data loss is possible.
- Modals must scroll on small screens (max-height like 90vh).

### Print/PDF

- Use print CSS:
  - page-break-inside: avoid on cards/sections
  - page-break-before on major headings when helpful
  - hide link URLs in print (avoid showing long hrefs)
  - ensure readable typography on A4
  - long text wraps nicely; avoid cutting headings from their content

## 5) API conventions (apps/api)

### Folder patterns

- Endpoints: HTTP routing + auth/role checks + calling handlers
- Contracts: request/response DTOs
- Handlers: main logic, DB queries, validation
- Helpers: parsing/formatting utilities

### Org scope

- All event data is org-scoped.
- Always apply OrganizationId filters in queries.
- Prefer one place for resolving org from token/header; do not duplicate logic.

### Performance

- Avoid N+1: prefer joins / projections.
- Paging endpoints should return { page, pageSize, total, items } response.
- Sorting and filtering should be server-side for large lists.

### Migrations

- Prefer additive migrations:
  - add columns/tables, keep old columns during transition
  - backfill when safe
  - introduce fallback mapping (legacy -> new)
- Only remove legacy columns after production confidence + explicit follow-up migration plan.

### Logging semantics

- When writing a log plus applying an effect (like check-in), do it in a single transaction.
- Client info (ip/userAgent) may be stored in DB when needed; do not display in UI tables unless requested.
- CSV exports can include extra columns at the end when needed.

## 6) Docs tabs model (portal docs)

- Built-in tabs exist (e.g., Flight/Hotel/Insurance).
- Custom tabs support two editing modes:
  - Form mode: free text + key/value fields list (add/remove rows)
  - Advanced mode: raw JSON (optional), must preserve legacy compatibility
- Portal rendering should support:
  - { text, fields[] } (preferred)
  - legacy flat JSON object (read-only display)
- Rendering rules:
  - hide empty rows (no placeholders)
  - phone/address-like fields become clickable (tel/maps)

## 7) Import templates

- Templates must match parser headers exactly.
- Keep parsing tolerant:
  - XLSX typed cells (date/time)
  - CSV strings with common TR formats
- Warnings vs errors:
  - warnings for optional fields that fail parsing
  - errors for required fields (tc, fullName, etc.)

## 8) Definition of done (DoD)

- Builds pass:
  - dotnet build (api)
  - npm --workspace apps/web run build
- No raw i18n keys in UI
- Mobile layout sanity check (no overflow, primary action visible)
- Manual test checklist updated and performed for changed flows
- Contract changes are backward compatible (unless explicitly breaking)

## 9) Suggested work loop for Cursor

1) Summarize the ask in 3–6 bullets.
2) Propose a minimal plan and list the files to edit.
3) Implement in small diffs.
4) Provide a short manual test checklist and call out edge cases.

## 10) Commit style

Prefer at most 2–3 commits per feature:

- feat(api|db): ...
- feat(web): ...
- fix(web|api): ...
