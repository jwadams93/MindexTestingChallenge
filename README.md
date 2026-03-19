
---

# Sr Test Engineer – Todo Challenge

**Goal:** add and test small features in a tiny Todo app. You’ll write **E2E (SpecFlow + Selenium, non-headless)** and **service tests (xUnit)**.

## Prereqs

* .NET 8 SDK
* Google Chrome
* VS Code

## Run

From the repo root:

```bash
# 1) start the app (http://localhost:5173)
# Windows
.\scripts\run_app.ps1
# macOS/Linux
./scripts/run_app.sh
```

```bash
# 2) in a second terminal, run tests
# Service tests
.\scripts\run_service_tests.ps1   # or ./scripts/run_service_tests.sh

# E2E tests (Chrome visible)
.\scripts\run_e2e.ps1             # or ./scripts/run_e2e.sh
```

## Your tasks

* Add/adjust scenarios in `tests/E2E.Specs/Features/Todos.feature`.
* Implement steps in:

  * `tests/E2E.Specs/Steps/TodosSteps.cs`
  * `tests/E2E.Specs/Steps/AdvancedTodosSteps.cs`
* Extend service tests in `tests/Service.Tests/TodoServiceTests.cs`.

### What to cover (suggested)

* Create todo with **priority/due/tags**; shows **Overdue** when past due.
* **Edit** title; duplicate title shows an error.
* **Search & filter** (priority/status), **sort** by due.
* **Bulk** complete/delete.
* Service tests for **validation**, **idempotent complete/uncomplete**, **bulk counts**, **filter/sort**.

---

## What the app does

### UI features

* Add a todo with **Title**, **Priority** (Low/Medium/High), optional **Due date**, **Tags** (comma-separated), **Notes**.
* List shows **title**, a **priority badge**, an **Overdue** chip when `dueDate < today`, and rendered **#tags**.
* **Inline edit** (simple prompt for title), **Complete**, **Delete**.
* **Search** (title/tags), **filter** by **priority** & **status** (All/Active/Completed), **sort** by **Title/Priority/Due**.
* **Bulk actions:** select rows → **Complete** or **Delete**.

### Data model (server)

```csharp
Todo {
  Guid Id,
  string Title,
  bool Completed,
  bool Locked,        // cannot delete when true (simulated in tests)
  Priority Priority,  // Low|Medium|High
  DateTime? DueDate,
  string[] Tags,
  string Notes
}
```

### Validation & behavior

* **Title** required, trimmed, ≤ 100 chars.
* **Duplicate titles** (case-insensitive) rejected on add/update → **409 Conflict**.
* **Tags:** server sanitizes (strips `#`, replaces invalid chars with `-`, trims `-/_`, truncates to 20 chars, max 5). No 500s for bad tags.
* **Complete/Uncomplete** are idempotent.
* **Delete** fails if `Locked` → **409 Conflict**.

**Filters**

* Search matches **title or tags** (case-insensitive).
* If **all three priorities** are selected, it behaves like **no priority filter**.
* `dueBefore` is `<=`, `dueAfter` is `>=`.

**Sort**

* **Priority** High → Low (then Title).
* **Due date** Soonest first; `null` due goes to bottom.

### API reference (minimal)

```
GET    /api/todos?query=&priority=&status=&dueBefore=&dueAfter=&sort=
  priority = CSV of High,Medium,Low (omit or select all → no priority filter)
  status   = All|Active|Completed
  sort     = priority|due (empty = Title)

GET    /api/todos/{id}

POST   /api/todos
BODY:
{
  "title": "...",
  "priority": "High|Medium|Low",
  "dueDate": "2025-12-31T00:00:00Z",
  "tags": ["alpha","ok-1"],
  "notes": "..."
}

PUT    /api/todos/{id}   // partial
BODY:
{
  "title": "...",
  "priority": "High",
  "dueDate": "...",
  "tags": ["..."],
  "notes": "..."
}

PUT    /api/todos/{id}/complete
PUT    /api/todos/{id}/uncomplete
DELETE /api/todos/{id}

POST   /api/todos/bulk
BODY:
{ "op": "complete|delete", "ids": ["guid1","guid2"] }
```

**Dev-only (Development environment)**

```
DELETE /api/test/reset     // clear all
POST   /api/test/seed      // seed items
BODY:
{ "todos": [ { "title": "Buy milk", "priority": "High", "tags": ["alpha"] }, ... ] }
```

---

## What we’re looking for

* Clear Gherkin, stable selectors, sensible waits (no `Thread.Sleep`).
* Thoughtful split between **E2E** and **service** tests.
* Brief `NOTES.md` (assumptions, test data strategy, improvements you’d make next).
