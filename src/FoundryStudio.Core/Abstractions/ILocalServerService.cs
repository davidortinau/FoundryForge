namespace FoundryStudio.Core.Abstractions;

public interface ILocalServerService
{
    bool IsSupported { get; }

    IReadOnlyList<string> Urls { get; }

    Task<IReadOnlyList<string>> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
