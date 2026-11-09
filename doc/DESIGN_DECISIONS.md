# Design Decisions and Assumptions

This document records the main architectural and business-rule decisions made for the TransactIngest coding exercise. The goal is to make the implementation easy to evaluate, maintain, test, and extend.

## 1. Architecture

The solution uses a Clean Architecture-inspired separation:

| Project | Responsibility |
| --- | --- |
| `TransactIngest.Domain` | Transaction entities, audit entities, and status definitions |
| `TransactIngest.Application` | Import workflow, interfaces, result models, and business rules |
| `TransactIngest.Infrastructure` | EF Core, SQLite, repository implementation, and mocked source systems |
| `TransactIngest` | ASP.NET Core API, dependency injection, configuration, middleware, and controllers |
| `TransactIngest.Tests` | Unit tests for reconciliation and time-dependent behavior |
| `transact-ingest-ui` | Angular user interface |

The Application layer depends on abstractions such as `ITransactionRepository` and `ITransactionSource`, rather than EF Core or concrete source integrations. Infrastructure implements those abstractions. This keeps the import rules independently testable and allows mocked sources to be replaced by real external API clients without rewriting `ImportService`.

The same `IImportService` is called by the manual API endpoint and could later be called by an external scheduler or background worker without changing the reconciliation logic. No scheduler or timer is included because scheduling is outside the exercise scope.

## 2. Transaction Identity

The upstream `TransactionId` is not globally unique. A transaction is identified by the composite logical key:

`(SourceSystem, TransactionId)`

For example, transaction 100001 from Store Uptown and transaction 100001 from Online are separate records. The database enforces this rule with a unique composite index.

`TransactionRecordId` is a separate database-generated primary key. This makes relationships, including audit-history relationships, straightforward while retaining the required source-specific identity.

The name `TransactionRecord` distinguishes the persisted entity from an incoming `SourceTransaction` and from a database transaction used to commit changes.

## 3. Source Isolation

Each source is processed independently:

1. Retrieve the source snapshot.
2. Load stored transactions for that source only.
3. Match records by `TransactionId` within that source.
4. Reconcile new, changed, unchanged, missing, revoked, and finalized records.

Source failures are logged and recorded in the import result, and processing continues with the remaining sources. An `OperationCanceledException` aborts the run, as described in §8.

A failed source is never treated as an empty snapshot. Its current state is unknown, so none of its stored transactions are revoked because of that failure. In contrast, a successfully retrieved empty snapshot is considered authoritative and can cause eligible stored transactions to be revoked or finalized.

## Mock Source Systems
Each mock source derives from InMemoryTransactionSource and is registered as a singleton. The API initializes the sources at startup, and each source calculates its sample timestamps from a UTC anchor captured during construction. Most sample transactions start within the 24-hour update window. One older sample is inserted as Finalized on the first import into a fresh database. With the same settings, repeated imports during one API run receive identical source snapshots.

Each source reads its demo settings from MockSources in appsettings.json through IOptionsMonitor. After the configuration reloads, the next import uses the new settings.
- ExcludedTransactionIds omits selected records so eligible missing transactions can be revoked.
- AmountOverrides changes selected amounts so the importer can detect and audit a source correction.
Restarting the API creates new anchors. On the next import, existing records still eligible for updates may receive a TransactionTime change and a corresponding audit entry. To repeat the first-import demo, point the API to a fresh SQLite file and apply the existing migrations.

The importer takes the source system name from ITransactionSource.SourceSystem and matches records by (SourceSystem, TransactionId). The same transaction ID can appear in multiple sources without the records being merged.

## 4. Reconciliation Rules

### New transactions

If no stored transaction has the same `(SourceSystem, TransactionId)`, the importer inserts the transaction as `Active`, or as `Finalized` if its `TransactionTime` is already more than 24 hours old. Neither insertion creates an audit row.

### Existing transactions

For a matching mutable transaction, the imported fields are compared individually. Only actual changes are applied. Every changed field creates a separate audit record containing:

- Field name
- Old value
- New value
- UTC change time

