# Testing

## Prerequisites

- **SQL Server** (local instance) — tests run against a real SQL Server database, not an in-memory provider. The board's behavior depends on SQL Server semantics (string conversions, transactions, and the `READPAST` claim query).
- **Your account must have `CREATE DATABASE` and `DROP DATABASE` permissions** on the local SQL Server instance. The test fixture creates and drops a test database on every run.

### Granting permissions

If you see `Cannot drop the database 'KanbanBoard_Test_*'`:

```sql
-- Check your current permissions
SELECT permission_name FROM sys.fn_my_permissions(NULL, 'DATABASE');

-- Grant dbcreator server role (allows CREATE/DROP ANY DATABASE)
ALTER SERVER ROLE dbcreator ADD MEMBER [DOMAIN\YourAccount];
```

Or, if you lack admin rights, ask a DBA to grant `CREATE DATABASE` and `DROP DATABASE` to your login.

---

## Running tests

```powershell
# Full suite
dotnet test

# A single project
dotnet test tests/Kanban.Core.Tests
dotnet test tests/Kanban.Runner.Tests
```

The build script also runs tests as part of the `Publish` and `All` tasks:

```powershell
.\build\build.ps1 -Task Test
```

---

## Test database

Each test assembly gets its own database to prevent collisions between the two test projects running in parallel:

| Assembly | Database |
|----------|----------|
| `Kanban.Core.Tests` | `KanbanBoard_Test_Kanban_Core_Tests` |
| `Kanban.Runner.Tests` | `KanbanBoard_Test_Kanban_Runner_Tests` |

The `DatabaseFixture` in `Kanban.TestSupport`:

1. **Drops** the database if it exists (`EnsureDeletedAsync`).
2. **Creates** it fresh by running all EF Core migrations (`MigrateAsync`).
3. Between tests, `ResetAsync()` deletes all rows from every table.

This means the first test run is the slowest (migrations run). Subsequent runs in the same session reuse the database and only truncate data.

---

## Test structure

```
tests/
  Kanban.TestSupport/          Shared fixture, referenced by both test projects
    DatabaseFixture.cs         Creates/drops the test database, resets data between tests
  Kanban.Core.Tests/           38 tests
    BoardOrderingTests.cs      Position renumbering: insert, move, contiguity
    CardTransitionsTests.cs    Every permitted and forbidden transition
    BoardServiceTests.cs       Board queries, card moves, rework notes
  Kanban.Runner.Tests/         69 tests
    BranchNamingTests.cs       Branch name generation from card ID and title
    PromptComposerTests.cs     Prompt structure: task, notes, project, rules
    ResultFileParserTests.cs   Parsing valid/invalid/missing result.json
    RunClassifierTests.cs      One test per row of the classification table
    CardQueueTests.cs          Atomic claim: Ready cards, position order, single claim
    CardRunnerTests.cs         End-to-end orchestration with fakes
    WorkerTests.cs             Startup reconciliation, poll loop
```

### Runner tests use fakes

All Runner tests use `IAgentProcess` and `IGitOperations` fakes — no real Crush process is spawned, no real git commands run, no network calls. The fakes are in `tests/Kanban.Runner.Tests/Fakes/`.

---

## Expected output

A clean run produces:

```
Kanban.Core.Tests:    38 passed, 0 failed, 0 skipped
Kanban.Runner.Tests:  69 passed, 0 failed, 0 skipped

Total: 107 passed
```

---

## Common failures

| Error | Cause | Fix |
|-------|-------|-----|
| `Cannot drop the database 'KanbanBoard_Test_*'` | Your account lacks `DROP DATABASE` permission | Grant `dbcreator` server role, or ask a DBA |
| `Cannot open database 'KanbanBoard_Test_*'` | Database was manually deleted or the account changed | Run the tests — the fixture recreates it |
| `Login failed for user` | SQL Server is not using Windows auth, or the instance name is wrong | Verify the connection string in `DatabaseFixture.cs` |
| Timeout connecting to SQL Server | SQL Server is stopped, or TCP/IP is disabled | Check `services.msc` → SQL Server, and enable TCP/IP in SQL Server Configuration Manager |

---

## CI considerations

If you set up a CI agent:

1. The CI agent must run on a machine with SQL Server installed.
2. The CI agent's account must have `dbcreator` on the SQL Server instance.
3. The connection string in `DatabaseFixture.cs` uses `localhost` with Windows auth — this works for a local agent, but for a remote SQL Server you would need to modify the connection string or use a SQL login.