using Kanban.Core;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Runner.Persistence;

/// <summary>
/// The board is the queue. A single UPDATE with OUTPUT performs the claim, so the read and
/// the write cannot be separated by another consumer. READPAST means a second runner would
/// skip a locked row rather than block on it.
/// </summary>
public class SqlCardQueue : ICardQueue
{
    private const string ClaimSql = """
        UPDATE TOP (1) c
        SET    c.Status     = 'InProgress',
               c.Outcome    = NULL,
               c.UpdatedUtc = SYSUTCDATETIME()
        OUTPUT inserted.Id
        FROM   Cards AS c WITH (ROWLOCK, READPAST)
        WHERE  c.Id = (
                   SELECT TOP (1) c2.Id
                   FROM   Cards AS c2 WITH (ROWLOCK, READPAST)
                   WHERE  c2.Status = 'Ready'
                   ORDER  BY c2.Position, c2.Id
               );
        """;

    private readonly KanbanDbContext _db;

    public SqlCardQueue(KanbanDbContext db) => _db = db;

    public async Task<int?> TryClaimNextAsync(CancellationToken ct)
    {
        var ids = await _db.Database
            .SqlQueryRaw<int>(ClaimSql)
            .ToListAsync(ct);

        return ids.Count > 0 ? ids[0] : null;
    }
}