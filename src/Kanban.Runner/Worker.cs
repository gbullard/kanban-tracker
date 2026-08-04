using Kanban.Runner.Options;
using Microsoft.Extensions.Options;

namespace Kanban.Runner;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly RunnerOptions _options;

    public Worker(ILogger<Worker> log, IOptions<RunnerOptions> options)
    {
        _log = log;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _log.LogInformation("Runner alive. Poll interval {Seconds}s.", _options.PollIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
    }
}