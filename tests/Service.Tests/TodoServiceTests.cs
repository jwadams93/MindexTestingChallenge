#nullable enable
using System;
using System.Linq;
using FluentAssertions;
using Xunit;

public class TodoServiceTests
{
    private static TodoService NewSvc(out ITodoRepository repo)
    {
        repo = new InMemoryTodoRepository();
        return new TodoService(repo);
    }

    [Fact]
    public void Add_Valid_MinimumFields()
    {
        var svc = NewSvc(out _);

        var created = svc.Add(new TodoCreate(
            Title: "Write tests",
            Priority: null,
            DueDate: null,
            Tags: null,
            Notes: null));

        created.Id.Should().NotBeEmpty();
        created.Title.Should().Be("Write tests");
        created.Completed.Should().BeFalse();
        created.Priority.Should().Be(Priority.Medium);
    }

    [Fact]
    public void Add_TrimsTitle_AndRejectsTooLong()
    {
        var svc = NewSvc(out _);

        var t = svc.Add(new TodoCreate("  Trim me  ", null, null, null, null));
        t.Title.Should().Be("Trim me");

        Action tooLong = () => svc.Add(new TodoCreate(new string('a', 101), null, null, null, null));
        tooLong.Should().Throw<ArgumentException>().WithMessage("*Title too long*");
    }

    [Fact]
    public void Add_DuplicateTitle_IsConflict()
    {
        var svc = NewSvc(out _);
        svc.Add(new TodoCreate("Buy milk", null, null, null, null));
        Action dup = () => svc.Add(new TodoCreate("buy MILK", null, null, null, null));
        dup.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate title*");
    }

    [Fact]
    public void Update_TitleToDuplicate_IsConflict()
    {
        var svc = NewSvc(out _);
        var a = svc.Add(new TodoCreate("A", null, null, null, null));
        svc.Add(new TodoCreate("B", null, null, null, null));

        Action act = () => svc.Update(a.Id, new TodoUpdate(Title: "b", Priority: null, DueDate: null, Tags: null, Notes: null));
        act.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate title*");
    }

    [Fact]
    public void CleanTags_SanitizesAndLimits()
    {
        var svc = NewSvc(out _);
        var created = svc.Add(new TodoCreate(
            Title: "Tags",
            Priority: Priority.High,
            DueDate: null,
            Tags: new[] { "  #alpha ", "b@d tag", "UPPER", "looooooooooooooooooooong", "ok-1", "extra" },
            Notes: null));

        created.Tags.Length.Should().Be(5);                   // max 5
        created.Tags.Should().Contain("alpha");
        created.Tags.Should().Contain("b-d-tag");             // sanitized
        created.Tags.Should().Contain("UPPER");
        created.Tags.Should().Contain("loooooooooooooooooo"); // trimmed to 20
        created.Tags.Should().Contain("ok-1");
    }

    [Fact]
    public void Query_Filter_ByPriority_Status_SortByDue()
    {
        var svc = NewSvc(out _);
        var today = DateTime.Today;

        svc.Add(new TodoCreate("Low old", Priority.Low,  today.AddDays(-1), new[] { "d1" }, null));
        svc.Add(new TodoCreate("High soon", Priority.High, today.AddDays(2), new[] { "d2" }, null));
        var mid = svc.Add(new TodoCreate("Medium none", Priority.Medium, null, null, null));
        svc.Complete(mid.Id);

        var res = svc.Query(query: null,
                            priorities: new[] { Priority.High, Priority.Medium },
                            status: StatusFilter.Active,
                            dueBefore: today.AddDays(10),
                            dueAfter: today.AddDays(-10),
                            sort: SortKey.DueDate).ToList();

        res.Select(r => r.Title).Should().Equal("High soon");
    }

    [Fact]
    public void Bulk_Complete_And_Delete_Mixed()
    {
        var svc = NewSvc(out _);
        var a = svc.Add(new TodoCreate("A", null, null, null, null));
        var b = svc.Add(new TodoCreate("B", null, null, null, null));

        var result = svc.Bulk(new BulkRequest("complete", new[] { a.Id, b.Id }));
        result.Succeeded.Should().Be(2);

        var result2 = svc.Bulk(new BulkRequest("delete", new[] { a.Id, Guid.NewGuid() }));
        result2.Requested.Should().Be(2);
        result2.Succeeded.Should().Be(1);
        result2.Failed.Should().Be(1);
    }

    [Fact]
    public void Delete_Locked_Throws()
    {
        var svc = NewSvc(out var repo);
        var t = svc.Add(new TodoCreate("Lock me", null, null, null, null));

        // make it locked in the same repo
        var locked = new Todo(t.Id, t.Title, t.Completed, Locked: true, t.Priority, t.DueDate, t.Tags, t.Notes);
        repo.Upsert(locked);

        Action act = () => svc.Delete(t.Id);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Locked*");
    }
}
