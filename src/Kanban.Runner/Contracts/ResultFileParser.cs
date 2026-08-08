using System.Text.Json;

namespace Kanban.Runner.Contracts;

/// <summary>
/// Reads the one file the agent is contractually required to produce. Anything unexpected is
/// Malformed rather than an exception: a confused agent must not be able to crash the Runner.
/// </summary>
public static class ResultFileParser
{
    public static ResultFileRead Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ResultFileRead.Missing();
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ResultFileRead.Malformed();
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return ResultFileRead.Malformed();
        }

        if (!root.TryGetProperty("status", out var statusElement) ||
            statusElement.ValueKind != JsonValueKind.String)
        {
            return ResultFileRead.Malformed();
        }

        var status = statusElement.GetString() switch
        {
            var s when string.Equals(s, "completed", StringComparison.OrdinalIgnoreCase) => AgentStatus.Completed,
            var s when string.Equals(s, "blocked", StringComparison.OrdinalIgnoreCase) => AgentStatus.Blocked,
            _ => (AgentStatus?)null
        };

        if (status is null)
        {
            return ResultFileRead.Malformed();
        }

        return ResultFileRead.Valid(new AgentResultFile(
            status.Value,
            ReadString(root, "summary"),
            ReadString(root, "blockedReason")));
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}