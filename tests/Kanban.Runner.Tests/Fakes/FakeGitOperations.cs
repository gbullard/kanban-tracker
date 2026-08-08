using Kanban.Runner.Git;

namespace Kanban.Runner.Tests.Fakes;

public class FakeGitOperations : IGitOperations
{
    public bool RepositoryExists { get; set; } = true;
    public bool WorkingTreeClean { get; set; } = true;
    public bool CommitWillProduceChanges { get; set; } = true;
    public DiffStat Stat { get; set; } = new(2, 30, 4);

    public List<string> CheckedOutBranches { get; } = new();
    public List<string> Excluded { get; } = new();
    public List<string> CommitMessages { get; } = new();

    public bool IsRepository(string path) => RepositoryExists;

    public Task<bool> IsWorkingTreeCleanAsync(string path, CancellationToken ct) =>
        Task.FromResult(WorkingTreeClean);

    public Task<string> GetHeadShaAsync(string path, CancellationToken ct) =>
        Task.FromResult(new string('a', 40));

    public Task EnsureExcludedAsync(string path, string pattern, CancellationToken ct)
    {
        Excluded.Add(pattern);
        return Task.CompletedTask;
    }

    public Task CheckoutBranchAsync(string path, string branch, CancellationToken ct)
    {
        CheckedOutBranches.Add(branch);
        return Task.CompletedTask;
    }

    public Task<bool> CommitAllAsync(string path, string message, CancellationToken ct)
    {
        CommitMessages.Add(message);
        return Task.FromResult(CommitWillProduceChanges);
    }

    public Task<DiffStat> GetDiffStatAsync(string path, string baseSha, CancellationToken ct) =>
        Task.FromResult(Stat);
}