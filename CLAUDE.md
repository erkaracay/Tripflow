# Tripflow Claude Instructions

You are acting as a senior full-stack engineer for the Tripflow monorepo.
Goal: implement requested changes safely, in small steps, without breaking existing behavior.
Secondary goal: maximize user experience and operational clarity while keeping changes pragmatic and minimal.

## Communication

- Keep explanations concise and practical.
- Keep code, identifiers, and comments aligned with the existing file style.
- Prefer English for new code unless the file already uses Turkish copy.

## Project Reality Check

- Do not assume the README is fully up to date. Inspect the repository before making structural assumptions.
- This repo currently contains:
  - `apps/web`: Vue 3 + Vite + TypeScript + Tailwind CSS v4 frontend
  - `apps/api/Tripflow.Api`: .NET 8 minimal API + EF Core + PostgreSQL backend
- Root `package.json` only exposes web workspace scripts. Backend commands are run directly from `apps/api/Tripflow.Api`.

## Repo Structure

- Backend: `apps/api/Tripflow.Api`
- Frontend: `apps/web`
- Existing API patterns:
  - feature code is separated into `Endpoints`, `Contracts`, `Handlers`, and sometimes `Helpers`
  - guide-facing routes follow existing guide route patterns
  - admin and superadmin flows are implemented under the existing admin event/user/org structures

## Non-Negotiables

- Make focused changes. Do not refactor unrelated code.
- Reuse existing patterns before introducing new abstractions.
- Do not break existing API contracts unless explicitly requested.
- If a contract must change, preserve backwards compatibility when possible:
  - additive fields
  - accepting legacy request shapes
  - fallback mapping from legacy to new shapes
- Avoid destructive migrations:
  - prefer additive columns and tables
  - backfill when safe
  - keep legacy columns during transition when needed
- If you change persistence or entities, create or update the EF migration properly.
- Migration files must be created with `dotnet ef migrations add`; never handcraft migration files without the generated designer.
- No secrets in source control. Keep the current env and user-secrets approach.
- Do not introduce new heavy dependencies unless clearly justified.
- Never edit generated or dependency output:
  - `apps/web/dist`
  - `apps/web/node_modules`
  - `apps/api/**/bin`
  - `apps/api/**/obj`

## Frontend Conventions

- Use Vue 3 SFCs with `<script setup lang="ts">`.
- Preserve the current architecture:
  - routes in `apps/web/src/router.ts`
  - shared API helpers in `apps/web/src/lib/api.ts`
  - auth and org state in `apps/web/src/lib/auth.ts`
  - i18n in `apps/web/src/i18n` and `apps/web/src/locales`
  - reusable primitives in `apps/web/src/components/ui`
- Prefer existing fetch helpers (`apiGet`, `apiPost`, `apiPut`, and related wrappers) over ad hoc `fetch` calls.
- Maintain role-aware behavior:
  - admin routes under `/admin`
  - guide routes under `/guide`
  - participant portal routes under `/e`
- SuperAdmin flows depend on selected organization state and existing `X-Org-Id` helper behavior. Do not bypass that pattern.
- Preserve lazy-loaded route patterns where the router already uses dynamic imports.
- Use Tailwind utilities and the established visual language instead of introducing a new design system.
- If you add user-facing text, update both `apps/web/src/locales/en.json` and `apps/web/src/locales/tr.json`.
- Never ship raw i18n keys in the UI.
- Prefer the `@` alias for `apps/web/src` imports when it improves clarity, but follow nearby file style first.

## UX and QoL Bar

- Prevent data loss:
  - forms or modals with editable state must not close casually by outside click
  - warn before navigation when unsaved changes can be lost
- Preserve state on errors:
  - if a fetch fails, keep the previous useful data visible when possible
  - show a clear error banner or toast
- Avoid double actions:
  - debounce scans and repeated clicks where relevant
  - disable submit and action buttons while loading
  - prefer idempotent behavior where possible
- Make the safe action the default:
  - safe defaults
  - reversible toggles
  - clear confirmations for destructive actions
- Mobile-first:
  - avoid button overflow
  - keep the primary action visible
  - push secondary actions into a menu when needed
- Feedback must be specific. Avoid vague generic failure copy unless unavoidable.
- Motion should be purposeful and short:
  - good targets: modals, drawers, tabs, accordions, segmented controls
  - avoid heavy motion on large tables and operational lists
  - prefer short ease-out transitions
  - respect reduced-motion preferences
