# TransactIngest

TransactIngest is a full-stack transaction-ingestion application that consolidates retail transaction snapshots from multiple source systems. It reconciles incoming snapshots with locally stored data, inserts new transactions, records field-level changes, handles revocation and reactivation, finalizes older transactions, and supports idempotent execution.

The project uses an ASP.NET Core 10 API, Entity Framework Core with SQLite, Clean Architecture, and an Angular 22 frontend.

## Features

- Imports transaction snapshots from three mocked source systems:
  - Store Uptown
  - Store Downtown
  - Online
- Uses `(SourceSystem, TransactionId)` as the logical unique key.
- Inserts new transactions.
- Detects and updates changed transactions.
- Records field-level audit history with UTC timestamps.
- Marks recent transactions as revoked when they disappear from a successful source snapshot.
- Finalizes transactions older than 24 hours.
- Prevents changes to finalized transactions.
- Reactivates a revoked transaction when it reappears within the 24-hour window and records the status change in audit history.
- Isolates source failures so one unavailable source does not stop other sources.
- Prevents revocation when a source snapshot cannot be retrieved.
- Supports idempotent imports without duplicate records or unnecessary audit entries.
- Provides structured application logging with `ILogger`.
- Uses centralized exception handling and Problem Details responses.
- Displays transactions in a sortable Angular table.
- Provides manual import and refresh actions with loading and error states.

## Technology Stack

### Backend

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core Code-First
- SQLite
- xUnit
- Microsoft `ILogger`

### Frontend

- Angular 22
- TypeScript
- Angular signals and computed signals
- Angular `HttpClient`
- HTML and CSS

### Solution Structure

TransactIngest/
├── TransactIngest/                 # ASP.NET Core API and startup project
├── TransactIngest.Domain/          # Entities and transaction status definitions
├── TransactIngest.Application/     # Interfaces, models, and import business logic
├── TransactIngest.Infrastructure/  # EF Core, SQLite, repositories, and mocked sources
├── TransactIngest.Tests/           # Automated tests
└── transact-ingest-ui/             # Angular frontend

### Dependency Direction

API → Application
API → Infrastructure
Infrastructure → Application
Infrastructure → Domain
Application → Domain
Tests → Application and Domain

The Application layer defines repository and source-system interfaces. Infrastructure implements those interfaces. The import logic therefore remains independent of EF Core, SQLite, and the concrete mocked sources.

## Data Model

### TransactionRecord

`TransactionRecordId` is the internal database primary key. `TransactionId` is the identifier supplied by an upstream source system.

The logical unique key is:

(SourceSystem, TransactionId)

This permits different source systems to use the same transaction identifier without creating a conflict.

Important fields include:

- SourceSystem
- TransactionId
- CardNumber
- LocationCode
- ProductName
- Amount
- TransactionTime (UTC)
- Status (`Active`, `Revoked`, or `Finalized`)
- AuditHistory

### TransactionAudit

An audit record is created for each changed field. It stores the field name, previous value, new value, and `ChangedTimeUtc`.

## Reconciliation Rules

For each source system, the import service retrieves the latest snapshot and compares it with stored transactions from that source.

1. A transaction not already stored for the source is inserted.
2. A matching transaction is compared field by field.
3. Changed values are updated and recorded in audit history.
4. An identical transaction is counted as unchanged and produces no audit record.
5. An active transaction missing from a successful snapshot is revoked when its `TransactionTime` is no more than 24 hours old.
6. A failed source does not trigger revocation because its current state is unknown.
7. A transaction older than 24 hours is finalized and becomes immutable. A transaction exactly 24 hours old remains inside the revocation window.
8. A revoked transaction that reappears within 24 hours becomes active again, and the `Revoked` to `Active` status change is audited.
9. A finalized transaction cannot be updated or reactivated.
10. Repeated imports with identical source data produce the same stored state.

## API Endpoints

### Import transactions

POST /api/import


Runs the reconciliation process and returns an import summary containing successful and failed source counts and transaction outcome counts.

### Retrieve transactions

GET /api/transactions

Returns transactions for display in the Angular application.

## Prerequisites

- .NET 10 SDK
- Visual Studio 2026 or another .NET 10-compatible development environment
- Node.js and npm compatible with Angular 22
- Angular CLI 22, if running Angular CLI commands globally
- EF Core command-line tools, if migrations are run outside Visual Studio

## Configuration

