using Microsoft.EntityFrameworkCore;
using Todo.Api.Data;

namespace Todo.Api.Jobs;

/// Every N minutes (default 5) writes a reminder comment on all todos that are not done.
///
/// Safe with multiple replicas: each run takes a Postgres transaction-level advisory lock,
/// so only ONE pod does the work per tick; the others skip immediately.
public class PendingTodoCommentJob(
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<PendingTodoCommentJob> logger) : BackgroundService
{
    private const long AdvisoryLockId = 874001;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Job:Enabled", true))
        {
            logger.LogInformation("PendingTodoCommentJob is disabled");
            return;
        }

        var interval = TimeSpan.FromMinutes(config.GetValue("Job:IntervalMinutes", 5));
        logger.LogInformation("PendingTodoCommentJob started, interval {Interval}", interval);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "PendingTodoCommentJob run failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var gotLock = await db.Database
            .SqlQueryRaw<bool>($"SELECT pg_try_advisory_xact_lock({AdvisoryLockId}) AS \"Value\"")
            .SingleAsync(ct);

        if (!gotLock)
        {
            logger.LogDebug("Another instance is running the job; skipping this tick");
            return;
        }

        var now = DateTime.UtcNow;
        var pending = await db.Todos.Where(t => !t.IsDone).ToListAsync(ct);

        foreach (var todo in pending)
        {
            todo.CommentCount++;
            todo.CommentedAtUtc = now;
            todo.Comment =
                $"Still pending after {Humanize(now - todo.CreatedAtUtc)} (reminder #{todo.CommentCount}, {now:HH:mm} UTC)";
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Commented {Count} pending todos", pending.Count);
    }

    private static string Humanize(TimeSpan span) => span switch
    {
        { TotalMinutes: < 1 } => "less than a minute",
        { TotalHours: < 1 } => $"{(int)span.TotalMinutes} min",
        { TotalDays: < 1 } => $"{(int)span.TotalHours} h",
        _ => $"{(int)span.TotalDays} d"
    };
}
