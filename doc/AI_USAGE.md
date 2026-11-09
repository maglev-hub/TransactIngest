# AI Usage

## Summary

AI tools were used during development: GitHub Copilot in Visual Studio (inline completions and
Copilot Chat) for coding, and Claude for code review, testing strategy, and documentation review.
All AI-suggested code was reviewed, modified where appropriate, compiled, and tested before
inclusion.

| Tool | Used for |
| --- | --- |
| GitHub Copilot (inline completions) | Code completion while writing the implementation |
| GitHub Copilot Chat | Implementing features and review fixes |
| Claude (Anthropic) | Code review, test design and gap analysis, documentation review, drafting Copilot instructions and prompts |

The prompts below are condensed versions of the prompts used, grouped by development step.

## Implementation Prompts (GitHub Copilot Chat)

### 1. Domain entities

1. Create entity `TransactionRecord` with a surrogate key, the source fields, a UTC
   `TransactionTime`, `Status` defaulting to `Active`, and an `AuditHistory` collection.
   No data annotations.
2. Create entity `TransactionAudit` with `FieldName`, `OldValue`, `NewValue`, `ChangedTimeUtc` and
   a navigation property to `TransactionRecord`.

### 2. Application layer and import service

1. Create `ImportResult` with per-outcome counts and an `Errors` list, and `IImportService`.
2. Create the `ImportService` skeleton: inject sources, repository, logger and `TimeProvider`;
   read the time once; process each source in its own try/catch; save once at the end.
3. Insert snapshot records with no match. Use `Finalized` if older than 24 hours, otherwise
   `Active`; no audit row on insert.
4. For matching records within 24 hours, compare each field, write one audit row per change, and
   apply the new value. Skip `Finalized` or older records as immutable.
5. Reactivate a `Revoked` record that reappears within 24 hours and audit the status change.
   Never reactivate `Finalized` records.
6. After each snapshot, finalize `Active` records older than 24 hours and revoke `Active` records
   missing from the snapshot within 24 hours, each with a status audit row.
7. Add structured logging for import start, per-source counts, failures and final totals.

### 3. Infrastructure and API

1. Add a convention-based `DateTime` converter that marks values read from SQLite as UTC.
2. Write mock sources: an `InMemoryTransactionSource` base class plus Uptown, Downtown and Online
   sources. Use `TransactionId` 100001 in all three, make times relative to an anchor captured
   once, and add configurable `ExcludedTransactionIds` and `AmountOverrides` read through
   `IOptionsMonitor` so updates and revocations can be demonstrated.
3. Create a `GlobalExceptionHandler` that logs the exception and returns a safe 500
   ProblemDetails response.
4. Update `Program.cs`: DI registrations, named options per source, CORS for the Angular dev
   server, ProblemDetails, and the correct middleware order.

### 4. SQLite database

1. Which EF Core SQLite packages are needed, and in which projects? Where does the `.db` file
   get created?
2. Show the `dotnet ef` and Package Manager Console commands to add `InitialCreate` and update
   the database.
3. Apply migrations at startup with `Database.Migrate()` so reviewers don't need EF commands.
4. How do I inspect the tables and reset the database?

### 5. Angular frontend

1. Review my sample responses from `GET /api/transactions` and `POST /api/import`, and update
   `TransactionModel` and `ImportResultModel` to match the JSON, including `Finalized` and
   `finalizedTransactions`. Keep `transactionTime` as a string so the UTC suffix is preserved.
2. Implement `getTransactions()` and `importPayments()` in the transaction service with typed
   `HttpClient` calls and the configured base URL. 
3. In the standalone `TransactionList` component, add `transactions` and
   `isLoadingTransactions` signals. Load on open, reuse the same method for Refresh, and reset
   loading on success and failure.
4. Render the transaction table with the required columns, formatting amounts as currency and
   UTC times in the viewer's local time.
5. Wire the Import button to `importPayments()`. Set `isImporting` and disable the button while
   the request runs, store the result, then refresh the table. If the refresh fails, keep the
   import summary and show an error.
6. Display the import summary: new, updated, unchanged, revoked and finalized counts, plus failed
   sources and errors.
7. Add client-side column sorting with a computed signal and status badges.

## Review and Testing Prompts (Claude)

1. Review my backend code file by file against the exercise requirements, and list issues by
   severity with suggested fixes.
2. Review the import rules in `ImportService` against the acceptance criteria: insert, field-level
   update and audit, revocation within 24 hours, finalization and idempotency. Walk through the
   edge cases.
3. Generate xUnit tests for `ImportService` using hand-written fakes and a fixed clock.
4. Find gaps in the xUnit tests.
5. Review `README.md` and `DESIGN_DECISIONS.md` for accuracy against the code and for missing
   sections.

## Design Discussions

Main topics discussed with Claude during review:

- **UTC handling.** SQLite has no datetime type, so values are read back as `Unspecified`. This
  led to the UTC value converter and to using `DateTime` with UTC instead of `DateTimeOffset`,
  because EF Core's SQLite provider cannot compare or sort `DateTimeOffset` in queries.

- **Source failures.** A failed fetch must not be treated as an empty snapshot, or every recent
  transaction from that source would be revoked.
- **Finalization.** Immutability has to be checked by time, not only by status, so rows older
  than 24 hours cannot be updated before the finalization step marks them.
- **Testability.** Injecting `TimeProvider` makes the exact 23h59m / 24h / 24h01m boundaries
  testable.
- **Demo data.** Fixed sample dates would finalize everything on the first import,
  so the mock sources use times relative to an anchor plus configuration-driven changes.

## Modifications to AI Output

- Changed finalization from a status-based check to a time-based check after review found that
  rows older than 24 hours could still be updated before being finalized.
- Inserted delayed transactions directly as `Finalized` instead of inserting them as `Active` and
  finalizing them in the same run, which created confusing audit rows.
- Replaced `DateTime.UtcNow` calls with an injected `TimeProvider` and passed a single run
  timestamp to all audit rows.
- Changed audit timestamps from local time (`DateTime.Now`) to UTC.
- Limited reactivation to `Revoked` records instead of any non-`Active` status, so `Finalized`
  records can never be reactivated.
- Replaced hard-coded mock data with anchor-relative times and configurable demo
  overrides.
- Accepted some suggestions in part and rejected others where they added complexity beyond the
  exercise scope.
