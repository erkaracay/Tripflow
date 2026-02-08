# Tripflow AI Playbook (for Cursor / contributors)

This document explains how we implement features in Tripflow, what patterns we follow, and what “done” means.

## 1) Product surfaces

- Admin web: event setup, participants, imports, check-in, logs, docs tabs, program editor.
- Guide web: check-in + program view + operational quick actions.
- Participant portal: read-only tabs (Days/Docs/QR/Info), print/PDF views.

## 2) Roles and access patterns

- SuperAdmin: cross-org management, can create Admin/Guide users, must always be explicit about org scope.
- Admin: event-level management inside org scope.
- Guide: scoped to assigned events (and org), mostly read/operate flows (check-in, program view).

## 3) UI conventions (apps/web)

### i18n

- Every label, toast, empty state, chip text must be in en.json and tr.json.
- Never leave raw keys visible. If you add a key, add both locales.

### Formatting

- UI dates: dd.MM.yyyy
- UI times: HH:mm
- Baggage: “{pieces} pc • {kg} kg” style (use formatBaggage)
- Use shared helpers in a single place (formatters.ts or similar) and reuse them everywhere.

### Hide empty fields

- If a field is null/empty, do not render the row (e.g., PNR missing => row not shown).

### Forms

- Enter-to-submit where it makes sense (login/create/modals).
- Disable controls while saving; guard against double submit.
- Focus the first invalid field on error.

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

## 4) API conventions (apps/api)

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
- Paging endpoints should return { page, pageSize, total, items } style response.
- Sorting and filtering should be server-side for large lists.

### Migrations

- Prefer additive migrations:
  - add columns/tables, keep old columns during transition
  - backfill when safe
  - introduce fallback mapping (legacy -> new)
- Only remove legacy columns after we’re confident in production and have a follow-up migration plan.

### Logging semantics

- When writing a log plus applying an effect (like check-in), do it in a single transaction.
- If client info (ip/userAgent) exists, store it in DB; do not display in UI tables unless requested.
- CSV exports can include extra columns at the end when needed.

## 5) Docs tabs model (portal docs)

- Built-in tabs exist (e.g., Flight/Hotel/Insurance).
- Custom tabs support two editing modes:
  - Form mode: free text + key/value fields list (add/remove rows)
  - Advanced mode: raw JSON (optional), must preserve legacy compatibility
- Portal rendering should support:
  - { text, fields[] } (preferred)
  - legacy flat JSON object (read-only display)

## 6) Import templates

- Templates must match parser headers exactly.
- Keep parsing tolerant:
  - XLSX typed cells (date/time)
  - CSV strings with common TR formats
- Warnings vs errors:
  - Prefer warnings for optional fields that fail parsing
  - Errors for required fields (tc, fullName, etc.)

## 7) Definition of done (DoD)

For a change to be “done”:

- Builds pass:
  - dotnet build (api)
  - npm --workspace apps/web run build
- No raw i18n keys in UI
- Mobile layout does not break (basic manual check)
- Manual test checklist is updated and performed for the changed flows
- Contract changes are backward compatible (unless explicitly breaking)

## 8) Suggested “work loop” for Cursor

When starting any task:

1) Summarize the ask in 3–6 bullets.
2) Propose a minimal plan and list the files to edit.
3) Implement in small diffs.
4) Provide a short manual test checklist and call out edge cases.

## 9) Commit style

Prefer at most 2–3 commits per feature:

- feat(api|db): ...
- feat(web): ...
- fix(web|api): ...
