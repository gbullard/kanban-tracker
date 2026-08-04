namespace Kanban.Runner.Options;

public class RunnerOptions
{
    public const string SectionName = "Runner";

    public int PollIntervalSeconds { get; set; } = 3;
    public int AgentTimeoutMinutes { get; set; } = 20;

    /// <summary>Executable to launch. See docs/crush-invocation.md.</summary>
    public string AgentCommand { get; set; } = "crush";

    /// <summary>
    /// Argument template. The token {prompt} is replaced with the composed prompt text.
    /// Ignored when AgentPromptViaStdin is true.
    /// </summary>
    public string AgentArgumentTemplate { get; set; } = "run \"{prompt}\"";

    public bool AgentPromptViaStdin { get; set; }

    public int LogFlushIntervalMs { get; set; } = 1000;

    /// <summary>Where composed prompts are written. Outside any project directory.</summary>
    public string PromptDirectory { get; set; } = @"C:\ProgramData\Kanban\prompts";
}