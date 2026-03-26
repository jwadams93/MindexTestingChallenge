#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using TechTalk.SpecFlow;

[Binding]
public class AdvancedTodosSteps
{
    private readonly ScenarioContext _ctx;
    private IWebDriver Driver => (IWebDriver)_ctx["driver"];
    private WebDriverWait Wait => new(Driver, TimeSpan.FromSeconds(6));

    public AdvancedTodosSteps(ScenarioContext ctx) => _ctx = ctx;

    // ---------- Setup / Navigation ----------
    [Given(@"the app is running at ""(.*)""")]
    public void GivenTheAppIsRunningAt(string baseUrl) => _ctx["baseUrl"] = baseUrl.TrimEnd('/');

    [Given(@"I open the unpopulated Todos page")]
    public void GivenIOpenTheUnpopulatedTodosPage()
    {
        Driver.Navigate().GoToUrl(BaseUrl());
        Wait.Until(d => Exists(d, "[data-testid='new-title']"));
    }

    [Given(@"I open the Todos page")]
    public void GivenIOpenTheTodosPage()
    {
        Driver.Navigate().GoToUrl(BaseUrl());
        Wait.Until(d => Exists(d, "[data-testid='new-title']"));
        Wait.Until(d => d.FindElements(By.CssSelector("#list li")).Any());  
    }

    // ---------- Dev seed/reset ----------
    [Given(@"I reset data")]
    public async Task GivenIResetData() => await new TestApi(BaseUrl()).ResetAsync();

    [Given(@"I have a todo seeded titled ""(.*)""")]
    public async Task GivenIHaveATodoSeededTitled(string title)
    {
        var api = new TestApi(BaseUrl());
        await api.ResetAsync();
        await api.SeedAsync(new List<TodoCreateReq> { new(title) });
    }

    [Given(@"I seed todos:")]
    public async Task GivenISeedTodos(Table table)
    {
        var items = new List<TodoCreateReq>();
        foreach (var row in table.Rows)
        {
            var title = row.GetValueOrDefault("title") ?? row.Values.First();
            var priority = row.GetValueOrDefault("priority");
            var notes = row.GetValueOrDefault("notes");
            var tagsCsv = row.GetValueOrDefault("tags");
            string[]? tags = !string.IsNullOrWhiteSpace(tagsCsv)
                ? tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : null;

            DateTime? due = null;
            var dueStr = row.GetValueOrDefault("dueDate");
            if (!string.IsNullOrWhiteSpace(dueStr)) due = ParseRelativeOrIsoDate(dueStr!);

            items.Add(new TodoCreateReq(title!, priority, due, tags, notes));
        }
        await new TestApi(BaseUrl()).SeedAsync(items);
    }

    // ---------- Create with details ----------
    [When(@"I create a todo titled ""(.*)"" with:")]
    public void WhenICreateATodoTitledWith(string title, Table table)
    {
        string? priority = table.Get("priority");
        string? due = table.Get("dueDate");
        string? tags = table.Get("tags");
        string? notes = table.Get("notes");

        Type("#title", title);
        Select("#priority", priority ?? "Medium");
        if (!string.IsNullOrWhiteSpace(due)) SetDate("#due", ParseRelativeOrIsoDate(due!).Date);
        if (!string.IsNullOrWhiteSpace(tags)) Type("#tags", tags);
        if (!string.IsNullOrWhiteSpace(notes)) Type("#notes", notes);

        Click("[data-testid='add-btn']");
        WaitForRow(title);
    }

    // ---------- Edit (cancel prompt; use API for determinism) ----------
    [When(@"I edit ""(.*)"" to title ""(.*)"" and notes ""(.*)""")]
    public async Task WhenIEditToTitleAndNotes(string oldTitle, string newTitle, string notes)
    {
        // click Edit then dismiss any prompt
        var row = FindRow(oldTitle);
        row.FindElement(By.XPath(".//button[normalize-space()='Edit']")).Click();
        TryDismissAlert();

        var api = new TestApi(BaseUrl());
        var id = await api.TryGetIdByTitleAsync(oldTitle) ?? throw new Exception("Item not found");
        await api.UpdateAsync(id, new TodoUpdateReq(Title: newTitle, Notes: notes));

        WaitForRow(newTitle);
    }

    // ---------- Edit (accept prompt;) ----------
    [When(@"I edit ""(.*)"" to title ""(.*)""")]
    public async Task WhenIEditToTitle(string oldTitle, string newTitle)
    {
        // click Edit then dismiss any prompt
        var row = FindRow(oldTitle);
        row.FindElement(By.XPath(".//button[normalize-space()='Edit']")).Click();
        WaitForAlert();
        SendKeysToAlert(newTitle);
        AcceptAlert();
    }

