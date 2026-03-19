using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// JSON: enums as strings (easier for UI)
builder.Services.Configure<JsonOptions>(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddRouting();
builder.Services.AddSingleton<ITodoRepository, InMemoryTodoRepository>();
builder.Services.AddSingleton<TodoService>();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/todos", (TodoService svc,
    string? query, string? priority, string? status, DateTime? dueBefore, DateTime? dueAfter, string? sort) =>
{
    var priorities = ParsePriorities(priority);
    var s = ParseStatus(status);
    var sortKey = sort?.ToLowerInvariant() switch
    {
        "priority" => SortKey.Priority,
        "due" or "duedate" => SortKey.DueDate,
        _ => SortKey.None
    };
    return Results.Ok(svc.Query(query, priorities, s, dueBefore, dueAfter, sortKey));
});

app.MapGet("/api/todos/{id:guid}", (TodoService svc, Guid id) =>
    svc.Get(id) is { } t ? Results.Ok(t) : Results.NotFound());

app.MapPost("/api/todos", (TodoService svc, TodoCreate dto) =>
{
    try
    {
        var created = svc.Add(dto);
        return Results.Created($"/api/todos/{created.Id}", created);
    }
    catch (ArgumentException ex)          { return Results.BadRequest(ex.Message); }
    catch (InvalidOperationException ex)  { return Results.Conflict(ex.Message); }
});

app.MapPut("/api/todos/{id:guid}", (TodoService svc, Guid id, TodoUpdate dto) =>
{
    try                     { return Results.Ok(svc.Update(id, dto)); }
    catch (KeyNotFoundException ex)      { return Results.NotFound(ex.Message); }
    catch (ArgumentException ex)         { return Results.BadRequest(ex.Message); }
    catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
});

app.MapPut("/api/todos/{id:guid}/complete", (TodoService svc, Guid id) =>
{
    try                     { return Results.Ok(svc.Complete(id)); }
    catch (KeyNotFoundException ex)      { return Results.NotFound(ex.Message); }
});

app.MapPut("/api/todos/{id:guid}/uncomplete", (TodoService svc, Guid id) =>
{
    try                     { return Results.Ok(svc.Uncomplete(id)); }
    catch (KeyNotFoundException ex)      { return Results.NotFound(ex.Message); }
});

app.MapDelete("/api/todos/{id:guid}", (TodoService svc, Guid id) =>
{
    try                     { svc.Delete(id); return Results.NoContent(); }
    catch (KeyNotFoundException ex)      { return Results.NotFound(ex.Message); }
    catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
});


app.MapPost("/api/todos/bulk", (TodoService svc, BulkRequest req) =>
{
    if (req.Op is not ("complete" or "delete")) return Results.BadRequest("Invalid op");
    var result = svc.Bulk(req);
    return Results.Ok(result);
});

// Dev-only test data helpers
if (app.Environment.IsDevelopment())
{
    app.MapDelete("/api/test/reset", (ITodoRepository repo) =>
    {
        repo.Reset();
        return Results.NoContent();
    });

    app.MapPost("/api/test/seed", (TodoService svc, SeedRequest req) =>
    {
        foreach (var t in req.Todos ?? [])
            svc.Add(t);
        return Results.Created("/api/todos", new { count = (req.Todos ?? []).Count });
    });
}

app.Run();

static StatusFilter ParseStatus(string? status) => status?.ToLowerInvariant() switch
{
    "active" => StatusFilter.Active,
    "completed" => StatusFilter.Completed,
    _ => StatusFilter.All
};

static Priority[]? ParsePriorities(string? csv)
{
    if (string.IsNullOrWhiteSpace(csv)) return null;
    var list = new List<Priority>();
    foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        if (Enum.TryParse<Priority>(part, true, out var p)) list.Add(p);
    return list.Count == 0 ? null : list.ToArray();
}

// ======================= Domain =======================

public enum Priority { Low = 0, Medium = 1, High = 2 }

public record Todo(
    Guid Id,
    string Title,
    bool Completed,
    bool Locked,
    Priority Priority,
    DateTime? DueDate,
    string[] Tags,
    string Notes
);

public record TodoCreate(
    string Title,
    Priority? Priority,
    DateTime? DueDate,
    string[]? Tags,
    string? Notes
);

public record TodoUpdate(
    string? Title,
    Priority? Priority,
    DateTime? DueDate,
    string[]? Tags,
    string? Notes
);

public record BulkRequest(string Op, Guid[] Ids);
public record BulkResult(int Requested, int Succeeded, int Failed);
public record SeedRequest(List<TodoCreate>? Todos);

public enum StatusFilter { All, Active, Completed }
public enum SortKey { None, Priority, DueDate }

public interface ITodoRepository
{
    IEnumerable<Todo> All();
    Todo? Get(Guid id);
    void Upsert(Todo t);
    void Remove(Guid id);
    void Reset();
    bool AnyTitle(string title, Guid? exceptId = null);
}

public sealed class InMemoryTodoRepository : ITodoRepository
{
    private readonly ConcurrentDictionary<Guid, Todo> _store = new();

    public IEnumerable<Todo> All() => _store.Values.ToArray();
    public Todo? Get(Guid id) => _store.TryGetValue(id, out var t) ? t : null;
    public void Upsert(Todo t) => _store[t.Id] = t;
    public void Remove(Guid id) => _store.TryRemove(id, out _);
    public void Reset() => _store.Clear();

