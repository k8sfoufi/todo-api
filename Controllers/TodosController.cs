using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Todo.Api.Clients;
using Todo.Api.Data;
using Todo.Api.Domain;
using Todo.Api.Dtos;

namespace Todo.Api.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController(TodoDbContext db, IInfoApiClient info, ILogger<TodosController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TodoDto>>> List(CancellationToken ct)
    {
        var todos = await db.Todos.AsNoTracking()
            .OrderBy(t => t.IsDone)
            .ThenByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

        // If the Info API is down we still return todos, just without category/priority names.
        var lookups = await TryGetLookupsAsync(ct);
        return todos.Select(t => Map(t, lookups)).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TodoDto>> Get(int id, CancellationToken ct)
    {
        var todo = await db.Todos.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (todo is null) return NotFound();
        return Map(todo, await TryGetLookupsAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<TodoDto>> Create(CreateTodoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError(nameof(request.Title), "Title is required.");
            return ValidationProblem(ModelState);
        }

        // Validate the referenced ids against the Info API (source of truth).
        var lookups = await TryGetLookupsAsync(ct);
        if (lookups is null)
            return Problem("The info service is unavailable. Try again in a moment.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        if (!lookups.Categories.ContainsKey(request.CategoryId))
            ModelState.AddModelError(nameof(request.CategoryId), "Unknown category.");
        if (!lookups.Priorities.ContainsKey(request.PriorityId))
            ModelState.AddModelError(nameof(request.PriorityId), "Unknown priority.");
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var todo = new TodoItem
        {
            Title = request.Title.Trim(),
            CategoryId = request.CategoryId,
            PriorityId = request.PriorityId,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Todos.Add(todo);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = todo.Id }, Map(todo, lookups));
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<TodoDto>> Update(int id, UpdateTodoRequest request, CancellationToken ct)
    {
        var todo = await db.Todos.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (todo is null) return NotFound();

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                ModelState.AddModelError(nameof(request.Title), "Title cannot be empty.");
                return ValidationProblem(ModelState);
            }
            todo.Title = request.Title.Trim();
        }

        if (request.IsDone is { } done && done != todo.IsDone)
        {
            todo.IsDone = done;
            todo.CompletedAtUtc = done ? DateTime.UtcNow : null;
        }

        await db.SaveChangesAsync(ct);
        return Map(todo, await TryGetLookupsAsync(ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await db.Todos.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    // ---------------------------------------------------------------- helpers

    private sealed record Lookups(
        Dictionary<int, CategoryDto> Categories,
        Dictionary<int, PriorityDto> Priorities);

    private async Task<Lookups?> TryGetLookupsAsync(CancellationToken ct)
    {
        try
        {
            var categories = info.GetCategoriesAsync(ct);
            var priorities = info.GetPrioritiesAsync(ct);
            await Task.WhenAll(categories, priorities);
            return new Lookups(
                categories.Result.ToDictionary(c => c.Id),
                priorities.Result.ToDictionary(p => p.Id));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not retrieve lookups from the Info API");
            return null;
        }
    }

    private static TodoDto Map(TodoItem t, Lookups? l)
    {
        var category = l is not null && l.Categories.TryGetValue(t.CategoryId, out var c) ? c : null;
        var priority = l is not null && l.Priorities.TryGetValue(t.PriorityId, out var p) ? p : null;

        return new TodoDto(
            t.Id, t.Title, t.IsDone,
            t.CategoryId, category?.Name, category?.Color,
            t.PriorityId, priority?.Name,
            t.Comment, t.CreatedAtUtc, t.CompletedAtUtc, t.CommentedAtUtc);
    }
}
