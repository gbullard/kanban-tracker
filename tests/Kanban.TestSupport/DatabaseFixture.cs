using Kanban.Core;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kanban.TestSupport;

/// <summary>
/// Tests run against a real SQL Server database because the board's behaviour depends on
/// SQL Server semantics (string conversions, transactions, and in Phase 2 the READPAST
/// claim query). An in-memory provider would test a different database than we ship.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly string _connectionString;

    public string ConnectionString => _connectionString;

    public DatabaseFixture()
    {
        // Each test assembly gets its own database so the two projects never collide.
        var suffix = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name?.Replace(".", "_")
                     ?? "Default";
        _connectionString =
            $"Server=localhost;Database=KanbanBoard_Test_{suffix};Trusted_Connection=True;TrustServerCertificate=True";
    }

    public KanbanDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KanbanDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        return new KanbanDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM RunLogLines; DELETE FROM Runs; DELETE FROM CardNotes; DELETE FROM Cards; DELETE FROM Projects;");
    }

    public Task DisposeAsync() => Task.CompletedTask;
}