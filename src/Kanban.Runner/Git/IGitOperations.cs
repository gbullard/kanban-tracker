namespace Kanban.Runner.Git;

/// <summary>
/// Every git command the system runs goes through here. The agent runs none of them.
/// </summary>
public interface IGitOperations
{
    bool IsRepository(string path);

    Task<bool> IsWorkingTreeCleanAsync(string path, CancellationToken ct);

    Task<string> GetHeadShaAsync(string path, CancellationToken ct);

    /// <summary>Appends a pattern to .git/info/exclude if it is not already there.</summary>
    Task EnsureExcludedAsync(string path, string pattern, CancellationToken ct);

    /// <summary>Checks the branch out, creating it from the current HEAD if it does not exist.</summary>
    Task CheckoutBranchAsync(string path, string branch, CancellationToken ct);

    /// <summary>Stages and commits everything. Returns false when there was nothing to commit.</summary>
    Task<bool> CommitAllAsync(string path, string message, CancellationToken ct);

    Task<DiffStat> GetDiffStatAsync(string path, string baseSha, CancellationToken ct);
}