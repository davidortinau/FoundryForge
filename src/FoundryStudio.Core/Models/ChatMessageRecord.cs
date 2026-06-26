namespace FoundryStudio.Core.Models;

/// <summary>The role of a single persisted conversation turn (FL-free; mapped to MEAI ChatRole at request time).</summary>
public enum ChatTurnRole
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary>
/// One persisted turn. FL-free — never persist MEAI types directly (R3). A stopped/interrupted assistant
/// turn stores its real partial <see cref="Content"/> plus <see cref="StopReason"/>, never a clean
/// completion (Constitution III/IV, FR-027). <see cref="Metrics"/>/<see cref="StopReason"/> are assistant-only.
/// </summary>
public sealed record ChatMessageRecord(
    ChatTurnRole Role,
    string Content,
    DateTimeOffset CreatedAt,
    GenerationMetrics? Metrics = null,
    StopReason? StopReason = null);
