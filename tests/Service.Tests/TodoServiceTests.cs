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
    public void Add_EmptyTitle_Throws()
    {
        var svc = NewSvc(out _);

        Action empty = () => svc.Add(new TodoCreate("", null, null, null, null));
        empty.Should().Throw<ArgumentException>().WithMessage("*Title required*");
    }

    [Fact]
    public void Add_WhitespaceOnlyTitle_Throws()
    {
        var svc = NewSvc(out _);

        Action empty = () => svc.Add(new TodoCreate(" ", null, null, null, null));
        empty.Should().Throw<ArgumentException>().WithMessage("*Title required*");
    }

    [Fact]
    public void Add_TitleExactly100Chars_Succeeds()
    {
        var svc = NewSvc(out _);

        var title = new string('a', 100);
        var result = svc.Add(new TodoCreate(title, null, null, null, null));
        result.Title.Should().Be(title);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public void Add_And_Get_RoundTrip()
    {
        var svc = NewSvc(out _);

        var t = svc.Add(new TodoCreate("Find me", null, null, null, null));

        var result = svc.Get(t.Id);

        result.Should().BeEquivalentTo(t);
    }

    [Fact]
    public void Update_NullTitle_KeepsExisting()
    {
        var svc = NewSvc(out _);

        var a = svc.Add(new TodoCreate("Original", null, null, null, null));

        var b = svc.Update(a.Id, new TodoUpdate(Title: null, Priority: null, DueDate: null, Tags: null, Notes: null));
        b.Title.Should().Be("Original");
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
    public void Update_SameTitle_DoesNotConflict()
    {
        var svc = NewSvc(out _);
        var a = svc.Add(new TodoCreate("A", null, null, null, null));

        var b = svc.Update(a.Id, new TodoUpdate(Title: "A", Priority: null, DueDate: null, Tags: null, Notes: null));
        b.Title.Should().Be("A");
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
        created.Tags.Should().Contain("looooooooooooooooooo"); // trimmed to 20
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
    public void Query_NullDueDate_SortsToBottom()
    {
        var svc = NewSvc(out _);
        var today = DateTime.Today;

        svc.Add(new TodoCreate("Low old", Priority.Low,  today.AddDays(-1), new[] { "d1" }, null));
        svc.Add(new TodoCreate("High soon", Priority.High, today.AddDays(2), new[] { "d2" }, null));
        svc.Add(new TodoCreate("Medium none", Priority.Medium, null, null, null));

        var res = svc.Query(query: null,
                            priorities: null,
                            status: StatusFilter.All,
                            dueBefore: null,
                            dueAfter: null,
                            sort: SortKey.DueDate).ToList();

        res.Last().Title.Should().Be("Medium none");
    }

    [Fact]
    public void Query_DueBeforeAndDueAfter_FiltersCorrectly()
    {
        var svc = NewSvc(out _);
        var today = DateTime.Today;

        svc.Add(new TodoCreate("Way past due",  null, today.AddDays(-10), null, null));
        svc.Add(new TodoCreate("Just past due", null, today.AddDays(-1),  null, null));
        svc.Add(new TodoCreate("Due soon",      null, today.AddDays(1),   null, null));
        svc.Add(new TodoCreate("Way future",    null, today.AddDays(10),  null, null));

        var res = svc.Query(query: null,
                            priorities: null,
                            status: StatusFilter.All,
                            dueBefore: today.AddDays(2),
                            dueAfter: today.AddDays(-2),
                            sort: SortKey.DueDate).ToList();

        res.Select(t => t.Title).Should().Equal("Just past due", "Due soon");
    }

    [Fact]
    public void Query_SearchByTitle_ReturnsMatch()
    {
        var svc = NewSvc(out _);

        svc.Add(new TodoCreate("Buy groceries", null, null, null, null));
        svc.Add(new TodoCreate("Pay bills", null, null, null, null));

        var results = svc.Query("grocer", null, StatusFilter.All, null, null, SortKey.None).ToList();
        results.Should().ContainSingle(t => t.Title == "Buy groceries");
    }

    [Fact]
    public void Query_SearchByTag_ReturnsMatch()
    {
        var svc = NewSvc(out _);

        svc.Add(new TodoCreate("Buy groceries", null, null, new[] { "food", "shopping" }, null));
        svc.Add(new TodoCreate("Pay bills", null, null, new[] { "finance" }, null));

        var results = svc.Query("food", null, StatusFilter.All, null, null, SortKey.None).ToList();
        results.Should().ContainSingle(t => t.Title == "Buy groceries");
    }

    [Fact]
    public void Query_SortByPriority_OrdersHighToLow()
    {
        var svc = NewSvc(out _);

        svc.Add(new TodoCreate("Low Task", Priority.Low, null, null, null));
        svc.Add(new TodoCreate("Medium Task", Priority.Medium, null, null, null));
        svc.Add(new TodoCreate("High Task", Priority.High, null, null, null));

        var results = svc.Query(null, null, StatusFilter.All, null, null, SortKey.Priority).ToList();
        results.Select(t => t.Priority).Should().Equal(Priority.High, Priority.Medium, Priority.Low);
    }

    [Fact]
    public void Query_AllPrioritiesSelected_BehavesLikeNoFilter()
    {
        var svc = NewSvc(out _);

        svc.Add(new TodoCreate("Low Task", Priority.Low, null, null, null));
        svc.Add(new TodoCreate("Medium Task", Priority.Medium, null, null, null));
        svc.Add(new TodoCreate("High Task", Priority.High, null, null, null));

        var withNull = svc.Query(null, null, StatusFilter.All, null, null, SortKey.None).ToList();
        var withAll  = svc.Query(null, new[] { Priority.Low, Priority.Medium, Priority.High }, StatusFilter.All, null, null, SortKey.None).ToList();

        withAll.Select(t => t.Title).Should().BeEquivalentTo(withNull.Select(t => t.Title));
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
    public void Bulk_Complete_And_Delete_AllSucceed_CountsCorrect()
    {
        var svc = NewSvc(out _);
        var a = svc.Add(new TodoCreate("A", null, null, null, null));
        var b = svc.Add(new TodoCreate("B", null, null, null, null));

        var result = svc.Bulk(new BulkRequest("complete", new[] { a.Id, b.Id }));
        result.Requested.Should().Be(2);
        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(0);

        var c = svc.Add(new TodoCreate("C", null, null, null, null));
        var d = svc.Add(new TodoCreate("D", null, null, null, null));

        var result2 = svc.Bulk(new BulkRequest("delete", new[] { c.Id, d.Id }));
        result2.Requested.Should().Be(2);
        result2.Succeeded.Should().Be(2);
        result2.Failed.Should().Be(0);
    }

    [Fact]
    public void Bulk_Complete_And_Delete_AllFail_CountsCorrect()
    {
        var svc = NewSvc(out _);

        var result = svc.Bulk(new BulkRequest("complete", new[] { Guid.NewGuid(), Guid.NewGuid() }));
        result.Requested.Should().Be(2);
        result.Succeeded.Should().Be(0);
        result.Failed.Should().Be(2);

        var result2 = svc.Bulk(new BulkRequest("delete", new[] { Guid.NewGuid(), Guid.NewGuid() }));
        result2.Requested.Should().Be(2);
        result2.Succeeded.Should().Be(0);
        result2.Failed.Should().Be(2);
    }


    [Fact]
    public void Complete_AlreadyCompleted_IsIdempotent()
    {
        var svc = NewSvc(out _);
        var t = svc.Add(new TodoCreate("Task", null, null, null, null));
        svc.Complete(t.Id);

        var result = svc.Complete(t.Id);
        result.Completed.Should().BeTrue();
    }

    [Fact]
    public void Uncomplete_AlreadyActive_IsIdempotent()
    {
        var svc = NewSvc(out _);
        var t = svc.Add(new TodoCreate("Task", null, null, null, null));

        var result = svc.Uncomplete(t.Id);
        result.Completed.Should().BeFalse();
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

    [Fact]
    [Trait("Category", "Smoke")]
    public void Delete_Succeeds_And_IsGone()
    {
        var svc = NewSvc(out _);

        var t = svc.Add(new TodoCreate("delete me", null, null, null, null));
        svc.Delete(t.Id);
        var result = svc.Get(t.Id);

        result.Should().BeNull();
    }

    [Fact]
    public void Add_Notes_StoredAndTrimmedTo500()
    {
        var svc = NewSvc(out _);
        var note666 = "Kramer: So how we doin' on time? George: We're perfect. I timed this out so we would pull up at the terminal *exactly* 17 minutes after their flight is supposed to land. That gives them just enough time to get off the plane, pick up their bags and be walking *out* of the terminal as we roll up. I tell you, it's a thing of beauty. I can not express to you the feeling I get from a perfect airport pickup. Um, George... Did you say 'perfect'? George: What's going on? What are you doing? The Long Island Expressway? What are you getting on the Long Island Expressway for? Do you know what the traffic will be like? This is a suicide mission! Kramer: Will you relax?!";
        var res = svc.Add(new TodoCreate("I have a note", null, null, null, note666));

        res.Notes.Length.Should().Be(500);
    }

    [Fact]
    public void Update_Notes_UpdatesCorrectly()
    {
        var svc = NewSvc(out _);
        var note = "Original Note";
        var t = svc.Add(new TodoCreate("I have a note", null, null, null, note));
        var newNote = "New Note";
        var res = svc.Update(t.Id, new TodoUpdate("I have a note", null, null, null, newNote));

        res.Notes.Should().Be(newNote);
    }

    [Fact]
    public void Update_NullNotes_KeepsExisting()
    {
        var svc = NewSvc(out _);
        var note = "Original Note";
        var t = svc.Add(new TodoCreate("I have a note", null, null, null, note));
        var res = svc.Update(t.Id, new TodoUpdate("I have a note", null, null, null, null));

        res.Notes.Should().Be(note);
    }
}
