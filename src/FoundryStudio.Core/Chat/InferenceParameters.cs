using Microsoft.Extensions.AI;

namespace FoundryStudio.Core.Chat;

/// <summary>
/// The <b>only four</b> inference parameters Foundry Local actually supports (US5, R6): temperature,
/// max output tokens, top-p, frequency penalty. <c>top_k</c>/<c>min_p</c>/<c>repeat_penalty</c>/<c>seed</c>
/// are excluded <i>by construction</i> — surfacing a control FL ignores would be a fabricated capability
/// (Constitution III, FR-019). A null field leaves the corresponding <see cref="ChatOptions"/> property
/// unset so the engine default applies — never a fabricated value.
/// </summary>
public sealed record InferenceParameters(
    double? Temperature = null,
    int? MaxOutputTokens = null,
    double? TopP = null,
    double? FrequencyPenalty = null)
{
    /// <summary>Honest, conservative defaults.</summary>
    public static InferenceParameters Defaults { get; } = new(
        Temperature: 0.7,
        MaxOutputTokens: 2048,
        TopP: 1.0,
        FrequencyPenalty: 0.0);

    /// <summary>
    /// Project the four supported params (plus model id and any tools) onto a MEAI <see cref="ChatOptions"/>.
    /// Sets EXACTLY those properties and nothing for unsupported params (FR-019).
    /// </summary>
    public ChatOptions ToChatOptions(string modelId, IEnumerable<AITool>? tools = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        var options = new ChatOptions
        {
            ModelId = modelId,
            Temperature = Temperature is null ? null : (float)Temperature.Value,
            MaxOutputTokens = MaxOutputTokens,
            TopP = TopP is null ? null : (float)TopP.Value,
            FrequencyPenalty = FrequencyPenalty is null ? null : (float)FrequencyPenalty.Value
        };

        if (tools is not null)
        {
            var toolList = tools.ToList();
            if (toolList.Count > 0)
            {
                options.Tools = toolList;
            }
        }

        return options;
    }
}
