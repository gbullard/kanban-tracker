using Kanban.Runner.Git;
using Xunit;

namespace Kanban.Runner.Tests;

public class GitCliTests : IDisposable
{
    private readonly string _repo;
    private readonly GitCli _git = new();

    public GitCliTests()
    {
        _repo = Path.Combine(Path.GetTempPath(), "kanban-git-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_repo);

        Run("init -q");
        Run("config user.email test@example.com");
        Run("config user.name Test");
        File.WriteAllText(Path.Combine(_repo, "README.md"), "initial\n");
        Run("add -A");
        Run("commit -q -m initial");
    }

    private void Run(string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git", args)
        {
            WorkingDirectory = _repo,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = System.Diagnostics.Process.Start(psi)!;
        p.WaitForExit();
        Assert.True(p.ExitCode == 0, $"git {args} failed: {p.StandardError.ReadToEnd()}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_repo, recursive: true); } catch { /* best effort on Windows */ }
    }

    [Fact]
    public void IsRepository_is_true_for_a_repo_and_false_for_a_plain_directory()
    {
        Assert.True(_git.IsRepository(_repo));

        var plain = Path.Combine(Path.GetTempPath(), "kanban-plain-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(plain);
        try
        {
            Assert.False(_git.IsRepository(plain));
        }
        finally
        {
            Directory.Delete(plain, recursive: true);
        }
    }

    [Fact]
    public async Task IsWorkingTreeCleanAsync_detects_an_untracked_file()
    {
        Assert.True(await _git.IsWorkingTreeCleanAsync(_repo, default));

        File.WriteAllText(Path.Combine(_repo, "stray.txt"), "x");

        Assert.False(await _git.IsWorkingTreeCleanAsync(_repo, default));
    }

    [Fact]
    public async Task GetHeadShaAsync_returns_a_full_sha()
    {
        var sha = await _git.GetHeadShaAsync(_repo, default);

        Assert.Equal(40, sha.Length);
        Assert.All(sha, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public async Task EnsureExcludedAsync_is_idempotent_and_actually_excludes()
    {
        await _git.EnsureExcludedAsync(_repo, ".kanban/", default);
        await _git.EnsureExcludedAsync(_repo, ".kanban/", default);

        var exclude = File.ReadAllLines(Path.Combine(_repo, ".git", "info", "exclude"));
        Assert.Single(exclude.Where(l => l.Trim() == ".kanban/"));

        Directory.CreateDirectory(Path.Combine(_repo, ".kanban"));
        File.WriteAllText(Path.Combine(_repo, ".kanban", "result.json"), "{}");

        Assert.True(await _git.IsWorkingTreeCleanAsync(_repo, default));
    }

    [Fact]
    public async Task CheckoutBranchAsync_creates_the_branch_and_can_return_to_it()
    {
        await _git.CheckoutBranchAsync(_repo, "card/1-thing", default);
        File.WriteAllText(Path.Combine(_repo, "a.txt"), "a");
        await _git.CommitAllAsync(_repo, "work", default);

        // Checking the same branch out again must succeed — this is the rework path.
        await _git.CheckoutBranchAsync(_repo, "card/1-thing", default);

        Assert.True(File.Exists(Path.Combine(_repo, "a.txt")));
    }

    [Fact]
    public async Task CommitAllAsync_returns_false_when_there_is_nothing_to_commit()
    {
        Assert.False(await _git.CommitAllAsync(_repo, "nothing", default));
    }

    [Fact]
    public async Task CommitAllAsync_returns_true_and_commits_new_files()
    {
        File.WriteAllText(Path.Combine(_repo, "new.txt"), "hello");

        Assert.True(await _git.CommitAllAsync(_repo, "add new.txt", default));
        Assert.True(await _git.IsWorkingTreeCleanAsync(_repo, default));
    }

    [Fact]
    public async Task GetDiffStatAsync_counts_files_and_lines_since_the_base()
    {
        var baseSha = await _git.GetHeadShaAsync(_repo, default);

        File.WriteAllText(Path.Combine(_repo, "one.txt"), "1\n2\n");
        File.WriteAllText(Path.Combine(_repo, "two.txt"), "3\n");
        await _git.CommitAllAsync(_repo, "two files", default);

        var stat = await _git.GetDiffStatAsync(_repo, baseSha, default);

        Assert.Equal(2, stat.FilesChanged);
        Assert.Equal(3, stat.Insertions);
        Assert.Equal(0, stat.Deletions);
    }

    [Fact]
    public async Task GetDiffStatAsync_returns_empty_when_nothing_changed()
    {
        var baseSha = await _git.GetHeadShaAsync(_repo, default);

        Assert.Equal(DiffStat.Empty, await _git.GetDiffStatAsync(_repo, baseSha, default));
    }
}