    public bool AnyTitle(string title, Guid? exceptId = null)
    {
        var norm = title.ToLowerInvariant();
        return _store.Values.Any(t =>
            (!exceptId.HasValue || t.Id != exceptId.Value) &&
            t.Title.ToLowerInvariant() == norm);
    }
}

public sealed class TodoService
{
    private readonly ITodoRepository _repo;
    public TodoService(ITodoRepository repo) => _repo = repo;

    public IEnumerable<Todo> Query(string? query, Priority[]? priorities, StatusFilter status,
        DateTime? dueBefore, DateTime? dueAfter, SortKey sort)
    {
        var q = _repo.All().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var needle = query.Trim().ToLowerInvariant();
            q = q.Where(t => t.Title.ToLowerInvariant().Contains(needle) ||
                             t.Tags.Any(tag => tag.ToLowerInvariant().Contains(needle)));
        }

        if (priorities is { Length: > 0 })
            q = q.Where(t => priorities.Contains(t.Priority));

        q = status switch
        {
            StatusFilter.Active => q.Where(t => !t.Completed),
            StatusFilter.Completed => q.Where(t => t.Completed),
            _ => q
        };

        if (dueBefore.HasValue) q = q.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date <= dueBefore.Value.Date);
        if (dueAfter.HasValue)  q = q.Where(t => t.DueDate.HasValue && t.DueDate.Value.Date >= dueAfter.Value.Date);

        q = sort switch
        {
            SortKey.Priority => q.OrderByDescending(t => t.Priority).ThenBy(t => t.Title),
            SortKey.DueDate  => q.OrderBy(t => t.DueDate ?? DateTime.MaxValue).ThenBy(t => t.Title),
            _ => q.OrderBy(t => t.Title)
        };

        return q.ToArray();
    }

    public Todo Add(TodoCreate dto)
    {
        var title = NormalizeTitle(dto.Title);
        if (_repo.AnyTitle(title)) throw new InvalidOperationException("Duplicate title");

        var todo = new Todo(
            Guid.NewGuid(),
            title,
            Completed: false,
            Locked: false,
            Priority: dto.Priority ?? Priority.Medium,
            DueDate: dto.DueDate,
            Tags: CleanTags(dto.Tags),
            Notes: TrimMax(dto.Notes, 500)
        );
        _repo.Upsert(todo);
        return todo;
    }

    public Todo Update(Guid id, TodoUpdate dto)
    {
        var cur = _repo.Get(id) ?? throw new KeyNotFoundException("Todo not found");

        var title = dto.Title is null ? cur.Title : NormalizeTitle(dto.Title);
        if (!string.Equals(title, cur.Title, StringComparison.OrdinalIgnoreCase) && _repo.AnyTitle(title, id))
            throw new InvalidOperationException("Duplicate title");

        var updated = cur with
        {
            Title = title,
            Priority = dto.Priority ?? cur.Priority,
            DueDate = dto.DueDate ?? cur.DueDate,
            Tags = dto.Tags is null ? cur.Tags : CleanTags(dto.Tags),
            Notes = dto.Notes is null ? cur.Notes : TrimMax(dto.Notes, 500)
        };

        _repo.Upsert(updated);
        return updated;
    }

    public Todo Complete(Guid id)
    {
        var t = _repo.Get(id) ?? throw new KeyNotFoundException("Todo not found");
        if (t.Completed) return t;
        var updated = t with { Completed = true };
        _repo.Upsert(updated);
        return updated;
    }

    public Todo Uncomplete(Guid id)
    {
        var t = _repo.Get(id) ?? throw new KeyNotFoundException("Todo not found");
        if (!t.Completed) return t;
        var updated = t with { Completed = false };
        _repo.Upsert(updated);
        return updated;
    }

    public void Delete(Guid id)
    {
        var t = _repo.Get(id) ?? throw new KeyNotFoundException("Todo not found");
        if (t.Locked) throw new InvalidOperationException("Locked item cannot be deleted");
        _repo.Remove(id);
    }

    public BulkResult Bulk(BulkRequest req)
    {
        var requested = req.Ids?.Length ?? 0;
        var ok = 0; var failed = 0;

        foreach (var id in req.Ids ?? Array.Empty<Guid>())
        {
            try
            {
                if (req.Op == "complete") Complete(id);
                else if (req.Op == "delete") Delete(id);
                ok++;
            }
            catch { failed++; }
        }
        return new BulkResult(requested, ok, failed);
    }

    public Todo? Get(Guid id) => _repo.Get(id);

    // helpers
    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required");
        var t = title.Trim();
        if (t.Length > 100) throw new ArgumentException("Title too long");
        return t;
    }

    private static readonly Regex TagChars = new(@"[^a-zA-Z0-9\-_]+", RegexOptions.Compiled);

    private static string[] CleanTags(string[]? tags)
    {
        // Normalize instead of throwing:
        // - strip leading '#'
        // - replace invalid chars with '-'
        // - trim '-'/'_' at ends
        // - cap to 20 chars
        // - max 5 tags
        return (tags ?? Array.Empty<string>())
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .Select(s => s.StartsWith("#") ? s[1..] : s)
            .Select(s => TagChars.Replace(s, "-"))
            .Select(s => s.Trim('-', '_'))
            .Where(s => s.Length > 0)
            .Select(s => s.Length > 20 ? s[..20] : s)
            .Take(5)
            .ToArray();
    }


    private static string TrimMax(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max];
}
