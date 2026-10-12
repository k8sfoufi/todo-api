using Microsoft.EntityFrameworkCore;
using Todo.Api.Domain;

namespace Todo.Api.Data;

public class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> Todos => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<TodoItem>(e =>
        {
            e.ToTable("todos");
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Comment).HasMaxLength(500);
            e.HasIndex(x => x.IsDone);
        });
    }
}
