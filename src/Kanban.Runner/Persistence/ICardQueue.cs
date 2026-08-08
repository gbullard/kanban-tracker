namespace Kanban.Runner.Persistence;

public interface ICardQueue
{
    /// <summary>
    /// Atomically moves the highest-priority Ready card to InProgress and returns its id,
    /// or null when there is nothing to do.
    /// </summary>
    Task<int?> TryClaimNextAsync(CancellationToken ct);
}