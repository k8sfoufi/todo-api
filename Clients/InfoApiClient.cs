using Microsoft.Extensions.Caching.Memory;
using Todo.Api.Dtos;

namespace Todo.Api.Clients;

public interface IInfoApiClient
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct);
    Task<IReadOnlyList<PriorityDto>> GetPrioritiesAsync(CancellationToken ct);
}

/// Typed HttpClient for the Info API.
/// - short-lived cache to avoid calling the API on every request
/// - long-lived "stale" copy served if the Info API is down (graceful degradation)
/// - retries / timeouts / circuit breaker come from AddStandardResilienceHandler()
public class InfoApiClient(HttpClient http, IMemoryCache cache, ILogger<InfoApiClient> logger) : IInfoApiClient
{
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Stale = TimeSpan.FromHours(6);

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct) =>
        GetAsync<CategoryDto>("categories", "api/categories", ct);

    public Task<IReadOnlyList<PriorityDto>> GetPrioritiesAsync(CancellationToken ct) =>
        GetAsync<PriorityDto>("priorities", "api/priorities", ct);

    private async Task<IReadOnlyList<T>> GetAsync<T>(string key, string path, CancellationToken ct)
    {
        if (cache.TryGetValue<IReadOnlyList<T>>($"fresh:{key}", out var fresh) && fresh is not null)
            return fresh;

        try
        {
            IReadOnlyList<T> data = await http.GetFromJsonAsync<List<T>>(path, ct) ?? new List<T>();
            cache.Set($"fresh:{key}", data, Fresh);
            cache.Set($"stale:{key}", data, Stale);
            return data;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            if (cache.TryGetValue<IReadOnlyList<T>>($"stale:{key}", out var stale) && stale is not null)
            {
                logger.LogWarning(ex, "Info API call failed for {Path}; serving stale data", path);
                return stale;
            }
            throw;
        }
    }
}
