using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Kanban.Runner.Persistence;
using Kanban.Runner.Tests.Fakes;
using Kanban.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kanban.Runner.Tests;

[Collection("database")]
public class SqlCardQueueTests
{
    private readonly RunnerDatabaseFixture _fixture;

    public SqlCardQueueTests(RunnerDatabaseFixture fixture) => _fixture = fixture;

    private async Task<int[]> SeedAsync(params (CardStatus Status, int Position)[] cards)
    {
        await _fixture.ResetAsync();
        await using var db = _fixture.CreateContext();

        var project = new Project { Name = "Demo", Path = @"C:\Repos\demo" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var entities = cards
            .Select((c, i) => new Card
            {
                ProjectId = project.Id,
                Title = $"Card {i}",
                Status = c.Status,
                Position = c.Position
            })
            .ToList();

        db.Cards.AddRange(entities);
        await db.SaveChangesAsync();

        return entities.Select(e => e.Id).ToArray();
    }

    [Fact]
    public async Task Returns_null_when_nothing_is_ready()
    {
        await SeedAsync((CardStatus.New, 0), (CardStatus.Review, 0), (CardStatus.Completed, 0));
        await using var db = _fixture.CreateContext();

        Assert.Null(await new SqlCardQueue(db).TryClaimNextAsync(default));
    }

    [Fact]
    public async Task Claims_the_ready_card_with_the_lowest_position()
    {
        var ids = await SeedAsync((CardStatus.Ready, 2), (CardStatus.Ready, 0), (CardStatus.Ready, 1));
        await using var db = _fixture.CreateContext();

        var claimed = await new SqlCardQueue(db).TryClaimNextAsync(default);

        Assert.Equal(ids[1], claimed);
    }

    [Fact]
    public async Task Sets_the_claimed_card_to_InProgress()
    {
        var ids = await SeedAsync((CardStatus.Ready, 0));
        await using var db = _fixture.CreateContext();

        await new SqlCardQueue(db).TryClaimNextAsync(default);

        var card = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == ids[0]);
        Assert.Equal(CardStatus.InProgress, card.Status);
    }

    [Fact]
    public async Task Never_claims_the_same_card_twice()
    {
        await SeedAsync((CardStatus.Ready, 0), (CardStatus.Ready, 1));
        await using var db = _fixture.CreateContext();
        var queue = new SqlCardQueue(db);

        var first = await queue.TryClaimNextAsync(default);
        var second = await queue.TryClaimNextAsync(default);
        var third = await queue.TryClaimNextAsync(default);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.Null(third);
    }

    [Fact]
    public async Task Clears_a_stale_outcome_badge_when_a_reworked_card_is_claimed()
    {
        var ids = await SeedAsync((CardStatus.Ready, 0));
        await using (var seed = _fixture.CreateContext())
        {
            var card = await seed.Cards.SingleAsync(c => c.Id == ids[0]);
            card.Outcome = RunOutcome.Failed;
            await seed.SaveChangesAsync();
        }

        await using var db = _fixture.CreateContext();
        await new SqlCardQueue(db).TryClaimNextAsync(default);

        var claimed = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == ids[0]);
        Assert.Null(claimed.Outcome);
    }
}