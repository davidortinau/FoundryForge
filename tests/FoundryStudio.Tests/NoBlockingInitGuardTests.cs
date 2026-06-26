using System.Runtime.CompilerServices;
using Xunit;

namespace FoundryStudio.Tests;

/// <summary>
/// KI-005 guardrail (FR-006, SC-003): synchronous blocking on a <see cref="System.Threading.Tasks.Task"/>
/// (the Foundry Local init task in particular) deadlocks the BlazorWebView dispatcher. This test scans the
/// real source under <c>src/</c> and fails the build if any blocking anti-pattern reappears. Wired into CI (T028).
/// </summary>
public class NoBlockingInitGuardTests
{
    private static readonly string[] BannedPatterns =
    {
        ".GetAwaiter().GetResult()",
        ".Wait()",
        ".Result",
    };

    [Fact]
    public void Source_under_src_contains_no_synchronous_task_blocking()
    {
        var srcDir = Path.Combine(FindRepoRoot(), "src");
        Assert.True(Directory.Exists(srcDir), $"Expected src directory at {srcDir}");

        var files = Directory
            .EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(srcDir, "*.razor", SearchOption.AllDirectories))
            .Where(f => !IsGenerated(f));

        var offenders = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///"))
                {
                    continue; // ignore comments (KI references mention the patterns by name)
                }

                foreach (var pattern in BannedPatterns)
                {
                    if (line.Contains(pattern, StringComparison.Ordinal))
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {line.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Synchronous blocking on Tasks is banned (KI-005 / FR-006). Offenders:\n" + string.Join("\n", offenders));
    }

    private static bool IsGenerated(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepoRoot([CallerFilePath] string? thisFile = null)
    {
        var dir = Path.GetDirectoryName(thisFile);
        while (dir is not null && !File.Exists(Path.Combine(dir, "FoundryStudio.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new DirectoryNotFoundException($"FoundryStudio.sln not found above {thisFile}");
    }
}