    // ---------- Search / Filters / Bulk ----------
    [When(@"I search for ""(.*)"" and select all items")]
    public void WhenISearchForAndSelectAllItems(string query)
    {
        SetValue("#q", query);
        Click("#apply");
        Wait.Until(_ => Driver.FindElements(By.CssSelector("#list li")).Any());
        foreach (var cb in Driver.FindElements(By.CssSelector("input[type='checkbox'][data-id]")))
            if (!cb.Selected) cb.Click();
    }

    [When(@"I select the following todos:")]
    public void WhenISelectTheTodos(Table table)
    {
        var titlesToSelect = table.Rows.Select(r => r.Values.First().Trim()).ToList();
        Wait.Until(_ => Driver.FindElements(By.CssSelector("#list li")).Any());
        foreach (var li in Driver.FindElements(By.CssSelector("#list li")))
        {
            var label = SafeText(li.FindElement(By.CssSelector("[data-testid='todo-label']")));
            if (titlesToSelect.Contains(label.Trim()))
            {
                var cb = li.FindElement(By.CssSelector("input[type='checkbox'][data-id]"));
                if (!cb.Selected) cb.Click();
            }
        }
    }

    [When(@"I set filter Priority to ""(.*)"" and Status to ""(.*)"" expecting ""(.*)""")]
    public void WhenISetFilterPriorityToAndStatusTo(string priosCsv, string status, string expectedTitle)
    {
        foreach (var cb in Driver.FindElements(By.CssSelector(".prio")))
            if (cb.Selected) cb.Click();

        var wanted = priosCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                             .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var cb in Driver.FindElements(By.CssSelector(".prio")))
            if (wanted.Contains(cb.GetAttribute("value")) && !cb.Selected) cb.Click();

