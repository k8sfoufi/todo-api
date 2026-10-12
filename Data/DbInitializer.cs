namespace Todo.Api.Data;

public static class DbInitializer
{
    /// Creates the schema. Retries while Postgres is still starting.
    /// For production-grade schema evolution replace EnsureCreated with EF migrations
    /// executed by a dedicated init job (so replicas don't race each other).
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TodoDbContext>>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync();
                return;
            }
            catch (Exception ex) when (attempt < 15)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
