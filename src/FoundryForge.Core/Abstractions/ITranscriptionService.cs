namespace FoundryForge.Core.Abstractions;

public interface ITranscriptionService
{
    bool IsSupported { get; }

    Task<string> TranscribeAsync(Stream audio, CancellationToken cancellationToken = default);
}