        Select("#status", status);
        Click("#apply");
        Wait.Until(driver => driver.FindElements(By.CssSelector("[data-testid='todo-label']"))
            .Any(el => SafeText(el).Contains(expectedTitle)));
    }

    [When(@"I apply bulk action ""(.*)""")]
    public void WhenIApplyBulkAction(string op)
    {
        var countBefore = Driver.FindElements(By.CssSelector("#list li")).Count;
        if (op.Equals("complete", StringComparison.OrdinalIgnoreCase))
        {
            Click("#bulk-complete");
            Wait.Until(d => d.FindElements(By.CssSelector("[data-testid='todo-label']"))
                .Any(e => SafeText(e).StartsWith("✅ ")));
        } 
        else
        {
            Click("#bulk-delete");
            Wait.Until(d => d.FindElements(By.CssSelector("#list-li")).Count < countBefore);
        }
    }

    // ---------- Sort ----------
    [When(@"I sort by ""(.*)""")]
    public void WhenISortBy(string sortType)
    {
        //Need to select "sort" and then check out how we handle the other drop down and see how I can best 
        // select the sort type 
        // It looks like Sort: Title has no value by which to select it?
        Select("#sort", sortType );
        Click("#apply");
        Wait.Until(driver => driver.FindElements(By.CssSelector("#sort"))
            .Any(el => SafeText(el).Contains(sortType)));
    }

    // ---------- Assertions ----------
    [Then(@"both items should appear completed")]
    public void ThenBothItemsShouldAppearCompleted()
    {
        var labels = Driver.FindElements(By.CssSelector("[data-testid='todo-label']")).Select(SafeText).ToList();
        labels.Should().NotBeEmpty();
        labels.Should().OnlyContain(t => t.StartsWith("✅ "));
    }

    [Then(@"""(.*)"" should appear completed")]
    public void ThenTitleShouldAppearCompleted(string title)
    {
        var labels = Driver.FindElements(By.CssSelector("[data-testid='todo-label']")).Select(SafeText).ToList();
        labels.Any(t => t.StartsWith("✅ ") && t.EndsWith(title)).Should().BeTrue();
    }

    [Then(@"I should see ""(.*)"" with priority ""(.*)""")]
    public void ThenIShouldSeeWithPriority(string title, string priority)
    {
        var row = FindRow(title);
        var badge = row.FindElement(By.CssSelector(".badge"));
        badge.Text.Trim().Should().Be(priority);
    }

    [Then(@"it should show a due date within (\d+) days")]
    public void ThenItShouldShowADueDateWithinDays(int days)
    {
        var chips = Driver.FindElements(By.CssSelector(".chip")).Select(SafeText).ToList();
        chips.Should().NotBeEmpty();
        var ok = chips.Any(c =>
        {
            if (DateTime.TryParse(c.Split('(')[0].Trim(), out var d))
            {
                var diff = (d.Date - DateTime.Today).TotalDays;
                return diff >= 0 && diff <= days;
            }
            return false;
        });
        ok.Should().BeTrue();
    }

    [Then(@"I should see exactly:")]
    public void ThenIShouldSeeExactly(Table table)
    {
        var expected = table.Rows.Select(r => r.Values.First().Trim()).ToList();
        var actual = Driver.FindElements(By.CssSelector("[data-testid='todo-label']"))
                           .Select(SafeText).Select(t => t.Replace("✅ ", "")).ToList();
        actual.Should().Equal(expected);
    }

    [Then(@"I should see in the list:")]
    public void ThenIShouldSeeInTheList(Table table)
    {
        var expected = table.Rows.Select(r => r.Values.First().Trim()).ToList();
        var actual = Driver.FindElements(By.CssSelector("[data-testid='todo-label']"))
                           .Select(SafeText).Select(t => t.Replace("✅ ", "")).ToList();
        actual.Should().BeEquivalentTo(expected);
    }

    [Then(@"I should see ""(.*)"" marked as overdue")]
    public void ThenIShouldSeeWithOverdue(string title)
    {
        var row = FindRow(title);
        var chip = row.FindElement(By.CssSelector(".chip"));
        chip.Text.Should().ContainEquivalentOf("(Overdue)");
    }

    [Then(@"I should see an alert containing the error: ""(.*)""")]
    public void ThenIShouldSeeAnAlertContainingError(string error)
    {
        var alert = Driver.SwitchTo().Alert();
        alert.Text.Should().Contain(error);
        alert.Dismiss();
    }

    // ---------- Helpers ----------
    private string BaseUrl() => (string)(_ctx.TryGetValue("baseUrl", out var v) ? v! : "http://localhost:5173");

    private static bool Exists(IWebDriver d, string css)
    {
        try { return d.FindElement(By.CssSelector(css)).Displayed; }
        catch { return false; }
    }

    private void Click(string css)
    {
        Wait.Until(d => Exists(d, css));
        Driver.FindElement(By.CssSelector(css)).Click();
    }

    private void Type(string css, string text)
    {
        var el = Driver.FindElement(By.CssSelector(css));
        el.Clear(); el.SendKeys(text);
    }

    private void SetValue(string css, string text)
    {
        var el = Driver.FindElement(By.CssSelector(css));
        el.Clear(); el.SendKeys(text);
    }

    private void Select(string css, string visible)
    {
        var sel = new SelectElement(Driver.FindElement(By.CssSelector(css)));
        sel.SelectByText(visible, true);
    }

    private void SetDate(string css, DateTime date)
    {
        var element = Driver.FindElement(By.CssSelector(css));
        ((IJavaScriptExecutor) Driver).ExecuteScript(
            "arguments[0].value = arguments[1];",
            element,
            date.ToString("yyyy-MM-dd"));
    }

    private IWebElement FindRow(string title)
    {
        Wait.Until(d => d.FindElements(By.CssSelector("#list li")).Any());
        foreach (var li in Driver.FindElements(By.CssSelector("#list li")))
        {
            var label = SafeText(li.FindElement(By.CssSelector("[data-testid='todo-label']")));
            if (label.EndsWith(title)) return li;
        }
        throw new Exception($"Row with title '{title}' not found");
    }

    private void WaitForRow(string title)
    {
        Wait.Until(d => d.FindElements(By.CssSelector("[data-testid='todo-label']"))
                         .Any(e => SafeText(e).EndsWith(title)));
    }

    private void WaitForAlert()
    {
        Wait.Until(driver =>
        {
            try { driver.SwitchTo().Alert(); return true; }
            catch (NoAlertPresentException) { return false; }
        });
    }

    private void SendKeysToAlert(string text)
    {
        Driver.SwitchTo().Alert().SendKeys(text);
    }

    private void AcceptAlert()
    {
        Driver.SwitchTo().Alert().Accept();
    }

    private void TryDismissAlert()
    {
        try { Driver.SwitchTo().Alert().Dismiss(); } catch {}
    }

    private static string SafeText(IWebElement el)
    {
        try { return el.Text ?? string.Empty; }
        catch (StaleElementReferenceException) { return string.Empty; }
    }

    private static DateTime ParseRelativeOrIsoDate(string s)
    {
        s = s.Trim();
        if (s.StartsWith("+") || s.StartsWith("-"))
        {
            var sign = s[0] == '+' ? 1 : -1;
            var numStr = new string(s.Skip(1).TakeWhile(char.IsDigit).ToArray());
            var unit = new string(s.Skip(1 + numStr.Length).ToArray()).ToLowerInvariant();
            var val = int.Parse(numStr);
            return unit.StartsWith("d") ? DateTime.Today.AddDays(sign * val) : DateTime.Today;
        }
        return DateTime.Parse(s);
    }
}

// Table helpers
public static class TableExt
{
    public static string? Get(this Table table, string key)
    {
        var row = table.Rows.FirstOrDefault(r => r.ContainsKey(key));
        return row != null ? row[key] : null;
    }

    public static string? GetValueOrDefault(this TableRow row, string key)
        => row.ContainsKey(key) ? row[key] : null;
}