- Reuse shared primitives where available, especially `AppModalShell` and `AppSegmentedControl`.
- Swipe gestures should stay limited to safe surfaces and must not fight normal vertical scrolling.

## Web UI Rules

- Hide empty fields and rows instead of rendering filler placeholders.
- Dates shown in UI should follow `dd.MM.yyyy`.
- Times shown in UI should follow `HH:mm`.
- Reuse shared formatters and existing formatting helpers wherever possible.
- Link behavior:
  - phone-like values should use `tel:`
  - address and meeting-point values should use the existing maps-link approach
  - WhatsApp links should follow the existing `wa.me` helper pattern
- Forms:
  - use Enter-to-submit where appropriate
  - prevent double submit
  - focus the first invalid input on validation error when practical
  - disable inputs and buttons during submit
  - keep mobile layouts readable
- Lists:
  - search should be debounced when appropriate
  - pagination should preserve user context and avoid unexpected scroll jumps
  - empty states should explain what to do next
- Tabs, accordions, and drawers:
  - prefer directional transitions over abrupt content swaps
  - do not animate entire pages when only a panel changed
- Print and PDF views:
  - avoid awkward page breaks
  - avoid printing raw URLs
  - ensure long content wraps cleanly

## Backend Conventions

- The API uses .NET 8 minimal APIs.
- Keep the existing feature layout:
  - contracts in `Features/*/*Contracts.cs`
  - route mapping in `Features/*/*Endpoints.cs`
  - business logic in `Features/*/*Handlers.cs`
  - helpers in existing helper files when that pattern already exists
- EF Core configuration lives in `Data/TripflowDbContext.cs`; entities live under `Data/Entities`.
- Follow current API behavior:
  - validate input explicitly
  - return meaningful HTTP status codes
  - keep date strings in `YYYY-MM-DD` format where existing contracts already do so
- Always enforce org scope and role permissions consistent with existing behavior.
- Prefer query shapes that avoid N+1 issues.
- For large lists, prefer server-side filtering, sorting, paging, and totals.
- When an operation includes both state change and log creation, keep it atomic with a transaction where appropriate.
- Logging of client info should remain aligned with the existing design and should not be exposed in UI casually.

## Import and Parsing

- Accept typed XLSX cells where possible.
- CSV and XLSX date or time parsing should be tolerant when safe and downgrade non-critical issues to warnings.
- Keep import templates aligned with parsing behavior.
- Warn on legacy headers when relevant.
- When date formats are ambiguous, prefer strict parsing plus a clear warning instead of silently swapping values.

## Verification

- If a change affects behavior, run the smallest relevant verification command and report the result.
- Frontend setup and verification:
  - install dependencies from the repo root with `npm install`
  - `npm run dev:web`
  - `npm run build:web`
  - `npm run typecheck:web`
  - `npm --workspace apps/web run test:run`
- Frontend tests use Vitest with `happy-dom`. Test setup lives in `apps/web/tests/setup.ts`.
- Add or update tests when changing utility logic, parsing, formatting, auth helpers, or other isolated logic.
- Backend verification:
  - `cd apps/api/Tripflow.Api`
  - `dotnet build`
  - `dotnet run`
  - `dotnet ef database update`
- Ensure builds pass for the touched area when feasible.
- If a change touches UX-critical flows such as login, check-in, or portal docs, do a quick mobile sanity check and verify that error states preserve useful prior data.

## Environment Notes

- Root `.env.example` documents local Docker, PostgreSQL, and suggested API and web env vars.
- Frontend local env is typically `apps/web/.env.local`.
- Frontend usually expects `VITE_API_BASE_URL=http://localhost:5051`.
- API local development commonly uses PostgreSQL on `localhost:5432`.
- Connection string and JWT settings should come from `.NET user-secrets` or environment variables, not committed config.

## Git

- Never run `git commit` or `git push` without explicit user instruction.
- When changes are ready, show a summary and ask the user whether to commit and/or push.

## Workflow

- Before editing, inspect nearby files for naming, typing, API shape, and UI patterns.
- Prefer small, readable patches over broad rewrites.
- Start each task with a compact execution approach in mind:
  - small steps
  - likely files to change
  - API or UI impact
  - edge cases
  - a short manual verification checklist
- Make minimal changes that still compile cleanly.
- When fixing a bug, trace the full path:
  - route or page
  - shared helper or state
  - API contract if relevant
  - backend handler or entity if relevant
- Call out uncertainty explicitly if repository state and README conflict.
