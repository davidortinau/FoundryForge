using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platforms.MacOS.Essentials;
using Microsoft.Maui.Platforms.MacOS.Hosting;
using FoundryStudio.App.Services;
using FoundryStudio.Core.Abstractions;
using FoundryStudio.Core.Chat;
using FoundryStudio.Core.Concurrency;
using FoundryStudio.Core.PostV1;
using FoundryStudio.Foundry;
using Microsoft.Maui.Storage;

#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
using Microsoft.Maui.DevFlow.Blazor;
#endif

namespace FoundryStudio.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiAppMacOS<App>()
            .AddMacOSBlazorWebView()
            .AddMacOSEssentials();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.RegisterFoundryStudioServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.AddMauiDevFlowAgent();
        builder.AddMauiBlazorDevFlowTools();
#endif

        return builder.Build();
    }
}

public static class FoundryStudioServiceCollectionExtensions
{
    public static IServiceCollection RegisterFoundryStudioServices(this IServiceCollection services)
    {
        // One lifecycle, one manager (Constitution V). Register the concrete singleton, then expose
        // IFoundryLifecycle as the same instance so UI gets the FL-free surface and Foundry-internal
        // services (catalog, chat) can resolve the concrete for strongly-typed manager access.
        services.AddSingleton<FoundryLifecycle>();
        services.AddSingleton<IFoundryLifecycle>(sp => sp.GetRequiredService<FoundryLifecycle>());

        // One concurrency gate backs the one manager and the future exposed server (Constitution V).
        services.AddSingleton<IModelStateGate, ModelStateGate>();

        // Catalog (concrete + interface; the chat adapter needs the concrete for strongly-typed model access).
        services.AddSingleton<FoundryCatalogService>();
        services.AddSingleton<IFoundryCatalogService>(sp => sp.GetRequiredService<FoundryCatalogService>());

        // In-process chat: thin IChatClient adapter (no loopback) behind IChatService (M4 middleware seam).
        services.AddSingleton<FoundryChatClient>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<IChatHistoryStore>(_ =>
            new FileChatHistoryStore(Path.Combine(FileSystem.AppDataDirectory, "chats")));

        // Post-v1 honest stubs keep the DI graph stable for M6 (IsSupported == false; operations throw).
        services.AddSingleton<IEmbeddingService, StubEmbeddingService>();
        services.AddSingleton<ITranscriptionService, StubTranscriptionService>();

        // Phase 5 — cross-session memory retriever: deterministic, offline, no model required.
        services.AddSingleton<ConversationMemoryRetriever>();
        // M5: the real exposed server over the single shared FoundryLocalManager (the only new FL-bound piece).
        services.AddSingleton<ILocalServerService, LocalServerService>();

        // Phase 0 — shared serving-state service (single source of truth for Dock + Serve screen).
        services.AddSingleton<ServingStateService>();
        // Phase 2 — 3-stage serve macro (Cast → Temper → Serve). Transient so each component gets
        // its own progress-event bus; the underlying catalog/state services are shared singletons.
        services.AddTransient<ServeMacroService>();
        // Phase 0 — one-time startup landing redirect tracker.
        services.AddSingleton<StartupNavigationService>();

        // Personalization (P4): on-device ~/.copilot context reader. Opt-in; default OFF.
        // READ-ONLY: never uploads, never writes, never sends data anywhere.
        services.AddSingleton<LocalContextReader>();

        // Settings: human-readable JSON in app data; consent-gated, never wiped without confirmation.
        services.AddSingleton<ISettingsService>(_ =>
        {
            var appData = FileSystem.AppDataDirectory;
            var settingsPath = Path.Combine(appData, "settings.json");
            var defaultModelCacheDirectory = Path.Combine(appData, "FoundryLocal", "models");
            return new FileSettingsService(settingsPath, defaultModelCacheDirectory);
        });

        return services;
    }
}
