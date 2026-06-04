using ApiForge.Application.DTOs.Phase4;

namespace ApiForge.Application.Abstractions.Services;

public interface IAiProviderService
{
    AiProviderStatusDto GetStatus();
    Task<IReadOnlyList<string>?> GenerateSuggestionsAsync(string action, string context, CancellationToken cancellationToken);
}