An identical record is counted as unchanged and creates no audit rows.

### Missing transactions

An active transaction missing from a successfully retrieved snapshot is handled according to its `TransactionTime`:

| Age at import time | Result |
| --- | --- |
| Future-dated (clock skew) | Left unchanged |
| Less than 24 hours old, including the current time | `Revoked` |
| Exactly 24 hours | `Revoked` |
| More than 24 hours | `Finalized` |

The phrase “older than 24 hours” is interpreted strictly. Therefore, exactly 24 hours remains inside the revocation window. The boundary is implemented with complementary comparisons so there is no gap or overlap.

Revocation and finalization of existing rows each create a status audit entry. A new row inserted directly as `Finalized` has no status transition to audit.

### Revoked transactions that reappear

A revoked transaction that reappears while it is still within the 24-hour window becomes `Active` again. The `Revoked` to `Active` transition creates an audit row.

Reactivation is limited to `Revoked` records. It does not use a general “status is not active” condition because that could accidentally reactivate a finalized transaction.

### Finalized transactions

Finalization is implemented as the optional enhancement in the requirements. An existing `Active` transaction more than 24 hours old becomes `Finalized` during reconciliation of a successful source snapshot. Finalized records are immutable.

Before applying field updates, the importer compares the stored `TransactionTime` with the cutoff from the injected clock. This protects expired records even if an earlier import failed before their status could be finalized.

### Idempotency

- The unique index on `(SourceSystem, TransactionId)` prevents duplicate stored rows.
- Field comparisons skip writes and field-level audit entries when values are identical.
- Status guards prevent repeated revocation or finalization of rows already in those states.
- One `SaveChangesAsync` call commits the staged changes together, as described in §6.

With a unique transaction ID per source snapshot, repeating a successful import with identical snapshots at the same clock time leaves the stored records and audit history unchanged. As time advances, eligible active records can cross the 24-hour cutoff and become finalized on a later import.

## 5. Time Handling

Source `TransactionTime` values and audit `ChangedTimeUtc` values are stored in UTC. UTC provides an unambiguous basis for comparisons and avoids daylight-saving and server-local-time differences.

SQLite has no dedicated date/time storage type. The stored timestamp values do not preserve `DateTime.Kind`, so SQLite can return them as `Unspecified`. The convention-based EF Core converter in `AppDbContext` restores `DateTimeKind.Utc` on mapped `DateTime` values when they are read. It preserves the stored clock value, which is assumed to already represent UTC. The default JSON serializer then emits timestamps with a `Z` suffix.

`ImportService` receives a `TimeProvider` through dependency injection. Production uses the system provider, while tests use a fixed provider. This avoids tests that change behavior depending on the date or time at which they run.

The 24-hour rule is based on elapsed time, not a calendar date:

`
cutoff = current UTC time - 24 hours
`

## 6. Data Model and Persistence

SQLite and EF Core Code-First are used as required. Migrations create the SQLite schema, indexes, and relationships. The EF Core model configures field lengths, decimal precision, and enum conversions.

Key persistence decisions include:

- `decimal(18,2)` for monetary amounts.
- Status values stored as readable strings.
- A one-to-many relationship from `TransactionRecord` to `TransactionAudit`.
- A unique index on `(SourceSystem, TransactionId)`.
- Audit rows saved with their parent transaction as part of the same EF Core change set.

`SourceSystem`, `CardNumber`, `LocationCode`, and the stored status string each have a configured maximum of 20 characters. `ProductName` has a configured maximum of 100 characters. The requirement table suggests fewer than 20 characters, but the supplied sample value “USB-C Docking Station” exceeds that limit. A larger limit also better reflects realistic product descriptions.

SQLite does not enforce the configured string lengths or decimal precision and scale. These settings document the intended limits and would be reflected in bounded string and `decimal(18,2)` columns with the SQL Server provider. EF Core stores decimal amounts as `TEXT` in this SQLite schema. The UI sorts amounts as numbers.

