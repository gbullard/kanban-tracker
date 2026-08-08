using Kanban.Core;
using Kanban.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Runner;

/// <summary>
/// There is exactly one consumer, so any card still in InProgress when the Runner starts was
/// abandoned by a process that died. This replaces lease and heartbeat machinery entirely.
/// </summary>
public class StartupReconciler
{
    private const string Reason = "runner restarted mid-run";

    private readonly KanbanDbContext _db;
    private readonly ILogger<StartupReconciler> _log;

    public StartupReconciler(KanbanDbContext db, ILogger<StartupReconciler> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<int> ReconcileAsync(CancellationToken ct)
    {
        var orphans = await _db.Cards
            .Where(c => c.Status == CardStatus.InProgress)
            .ToListAsync(ct);

        if (orphans.Count == 0)
        {
            return 0;
        }

        var orphanIds = orphans.Select(c => c.Id).ToList();

        var unfinishedRuns = await _db.Runs
            .Where(r => orphanIds.Contains(r.CardId) && r.EndedUtc == null)
            .ToListAsync(ct);

        foreach (var run in unfinishedRuns)
        {
            run.EndedUtc = DateTime.UtcNow;
            run.Outcome = RunOutcome.Failed;
            run.FailureReason = Reason;
        }

        foreach (var card in orphans)
        {
            card.Status = CardStatus.Review;
            card.Outcome = RunOutcome.Failed;
            card.UpdatedUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        _log.LogWarning("Reconciled {Count} card(s) abandoned by a previous Runner process.", orphans.Count);

        return orphans.Count;
    }
}