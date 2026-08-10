using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Kanban.Runner;
using Kanban.Runner.Tests.Fakes;
using Kanban.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kanban.Runner.Tests;

[Collection("database")]
public class StartupReconcilerTests
{
    private readonly RunnerDatabaseFixture _fixture;

    public StartupReconcilerTests(RunnerDatabaseFixture fixture) => _fixture = fixture;

    private async Task<int> SeedAsync(CardStatus status, bool withUnfinishedRun)
    {
        await _fixture.ResetAsync();
        await using var db = _fixture.CreateContext();

        var project = new Project { Name = "Demo", Path = @"C:\Repos\demo" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var card = new Card { ProjectId = project.Id, Title = "Stuck", Status = status, Position = 0 };
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        if (withUnfinishedRun)
        {
            db.Runs.Add(new Run
            {
                CardId = card.Id,
                BranchName = "card/1-stuck",
                BaseCommitSha = new string('a', 40)
            });
            await db.SaveChangesAsync();
        }

        return card.Id;
    }

    [Fact]
    public async Task An_orphaned_InProgress_card_is_moved_to_Review_and_marked_Failed()
    {
        var cardId = await SeedAsync(CardStatus.InProgress, withUnfinishedRun: true);
        await using var db = _fixture.CreateContext();

        var repaired = await new StartupReconciler(db, NullLogger<StartupReconciler>.Instance)
            .ReconcileAsync(default);

        Assert.Equal(1, repaired);

        var card = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        Assert.Equal(CardStatus.Review, card.Status);
        Assert.Equal(RunOutcome.Failed, card.Outcome);
    }

    [Fact]
    public async Task The_orphaned_run_is_closed_with_a_reason()
    {
        var cardId = await SeedAsync(CardStatus.InProgress, withUnfinishedRun: true);
        await using var db = _fixture.CreateContext();

        await new StartupReconciler(db, NullLogger<StartupReconciler>.Instance).ReconcileAsync(default);

        var run = await db.Runs.AsNoTracking().SingleAsync(r => r.CardId == cardId);
        Assert.NotNull(run.EndedUtc);
        Assert.Equal(RunOutcome.Failed, run.Outcome);
        Assert.Contains("restarted", run.FailureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_InProgress_card_with_no_run_at_all_is_still_repaired()
    {
        var cardId = await SeedAsync(CardStatus.InProgress, withUnfinishedRun: false);
        await using var db = _fixture.CreateContext();

        Assert.Equal(1, await new StartupReconciler(db, NullLogger<StartupReconciler>.Instance).ReconcileAsync(default));

        var card = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        Assert.Equal(CardStatus.Review, card.Status);
    }

    [Fact]
    public async Task Cards_in_other_columns_are_left_alone()
    {
        var cardId = await SeedAsync(CardStatus.Ready, withUnfinishedRun: false);
        await using var db = _fixture.CreateContext();

        Assert.Equal(0, await new StartupReconciler(db, NullLogger<StartupReconciler>.Instance).ReconcileAsync(default));

        var card = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        Assert.Equal(CardStatus.Ready, card.Status);
    }
}