The repository hides EF Core details from the Application layer. The importer stages changes across all sources and persists them in one `SaveChangesAsync` call. If an error or cancellation causes the database transaction to roll back before commit, none of the staged changes are persisted. This keeps the save atomic, but a database error can discard work staged for every source in that run.

## 7. API Design

The API exposes the two required operations:

| Method and route | Purpose |
| --- | --- |
| `POST /api/import` | Manually run the import and return outcome counts |
| `GET /api/transactions` | Return transactions for frontend display |

The import response reports successful and failed sources and counts of new, updated, unchanged, revoked, and finalized transactions.

The GET endpoint returns a display model rather than exposing persistence entities directly. This keeps database relationships and internal fields separate from the API contract.

`CancellationToken` values are passed through controllers, services, source clients, repositories, and EF Core calls so canceled HTTP requests can stop unnecessary work.

## 8. Logging and Error Handling

`ImportService` uses `ILogger<ImportService>` and structured message templates. Logs include:

- Import start and completion
- Source being processed
- Number of records retrieved
- Source success or failure
- Final outcome counts

Sensitive card information is not included in log messages.

Unexpected API exceptions are handled centrally by `GlobalExceptionHandler`. The API logs internal details and returns a standard Problem Details response without exposing stack traces or implementation details to the frontend.

The importer rethrows `OperationCanceledException` so request cancellation stops the run. A future HTTP source can also throw this exception on a timeout. With the current handling, that timeout would abort the whole import.

## 9. Frontend Decisions

The Angular frontend is intentionally lightweight because the exercise emphasizes backend reconciliation. It provides:

- A transaction table with the required display fields
- Data retrieval from the API
- Manual import and refresh actions
- Loading and error states
- Import-result summary
- Client-side sorting

Angular signals hold component state, and a computed signal produces the sorted view. Sorting remains a client-side concern because the exercise uses a small mocked dataset. Server-side sorting, filtering, and pagination would be preferable for a large production dataset.

## 10. Test Strategy

The import service is tested with in-memory fake repositories and fake source implementations. This isolates business rules from SQLite and external systems and keeps tests fast.

Time-dependent tests use one fixed UTC clock. Important scenarios include:

- New transaction insertion
- Idempotent repeated import
- Changed-field update with audit creation
- Same `TransactionId` from different sources creating two records
- Source failure not causing false revocation
- Exact 24-hour boundary behavior
- Revoked transaction reactivation and audit creation
- Finalized transaction immutability

The boundary tests use 23 hours 59 minutes, exactly 24 hours, and 24 hours 1 minute. Tests far from the cutoff would not detect mistakes such as using `<` instead of `<=`.

## 11. Assumptions and Tradeoffs

- Mock sources return complete snapshots, not incremental change feeds.
- A successful snapshot is authoritative for its source.
- The importer processes repeated `TransactionId` values within a source snapshot in input order without de-duplicating them. Duplicate entries can affect outcome counts and audit history even though the unique index prevents duplicate stored rows.
- Source system names are stable identifiers.
- Field-level audit values are stored as strings to support different field types in one audit structure.
- The import runs in a single application instance for this exercise. Distributed locking and overlapping-import coordination are not implemented.
- Authentication and authorization are outside the exercise scope.
- The SQLite database is suitable for the exercise but would be reconsidered for a high-volume or multi-instance production deployment.
- Audit-history API and UI views are optional and are not required for the submitted interface.
- Persistent production log storage and monitoring are outside the exercise scope; the implementation uses the standard .NET logging abstraction so another provider can be configured later.

## 12. Possible Future Enhancements

- Real source-system HTTP clients with retry and timeout policies. Use a `catch` filter on `cancellationToken.IsCancellationRequested` to propagate request cancellation and handle upstream timeouts as source failures.
- Import-batch metadata and correlation identifiers
- Database transaction boundaries per source or per import batch
- Concurrency protection for overlapping imports
- Audit-history API and frontend view
- Server-side filtering, sorting, and pagination
- Authentication and role-based authorization
- Production database, centralized logging, metrics, and health checks
