using System.ComponentModel.DataAnnotations;

namespace Todo.Api.Dtos;

public record CreateTodoRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    int CategoryId,
    int PriorityId);

public record UpdateTodoRequest(
    [StringLength(200, MinimumLength = 1)] string? Title,
    bool? IsDone);

public record CategoryDto(int Id, string Name, string Color);
public record PriorityDto(int Id, string Name, int Level);
public record LookupsDto(IReadOnlyList<CategoryDto> Categories, IReadOnlyList<PriorityDto> Priorities);

public record TodoDto(
    int Id,
    string Title,
    bool IsDone,
    int CategoryId,
    string? CategoryName,
    string? CategoryColor,
    int PriorityId,
    string? PriorityName,
    string? Comment,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CommentedAtUtc);
