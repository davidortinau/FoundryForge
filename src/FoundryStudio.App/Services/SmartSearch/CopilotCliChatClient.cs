using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.AI;

namespace FoundryStudio.App.Services.SmartSearch;

/// <summary>
/// An <see cref="IChatClient"/> that runs the locally-installed GitHub Copilot CLI (<c>copilot -p</c>) in
/// non-interactive mode and returns its stdout. Highest-quality extraction, but slow (~15s) and it spends
/// paid Copilot credits and sends the query online — so this engine is opt-in only, never an Auto default.
/// Streaming is not supported (single-shot completion).
/// </summary>
public sealed class CopilotCliChatClient : IChatClient
{
    private readonly string _executable;

    public CopilotCliChatClient(string executable = "copilot") => _executable = executable;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var prompt = string.Join("\n\n", messages.Select(m => m.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

        var psi = new ProcessStartInfo
        {
            FileName = _executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(prompt);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start the Copilot CLI.");
        }

        process.BeginOutputReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Copilot CLI exited with code {process.ExitCode}.");
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, stdout.ToString().Trim()));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Copilot CLI engine does not support streaming.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
