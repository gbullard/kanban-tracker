using System.Diagnostics;
using Kanban.Core.Enums;
using Kanban.Runner.Options;
using Microsoft.Extensions.Options;

namespace Kanban.Runner.Agents;

public class CrushAgentProcess : IAgentProcess
{
    private readonly RunnerOptions _options;

    public CrushAgentProcess(IOptions<RunnerOptions> options) => _options = options.Value;

    public async Task<AgentExecution> RunAsync(
        AgentRequest request,
        Func<LogStream, string, Task> onLine,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_options.PromptDirectory);

        // The prompt is written outside the project directory so it never dirties the repo.
        var promptFile = Path.Combine(_options.PromptDirectory, $"prompt-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(promptFile, request.Prompt, ct);

        try
        {
            return await ExecuteAsync(request, promptFile, onLine, ct);
        }
        finally
        {
            try { File.Delete(promptFile); } catch { /* leaving a prompt file behind is harmless */ }
        }
    }

    private async Task<AgentExecution> ExecuteAsync(
        AgentRequest request,
        string promptFile,
        Func<LogStream, string, Task> onLine,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo(_options.AgentCommand)
        {
            Arguments = _options.AgentArgumentTemplate.Replace("{promptFile}", promptFile),
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = _options.AgentPromptViaStdin,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // A single lock serialises the two reader threads so log lines keep their
        // real interleaving and the sequence numbers assigned downstream are stable.
        var writeLock = new SemaphoreSlim(1, 1);

        async void Handle(LogStream stream, string? text)
        {
            if (text is null) return;

            await writeLock.WaitAsync(CancellationToken.None);
            try
            {
                await onLine(stream, text);
            }
            finally
            {
                writeLock.Release();
            }
        }

        process.OutputDataReceived += (_, e) => Handle(LogStream.Stdout, e.Data);
        process.ErrorDataReceived += (_, e) => Handle(LogStream.Stderr, e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (_options.AgentPromptViaStdin)
        {
            await process.StandardInput.WriteAsync(request.Prompt);
            process.StandardInput.Close();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(_options.AgentTimeoutMinutes));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            KillTree(process);
            return new AgentExecution(-1, TimedOut: true);
        }

        // WaitForExitAsync can return before the async readers have drained. The
        // synchronous overload flushes them.
        process.WaitForExit();

        return new AgentExecution(process.ExitCode, TimedOut: false);
    }

    private static void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // Exited between the check and the kill. Nothing to do.
        }
    }
}