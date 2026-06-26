using FoundryStudio.Core.Models;

namespace FoundryStudio.Core.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task ResetAsync(bool userConfirmed, CancellationToken cancellationToken = default);
}
