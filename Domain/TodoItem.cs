namespace Todo.Api.Domain;

public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }

    // Foreign ids owned by the Info API (different database / service)
    public int CategoryId { get; set; }
    public int PriorityId { get; set; }

    public string? Comment { get; set; }
    public int CommentCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CommentedAtUtc { get; set; }
}
