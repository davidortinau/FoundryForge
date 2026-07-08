namespace FoundryForge.Core.Models;

/// <summary>
/// Honest capability set derived from FL metadata (M2, R2). Each flag means "FL declared it"; absence means
/// "not declared" (rendered as "not reported", never asserted as absent). <see cref="ToolCallingKnown"/>
/// distinguishes "tools: no" from "tools: unknown" because FL's SupportsToolCalling is nullable.
/// </summary>
public readonly record struct ModelCapabilities(
    bool Vision,
    bool ToolCalling,
    bool Reasoning,
    bool ToolCallingKnown);
