namespace FoundryForge.Core.Server;

/// <summary>One real, observed server request (never synthesized).</summary>
public sealed record RequestActivityEntry(DateTimeOffset At, string Summary);

/// <summary>How the UI should render the request log: shown with real entries, or honestly omitted.</summary>
public sealed record RequestLogView(bool Show, string? HonestNote, IReadOnlyList<RequestActivityEntry> Entries);

/// <summary>
/// Projects observed request activity into a render decision (US7, R2). If Foundry Local exposes no
/// observable activity (<c>observed == null</c>), the log is OMITTED with an honest note — never
/// fabricated. Real entries are passed through verbatim (no synthesis, reordering, or timer source).
/// Pure, dylib-free (Constitution III, FR-022/023).
/// </summary>
public static class RequestActivityProjection
{
    private const string OmittedNote =
        "Foundry Local does not expose a live request feed, so per-request activity isn't shown here.";

    public static RequestLogView Project(IReadOnlyList<RequestActivityEntry>? observed)
    {
        if (observed is null)
        {
            return new RequestLogView(Show: false, HonestNote: OmittedNote, Entries: Array.Empty<RequestActivityEntry>());
        }

        return new RequestLogView(Show: true, HonestNote: null, Entries: observed.ToList());
    }
}
