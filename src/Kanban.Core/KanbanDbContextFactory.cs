using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanban.Core;

/// <summary>Used only by the `dotnet ef` tooling at design time, never at runtime.</summary>
public class KanbanDbContextFactory : IDesignTimeDbContextFactory<KanbanDbContext>
{
    public KanbanDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KanbanDbContext>()
            .UseSqlServer("Server=localhost;Database=KanbanBoard;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new KanbanDbContext(options);
    }
}