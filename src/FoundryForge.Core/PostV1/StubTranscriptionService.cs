using FoundryForge.Core.Abstractions;

namespace FoundryForge.Core.PostV1;

/// <summary>
/// Honest post-v1 stub (FR-013). Voice transcription (FL Whisper) lands in M6. <see cref="IsSupported"/>
/// is false and the operation throws rather than returning fake/empty text.
/// </summary>
public sealed class StubTranscriptionService : ITranscriptionService
{
    public bool IsSupported => false;

    public Task<string> TranscribeAsync(Stream audio, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Voice transcription is not implemented in v1 (planned for M6).");
}
