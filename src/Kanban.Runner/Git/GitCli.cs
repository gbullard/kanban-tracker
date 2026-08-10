using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Kanban.Runner.Git;

public class GitCli : IGitOperations
{
    private record GitResult(int ExitCode, string StdOut, string StdErr);

    private static async Task<GitResult> RunAsync(string path, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("git", $"-c safe.directory={path} {arguments}")
        {
            WorkingDirectory = path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start git in {path}.");

        var stdOut = process.StandardOutput.ReadToEndAsync(ct);
        var stdErr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return new GitResult(process.ExitCode, await stdOut, await stdErr);
    }

    private static async Task<GitResult> RunOrThrowAsync(string path, string arguments, CancellationToken ct)
    {
        var result = await RunAsync(path, arguments, ct);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {arguments} failed in {path} with exit code {result.ExitCode}: {result.StdErr.Trim()}");
        }
        return result;
    }

    public bool IsRepository(string path) =>
        Directory.Exists(Path.Combine(path, ".git"));

    public async Task<bool> IsWorkingTreeCleanAsync(string path, CancellationToken ct)
    {
        var result = await RunOrThrowAsync(path, "status --porcelain", ct);
        return string.IsNullOrWhiteSpace(result.StdOut);
    }

    public async Task<string> GetHeadShaAsync(string path, CancellationToken ct)
    {
        var result = await RunOrThrowAsync(path, "rev-parse HEAD", ct);
        return result.StdOut.Trim();
    }

    public async Task EnsureExcludedAsync(string path, string pattern, CancellationToken ct)
    {
        var excludePath = Path.Combine(path, ".git", "info", "exclude");
        Directory.CreateDirectory(Path.GetDirectoryName(excludePath)!);

        if (File.Exists(excludePath))
        {
            var existing = await File.ReadAllLinesAsync(excludePath, ct);
            if (existing.Any(line => line.Trim() == pattern))
            {
                return;
            }
        }

        await File.AppendAllTextAsync(excludePath, Environment.NewLine + pattern + Environment.NewLine, ct);
    }

    public async Task CheckoutBranchAsync(string path, string branch, CancellationToken ct)
    {
        var exists = await RunAsync(path, $"rev-parse --verify --quiet refs/heads/{branch}", ct);

        // `git checkout -b` fails if the branch already exists, which is the normal rework case.
        var arguments = exists.ExitCode == 0 ? $"checkout {branch}" : $"checkout -b {branch}";
        await RunOrThrowAsync(path, arguments, ct);
    }

    public async Task<bool> CommitAllAsync(string path, string message, CancellationToken ct)
    {
        await RunOrThrowAsync(path, "add -A", ct);

        var staged = await RunAsync(path, "diff --cached --quiet", ct);
        if (staged.ExitCode == 0)
        {
            return false; // exit 0 from --quiet means no differences
        }

        var escaped = message.Replace("\"", "\\\"");
        await RunOrThrowAsync(path, $"commit -m \"{escaped}\"", ct);
        return true;
    }

    private static readonly Regex StatPattern = new(
        @"(?<files>\d+)\s+files?\s+changed(?:,\s*(?<ins>\d+)\s+insertions?\(\+\))?(?:,\s*(?<del>\d+)\s+deletions?\(-\))?",
        RegexOptions.Compiled);

    public async Task<DiffStat> GetDiffStatAsync(string path, string baseSha, CancellationToken ct)
    {
        var result = await RunOrThrowAsync(path, $"diff --shortstat {baseSha}..HEAD", ct);

        var match = StatPattern.Match(result.StdOut);
        if (!match.Success)
        {
            return DiffStat.Empty;
        }

        return new DiffStat(
            int.Parse(match.Groups["files"].Value),
            match.Groups["ins"].Success ? int.Parse(match.Groups["ins"].Value) : 0,
            match.Groups["del"].Success ? int.Parse(match.Groups["del"].Value) : 0);
    }
}