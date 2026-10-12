using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Todo.Api.Clients;
using Todo.Api.Data;
using Todo.Api.Jobs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TodoDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Backend -> Info API (separate service with its own database)
builder.Services.AddMemoryCache();
builder.Services
    .AddHttpClient<IInfoApiClient, InfoApiClient>(client =>
    {
        var baseUrl = builder.Configuration["InfoApi:BaseUrl"]
                      ?? throw new InvalidOperationException("InfoApi:BaseUrl is not configured");
        client.BaseAddress = new Uri(baseUrl);
    })
    .AddStandardResilienceHandler(); // retry + timeout + circuit breaker

// Periodic job (every 5 min by default)
builder.Services.AddHostedService<PendingTodoCommentJob>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<TodoDbContext>("db", tags: new[] { "ready" });

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services);

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.Run();
