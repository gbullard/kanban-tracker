using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanban.Core;

/// <summary>Used only by the `dotnet ef` tooling at design time, never at runtime.</summary>
public class KanbanDbContextFactory : IDesignTimeDbContextFactory<KanbanDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost;Database=KanbanBoard;Trusted_Connection=True;TrustServerCertificate=True";

    public KanbanDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("KANBAN_CONNECTION_STRING")
                               ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<KanbanDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new KanbanDbContext(options);
    }
}