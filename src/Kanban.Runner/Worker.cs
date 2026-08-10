using Kanban.Core;
using Kanban.Core.Enums;
using Kanban.Runner.Options;
using Kanban.Runner.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Runner;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RunnerOptions _options;
    private readonly ILogger<Worker> _log;

    public Worker(IServiceScopeFactory scopes, IOptions<RunnerOptions> options, ILogger<Worker> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var reconciler = scope.ServiceProvider.GetRequiredService<StartupReconciler>();
            await reconciler.ReconcileAsync(stoppingToken);
        }

        _log.LogInformation("Runner polling every {Seconds}s.", _options.PollIntervalSeconds);

        var interval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // A fresh scope per card keeps the DbContext short-lived and means one
                // poisoned card cannot leave stale tracked entities behind for the next.
                await using var scope = _scopes.CreateAsyncScope();

                var queue = scope.ServiceProvider.GetRequiredService<ICardQueue>();
                var cardId = await queue.TryClaimNextAsync(stoppingToken);

                if (cardId is null)
                {
                    await Task.Delay(interval, stoppingToken);
                    continue;
                }

                var runner = scope.ServiceProvider.GetRequiredService<CardRunner>();
                await runner.ExecuteAsync(cardId.Value, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unhandled error in the poll loop. Returning card to Ready.");
                await ReturnCardToReadyAsync(ex);
                await Task.Delay(interval, stoppingToken);
            }
        }
    }

    private async Task ReturnCardToReadyAsync(Exception ex)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KanbanDbContext>();
            var card = await db.Cards.FirstOrDefaultAsync(c => c.Status == CardStatus.InProgress);
            if (card is not null)
            {
                card.Status = CardStatus.Ready;
                card.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
                _log.LogWarning("Returned card {CardId} to Ready after error.", card.Id);
            }
        }
        catch (Exception recoveryEx)
        {
            _log.LogError(recoveryEx, "Failed to return card to Ready.");
        }
    }
}