The SQLite connection string is stored in the API project's `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=transact-ingest-demo.db"
  }
}
```

The Angular development origin is allowed through the API's configured CORS policy. If the Angular port or API URL changes, update the corresponding CORS origin and Angular service URL.

## Build and Run the Backend

From the solution root:

```bash
dotnet restore
dotnet build
```

### Create the SQLite Database

The repository includes the EF Core migration files. The SQLite database file is generated locally and is not included in the repository.

From the solution root, install the EF Core command-line tool if necessary:

```bash
dotnet tool install --global dotnet-ef --version "10.*"
```

Create or update the database:

```bash
dotnet ef database update --project TransactIngest.Infrastructure --startup-project TransactIngest
```

Alternatively, in Visual Studio, open **Tools → NuGet Package Manager → Package Manager Console** and run:

```powershell
Update-Database -Project TransactIngest.Infrastructure -StartupProject TransactIngest
```

The migration creates the database schema. It does not insert the mock transaction data. After starting the API, run the import using the Angular **Import Transactions** button or:

```http
POST /api/import
```

Run the API:

```bash
dotnet run --project TransactIngest
```

The development launch profile currently uses URLs similar to:

```text
https://localhost:7089
http://localhost:5183
```

The exact ports are defined in `Properties/launchSettings.json`.

## Build and Run the Frontend

From the Angular project directory:

```bash
cd transact-ingest-ui
npm install
npm start
```

Open the URL displayed by the Angular development server. Use **Import Transactions** to run the import and **Refresh Transactions** to reload the table.

Create a production frontend build with:

```bash
npm run build
```


## Run Automated Tests

From the solution root:

```bash
dotnet test
```

The automated tests cover core reconciliation behavior, including:

- Inserting a new transaction
- Treating the same `TransactionId` from different source systems as two distinct transactions
- Preventing duplicates on repeated imports
- Updating a changed transaction and recording an audit
- Applying the exact 24-hour boundary: 23 hours 59 minutes and exactly 24 hours are revoked, while 24 hours 1 minute is finalized
- Reactivating a revoked transaction that reappears within 24 hours and recording an audit
- Isolating source failures
- Finalization and immutability behavior
- Using a fixed clock for deterministic time-based tests

## Logging and Error Handling

`ImportService` uses structured `ILogger<ImportService>` messages to record:

- Import start and completion
- Source processing
- Retrieved transaction counts
- Successful and failed sources
- New, updated, revoked, finalized, and unchanged outcomes
- Cancellation and database-save failures

Sensitive values such as card numbers are not written to logs.

Unexpected API exceptions are handled centrally by `GlobalExceptionHandler`. The server logs the exception and returns a safe HTTP 500 Problem Details response without exposing internal exception details to the frontend.

## Demo Walkthrough

Mock transaction times are generated relative to the first import after the API starts, so most transactions fall within the 24-hour window. Demo behavior can be changed through the `MockSources` section in `appsettings.json`.

1. Start the API and Angular frontend, and then click **Import Transactions**.

   Expected result: 6 new transactions—5 Active and Downtown transaction 100006 Finalized because it is 25 hours old.

2. Click **Import Transactions** again.

   Expected result: no records are added or updated, demonstrating idempotent execution.

3. In `appsettings.json`, set:

   ```json
   "MockSources": {
     "Online": {
       "AmountOverrides": {
         "100001": 119.95
       }
     },
     "Uptown": {
       "ExcludedTransactionIds": [100003]
     }
   }


### Inspect Audit History

An audit-history API and frontend view are not included. Audit records can be inspected directly in the SQLite database:

1. Open the generated `transact-ingest.db` file in **DB Browser for SQLite**.
2. Select the **Execute SQL** tab.
3. Run:

   ```sql
   SELECT *
   FROM TransactionAudits
   ORDER BY ChangedTimeUtc DESC;
   ```

Each changed field creates a separate audit row containing the transaction reference, field name, old value, new value, and UTC change time.

After completing the demo steps, the audit table should include records for:

- The amount change to Online transaction 100001
- The revocation of Uptown transaction 100003
- The reactivation of Uptown transaction 100003

If the database is locked, stop the API before opening the database file.

## Design Decisions and Assumptions

- Source-system integrations are mocked behind `ITransactionSource` so real API implementations can replace them later.
- The import operation is exposed through an API endpoint, but the same `IImportService` can later be invoked by a scheduled background process.
- A successful empty snapshot is considered authoritative. A failed snapshot is not.
- “Older than 24 hours” is interpreted strictly: exactly 24 hours remains within the revocation window, while any older transaction is eligible for finalization.
- Transaction and audit timestamps use UTC for consistent comparisons across systems.
- The SQLite database file is generated locally and should not be committed.
- Authentication and authorization are outside the scope of this exercise.
- Additional details are documented in `docs/DESIGN_DECISIONS.md`.

## Known Limitations and Tradeoffs

- Concurrent imports are not coordinated. Two simultaneous `POST /api/import` requests could attempt to insert the same transaction and cause a composite unique-index conflict. A production implementation could use a distributed lock, database lock, or import-job coordinator.

- The EF Core SQLite provider does not natively support reliable server-side ordering of `decimal` values without an additional conversion. The current application performs Amount sorting in the Angular frontend, which is appropriate for the small demo dataset.

- SQLite does not enforce configured maximum string lengths. The supplied sample value `USB-C Docking Station` contains 21 characters and exceeds the specified `< 20` ProductName limit, so the application allows ProductName values up to 100 characters.

- Each import currently loads all stored transactions for a source into memory. For a large production dataset, database queries would retrieve only transactions matching the incoming snapshot and records eligible for revocation or finalization.

- Changes from all successfully processed sources are persisted with one `SaveChangesAsync` call. EF Core treats that call atomically, so a canceled or failed save does not partially persist the import. However, a database failure prevents changes from every source in that import run from being saved.

- Mock transaction times are generated relative to the first import after the API starts. Restarting the API creates a new time reference, so importing against an existing database may produce TransactionTime updates. For a clean demonstration, reset the database after restarting the API.

## AI Usage Disclosure

AI tools were used during development: GitHub Copilot in Visual Studio (inline completions and Copilot Chat) for coding, and Claude for code review, testing strategy, and documentation review. All AI-suggested code was reviewed, modified where appropriate, compiled, and tested before inclusion.

Representative prompts, conventions, design discussions, and modifications are documented in `docs/AI_USAGE.md`.

## Development Time

- Initial estimate: 10 hours
- Actual time: 12 hours

## Future Enhancements

- Audit-history API and frontend view
- Filtering and pagination
- Authentication and authorization
- Real source-system API integrations
- Scheduled background execution
- Persistent production logging and monitoring

