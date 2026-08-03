using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Kanban.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Kanban.Core.Tests;

[Collection("database")]
public class BoardServiceTests
{
    private readonly DatabaseFixture _fixture;

    public BoardServiceTests(DatabaseFixture fixture) => _fixture = fixture;

    private async Task<(int projectId, int[] cardIds)> SeedAsync(params CardStatus[] statuses)
    {
        await _fixture.ResetAsync();
        await using var db = _fixture.CreateContext();

        var project = new Project { Name = "Demo", Path = @"C:\Repos\demo" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var cards = statuses
            .Select((s, i) => new Card
            {
                ProjectId = project.Id,
                Title = $"Card {i}",
                Status = s,
                Position = i
            })
            .ToList();

        db.Cards.AddRange(cards);
        await db.SaveChangesAsync();

        return (project.Id, cards.Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task MoveAsync_moves_a_card_from_New_to_Ready_and_renumbers_the_target_column()
    {
        var (_, ids) = await SeedAsync(CardStatus.New, CardStatus.Ready);
        await using var db = _fixture.CreateContext();
        var service = new BoardService(db);

        var result = await service.MoveAsync(ids[0], CardStatus.Ready, new[] { ids[1], ids[0] }, null, default);

        Assert.True(result.Success);
        var moved = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == ids[0]);
        var existing = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == ids[1]);
        Assert.Equal(CardStatus.Ready, moved.Status);
        Assert.Equal(1, moved.Position);
        Assert.Equal(0, existing.Position);
    }

    [Fact]
    public async Task MoveAsync_rejects_a_drag_into_InProgress()
    {
        var (_, ids) = await SeedAsync(CardStatus.Ready);
        await using var db = _fixture.CreateContext();
        var service = new BoardService(db);

        var result = await service.MoveAsync(ids[0], CardStatus.InProgress, new[] { ids[0] }, null, default);

        Assert.False(result.Success);
        Assert.Contains("not allowed", result.Error!, StringComparison.OrdinalIgnoreCase);
        var unchanged = await db.Cards.AsNoTracking().SingleAsync(c => c.Id == ids[0]);
        Assert.Equal(CardStatus.Ready, unchanged.Status);
    }

    [Fact]
    public async Task MoveAsync_requires_a_note_when_sending_a_card_back_for_rework()
    {
        var (_, ids) = await SeedAsync(CardStatus.Review);
        await using var db = _fixture.CreateContext();
        var service = new BoardService(db);

        var result = await service.MoveAsync(ids[0], CardStatus.Ready, new[] { ids[0] }, null, default);

        Assert.False(result.Success);
        Assert.Contains("note", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveAsync_records_the_rework_note_and_clears_the_outcome()
    {
        var (_, ids) = await SeedAsync(CardStatus.Review);
        await using (var seed = _fixture.CreateContext())
        {
            var c = await seed.Cards.SingleAsync(x => x.Id == ids[0]);
            c.Outcome = RunOutcome.Succeeded;
            await seed.SaveChangesAsync();
        }

        await using var db = _fixture.CreateContext();
        var service = new BoardService(db);

        var result = await service.MoveAsync(ids[0], CardStatus.Ready, new[] { ids[0] }, "Please also add logging.", default);

        Assert.True(result.Success);
        var card = await db.Cards.AsNoTracking().Include(c => c.Notes).SingleAsync(c => c.Id == ids[0]);
        Assert.Null(card.Outcome);
        var note = Assert.Single(card.Notes);
        Assert.Equal(NoteAuthor.User, note.Author);
        Assert.Equal("Please also add logging.", note.Body);
    }

    [Fact]
    public async Task GetBoardAsync_filters_by_project_when_one_is_given()
    {
        await _fixture.ResetAsync();
        await using var db = _fixture.CreateContext();

        var a = new Project { Name = "A", Path = @"C:\Repos\a" };
        var b = new Project { Name = "B", Path = @"C:\Repos\b" };
        db.Projects.AddRange(a, b);
        await db.SaveChangesAsync();
        db.Cards.AddRange(
            new Card { ProjectId = a.Id, Title = "in a", Status = CardStatus.New },
            new Card { ProjectId = b.Id, Title = "in b", Status = CardStatus.New });
        await db.SaveChangesAsync();

        var service = new BoardService(db);

        Assert.Equal(2, (await service.GetBoardAsync(null, default)).Count);
        var filtered = await service.GetBoardAsync(a.Id, default);
        Assert.Equal("in a", Assert.Single(filtered).Title);
    }
}