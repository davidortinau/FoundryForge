using FoundryForge.Core.Models;

namespace FoundryForge.Core.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task ResetAsync(bool userConfirmed, CancellationToken cancellationToken = default);
}
