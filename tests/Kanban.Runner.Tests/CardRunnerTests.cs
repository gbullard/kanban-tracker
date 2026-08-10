using Kanban.Core;
using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Kanban.Runner;
using Kanban.Runner.Options;
using Kanban.Runner.Tests.Fakes;
using Kanban.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kanban.Runner.Tests;

[Collection("database")]
public class CardRunnerTests : IDisposable
{
    private readonly RunnerDatabaseFixture _fixture;
    private readonly string _projectDir;

    public CardRunnerTests(RunnerDatabaseFixture fixture)
    {
        _fixture = fixture;
        _projectDir = Path.Combine(Path.GetTempPath(), "kanban-proj-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_projectDir, recursive: true); } catch { /* best effort */ }
    }

    private async Task<int> SeedCardAsync(params (NoteAuthor Author, string Body)[] notes)
    {
        await _fixture.ResetAsync();
        await using var db = _fixture.CreateContext();

        var project = new Project { Name = "Demo", Path = _projectDir };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var card = new Card
        {
            ProjectId = project.Id,
            Title = "Add user login",
            Description = "Use forms auth.",
            Status = CardStatus.InProgress,
            Position = 0
        };
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        foreach (var (author, body) in notes)
        {
            db.CardNotes.Add(new CardNote { CardId = card.Id, Author = author, Body = body });
        }
        await db.SaveChangesAsync();

        return card.Id;
    }

    private CardRunner Create(FakeAgentProcess agent, FakeGitOperations git, KanbanDbContext db) =>
        new(db, git, agent,
            Microsoft.Extensions.Options.Options.Create(new RunnerOptions { AgentTimeoutMinutes = 20 }),
            NullLogger<CardRunner>.Instance);

    [Fact]
    public async Task A_successful_run_moves_the_card_to_Review_and_marks_it_Succeeded()
    {
        var cardId = await SeedCardAsync();
        await using var db = _fixture.CreateContext();
        await Create(new FakeAgentProcess(), new FakeGitOperations(), db).ExecuteAsync(cardId, default);

        var card = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        Assert.Equal(CardStatus.Review, card.Status);
        Assert.Equal(RunOutcome.Succeeded, card.Outcome);
        Assert.Equal("card/" + cardId + "-add-user-login", card.BranchName);
    }

    [Fact]
    public async Task The_agent_summary_is_recorded_as_an_Agent_note()
    {
        var cardId = await SeedCardAsync();
        await using var db = _fixture.CreateContext();
        await Create(new FakeAgentProcess(), new FakeGitOperations(), db).ExecuteAsync(cardId, default);

        var note = await db.CardNotes.AsNoTracking()
            .Where(n => n.CardId == cardId && n.Author == NoteAuthor.Agent)
            .SingleAsync();

        Assert.Equal("Did the thing.", note.Body);
    }

    [Fact]
    public async Task Log_lines_are_persisted_in_order()
    {
        var cardId = await SeedCardAsync();
        var agent = new FakeAgentProcess { Lines = new[] { "alpha", "beta", "gamma" } };

        await using var db = _fixture.CreateContext();
        await Create(agent, new FakeGitOperations(), db).ExecuteAsync(cardId, default);

        var run = await db.Runs.AsNoTracking().SingleAsync(r => r.CardId == cardId);
        var lines = await db.RunLogLines.AsNoTracking()
            .Where(l => l.RunId == run.Id)
            .OrderBy(l => l.Seq)
            .Select(l => l.Text)
            .ToListAsync();

        Assert.Equal(new[] { "alpha", "beta", "gamma" }, lines);
    }

    [Fact]
    public async Task Notes_are_folded_into_the_prompt_in_creation_order()
    {
        var cardId = await SeedCardAsync(
            (NoteAuthor.Agent, "First attempt."),
            (NoteAuthor.User, "Please also add logging."));

        var agent = new FakeAgentProcess();
        await using var db = _fixture.CreateContext();
        await Create(agent, new FakeGitOperations(), db).ExecuteAsync(cardId, default);

        Assert.Contains("Please also add logging.", agent.ReceivedPrompt!);
        Assert.True(
            agent.ReceivedPrompt!.IndexOf("First attempt.", StringComparison.Ordinal) <
            agent.ReceivedPrompt!.IndexOf("Please also add logging.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_agent_runs_in_the_projects_directory()
    {
        var cardId = await SeedCardAsync();
        var agent = new FakeAgentProcess();

        await using var db = _fixture.CreateContext();
        await Create(agent, new FakeGitOperations(), db).ExecuteAsync(cardId, default);

        Assert.Equal(_projectDir, agent.ReceivedWorkingDirectory);
    }

    [Fact]
    public async Task A_dirty_working_tree_fails_before_the_agent_is_launched()
    {
        var cardId = await SeedCardAsync();
        var agent = new FakeAgentProcess();
        var git = new FakeGitOperations { WorkingTreeClean = false };

        await using var db = _fixture.CreateContext();
        await Create(agent, git, db).ExecuteAsync(cardId, default);

        Assert.Null(agent.ReceivedPrompt);
        var card = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        Assert.Equal(CardStatus.Review, card.Status);
        Assert.Equal(RunOutcome.Failed, card.Outcome);

        var run = await db.Runs.AsNoTracking().SingleAsync(r => r.CardId == cardId);
        Assert.Contains("not clean", run.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_path_that_is_not_a_repository_fails_before_the_agent_is_launched()
    {
        var cardId = await SeedCardAsync();
        var agent = new FakeAgentProcess();
        var git = new FakeGitOperations { RepositoryExists = false };

        await using var db = _fixture.CreateContext();
        await Create(agent, git, db).ExecuteAsync(cardId, default);

        Assert.Null(agent.ReceivedPrompt);
        var run = await db.Runs.AsNoTracking().SingleAsync(r => r.CardId == cardId);
        Assert.Contains("repository", run.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_missing_result_file_marks_the_card_Failed()
    {
        var cardId = await SeedCardAsync();
        var agent = new FakeAgentProcess { ResultFileContent = null };

        await using var db = _fixture.CreateContext();
        await Create(agent, new FakeGitOperations(), db).ExecuteAsync(cardId, default);

        var card = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        Assert.Equal(CardStatus.Review, card.Status);
        Assert.Equal(RunOutcome.Failed, card.Outcome);

        var run = await db.Runs.AsNoTracking().SingleAsync(r => r.CardId == cardId);
        Assert.Equal("agent produced no result file", run.FailureReason);
    }

    [Fact]
    public async Task A_timeout_marks_the_card_Failed()
    {
        var cardId = await SeedCardAsync();
        var agent = new FakeAgentProcess { TimedOut = true, ExitCode = -1, ResultFileContent = null };

        await using var db = _fixture.CreateContext();
        await Create(agent, new FakeGitOperations(), db).ExecuteAsync(cardId, default);

        var run = await db.Runs.AsNoTracking().SingleAsync(r => r.CardId == cardId);
        Assert.Contains("timed out", run.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_result_file_is_deleted_after_the_run()
    {
        var cardId = await SeedCardAsync();
        await using var db = _fixture.CreateContext();
        await Create(new FakeAgentProcess(), new FakeGitOperations(), db).ExecuteAsync(cardId, default);

        Assert.False(File.Exists(Path.Combine(_projectDir, ".kanban", "result.json")));
    }

    [Fact]
    public async Task The_kanban_directory_is_excluded_from_git()
    {
        var cardId = await SeedCardAsync();
        var git = new FakeGitOperations();

        await using var db = _fixture.CreateContext();
        await Create(new FakeAgentProcess(), git, db).ExecuteAsync(cardId, default);

        Assert.Contains(".kanban/", git.Excluded);
    }

    [Fact]
    public async Task Diff_statistics_are_recorded_on_the_run()
    {
        var cardId = await SeedCardAsync();
        var git = new FakeGitOperations { Stat = new Kanban.Runner.Git.DiffStat(5, 120, 7) };

        await using var db = _fixture.CreateContext();
        await Create(new FakeAgentProcess(), git, db).ExecuteAsync(cardId, default);

        var run = await db.Runs.AsNoTracking().SingleAsync(r => r.CardId == cardId);
        Assert.Equal(5, run.FilesChanged);
        Assert.Equal(120, run.Insertions);
        Assert.Equal(7, run.Deletions);
    }

    [Fact]
    public async Task A_rework_run_reuses_the_existing_branch()
    {
        var cardId = await SeedCardAsync();
        await using (var seed = _fixture.CreateContext())
        {
            var card = await seed.Cards.SingleAsync(c => c.Id == cardId);
            card.BranchName = "card/99-original-name";
            await seed.SaveChangesAsync();
        }

        var git = new FakeGitOperations();
        await using var db = _fixture.CreateContext();
        await Create(new FakeAgentProcess(), git, db).ExecuteAsync(cardId, default);

        Assert.Equal("card/99-original-name", Assert.Single(git.CheckedOutBranches));
    }
}