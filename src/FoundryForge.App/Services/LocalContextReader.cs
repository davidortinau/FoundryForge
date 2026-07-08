namespace FoundryForge.App.Services;

/// <summary>
/// The ONLY file-IO piece of the personalization stack. Reads on-device
/// <c>~/.copilot/copilot-instructions.md</c> and enumerates
/// <c>~/.copilot/skills/*/SKILL.md</c> (name + frontmatter description).
/// READ-ONLY. Never writes, never uploads, never sends to any model.
/// </summary>
public sealed class LocalContextReader
{
    /// <summary>
    /// Reads the user's local Copilot context. Every operation is guarded; missing
    /// directories and unreadable files silently yield empty results — no exception escapes.
    /// </summary>
    public async Task<LocalContextData> ReadAsync(CancellationToken cancellationToken = default)
    {
        var copilotDir = ResolveCopilotDirectory();
        if (string.IsNullOrEmpty(copilotDir))
        {
            return LocalContextData.Empty;
        }

        var instructions = await ReadInstructionsAsync(copilotDir, cancellationToken);
        var skills = await ReadSkillsAsync(copilotDir, cancellationToken);

        return new LocalContextData(instructions, skills);
    }

    private static string ResolveCopilotDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
        {
            home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        }

        if (string.IsNullOrEmpty(home))
        {
            return string.Empty;
        }

        return Path.Combine(home, ".copilot");
    }

    private static async Task<string?> ReadInstructionsAsync(
        string copilotDir, CancellationToken cancellationToken)
    {
        try
        {
            var path = Path.Combine(copilotDir, "copilot-instructions.md");
            if (!File.Exists(path))
            {
                return null;
            }

            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<(string Name, string Description)>> ReadSkillsAsync(
        string copilotDir, CancellationToken cancellationToken)
    {
        var skills = new List<(string Name, string Description)>();

        try
        {
            var skillsDir = Path.Combine(copilotDir, "skills");
            if (!Directory.Exists(skillsDir))
            {
                return skills;
            }

            foreach (var skillDir in Directory.EnumerateDirectories(skillsDir))
            {
                try
                {
                    var skillMdPath = Path.Combine(skillDir, "SKILL.md");
                    if (!File.Exists(skillMdPath))
                    {
                        continue;
                    }

                    var content = await File.ReadAllTextAsync(skillMdPath, cancellationToken)
                        .ConfigureAwait(false);
                    var skillName = Path.GetFileName(skillDir);
                    var description = ExtractFrontmatterDescription(content);
                    skills.Add((skillName, description));
                }
                catch
                {
                    // Skip individual unreadable skills without failing the whole read.
                }
            }
        }
        catch
        {
            // Skills directory unavailable — return whatever we collected.
        }

        return skills;
    }

    /// <summary>
    /// Parses a simple YAML frontmatter block (---…---) and extracts the value of
    /// the <c>description:</c> key. Returns an empty string if absent or unparseable.
    /// </summary>
    internal static string ExtractFrontmatterDescription(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var lines = content.AsSpan();
        var firstNewline = lines.IndexOf('\n');
        if (firstNewline < 0)
        {
            return string.Empty;
        }

        var firstLine = lines[..firstNewline].Trim();
        if (!firstLine.SequenceEqual("---"))
        {
            return string.Empty;
        }

        // Walk lines until we hit the closing ---
        var remaining = lines[(firstNewline + 1)..];
        while (true)
        {
            var nl = remaining.IndexOf('\n');
            var line = nl >= 0 ? remaining[..nl].TrimEnd() : remaining.TrimEnd();
            var lineStr = line.ToString();

            if (lineStr == "---" || lineStr == "..." )
            {
                break;
            }

            if (lineStr.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                var value = lineStr["description:".Length..].Trim();
                // Remove surrounding quotes if present
                if (value.Length >= 2 &&
                    ((value[0] == '"' && value[^1] == '"') ||
                     (value[0] == '\'' && value[^1] == '\'')))
                {
                    value = value[1..^1];
                }

                return value;
            }

            if (nl < 0)
            {
                break;
            }

            remaining = remaining[(nl + 1)..];
        }

        return string.Empty;
    }
}

/// <summary>
/// The on-device context data read from <c>~/.copilot</c>.
/// Immutable; passed to <see cref="FoundryForge.Core.Personalization.LocalContextProfiler"/>.
/// </summary>
public sealed record LocalContextData(
    string? InstructionsContent,
    IReadOnlyList<(string Name, string Description)> Skills)
{
    public static readonly LocalContextData Empty =
        new(null, Array.Empty<(string, string)>());

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(InstructionsContent) && Skills.Count == 0;
}
