#nullable enable
using System;
using System.Linq;
using FluentAssertions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using TechTalk.SpecFlow;

[Binding]
public class TodosSteps
{
    private readonly ScenarioContext _ctx;
    private IWebDriver Driver => (IWebDriver)_ctx["driver"];
    private WebDriverWait Wait => new(Driver, TimeSpan.FromSeconds(6));

    public TodosSteps(ScenarioContext ctx) => _ctx = ctx;

    // We assume another step file sets the base URL & opens the page.

    [When(@"I add a todo titled ""(.*)""")]
    public void WhenIAddATodoTitled(string title)
    {
        var t = Driver.FindElement(By.CssSelector("#title"));
        t.Clear(); t.SendKeys(title);
        Driver.FindElement(By.CssSelector("[data-testid='add-btn']")).Click();
        Wait.Until(d => d.FindElements(By.CssSelector("[data-testid='todo-label']")).Any(e => e.Text.EndsWith(title)));
    }

    [Then(@"I should see ""(.*)"" in the list")]
    public void ThenIShouldSeeInTheList(string title)
    {
        var labels = Driver.FindElements(By.CssSelector("[data-testid='todo-label']")).Select(e => e.Text).ToList();
        labels.Any(x => x.EndsWith(title)).Should().BeTrue();
    }

    [When(@"I try to add a todo titled ""(.*)""")]
    public void WhenITryToAddATodoTitled(string title)
    {
        var t = Driver.FindElement(By.CssSelector("#title"));
        t.Clear(); t.SendKeys(title);
        Driver.FindElement(By.CssSelector("[data-testid='add-btn']")).Click();
    }

    [When(@"I try to add a todo with an empty title")]
    public void WhenITryToAddATodoWithEmptyTitle()
    {
        Driver.FindElement(By.CssSelector("#title")).Clear();
        Driver.FindElement(By.CssSelector("[data-testid='add-btn']")).Click();
    }

   [Then(@"the todo list should be empty")]
    public void ThenTheTodoListShouldBeEmpty()
    {
        Driver.FindElements(By.CssSelector("[data-testid='todo-label']")).Should().BeEmpty();
    }

    [When(@"I complete the todo ""(.*)""")]
    public void WhenICompleteTheTodo(string title)
    {
        Wait.Until(_ =>
        {
            try
            {
                FindRow(title).FindElement(By.CssSelector("[data-testid='complete-btn']")).Click(); 
                return true;
            }
            catch (StaleElementReferenceException) { return false; }
            catch (Exception e) when (e.Message.Contains("not found")) { return false; }
        });
    }

    [Then(@"the todo ""(.*)"" should appear completed")]
    public void ThenTheTodoShouldAppearCompleted(string title)
    {
        Wait.Until(_ =>
        {
            try
            {
                var row = FindRow(title);
                var text = row.FindElement(By.CssSelector("[data-testid='todo-label']")).Text;
                text.StartsWith("✅ ").Should().BeTrue();
                return true;
            }
            catch (StaleElementReferenceException) { return false; }
            catch (Exception e) when (e.Message.Contains("not found")) { return false; }
        });
    }

    [When(@"I delete the todo ""(.*)""")]
    public void WhenIDeleteTheTodo(string title)
    {
        var row = FindRow(title);
        row.FindElement(By.CssSelector("[data-testid='delete-btn']")).Click();
        Wait.Until(_ => !Driver.FindElements(By.CssSelector("[data-testid='todo-label']"))
            .Any(e => {
                try { return e.Text.EndsWith(title); }
                catch (StaleElementReferenceException) { return false; }
            }));
    }

    [Then(@"I should not see ""(.*)"" in the list")]
    public void ThenIShouldNotSeeInTheList(string title)
    {
        var labels = Driver.FindElements(By.CssSelector("[data-testid='todo-label']")).Select(e => e.Text).ToList();
        labels.Any(x => x.EndsWith(title)).Should().BeFalse();
    }

    // helpers
    private IWebElement FindRow(string title)
    {
        Wait.Until(d => d.FindElements(By.CssSelector("#list li")).Any());
        foreach (var li in Driver.FindElements(By.CssSelector("#list li")))
        {
            string label;
            try { label = li.FindElement(By.CssSelector("[data-testid='todo-label']")).Text ?? string.Empty; }
            catch (StaleElementReferenceException) { continue; }
            if (label.EndsWith(title)) return li;
        }
        throw new Exception($"Row with title '{title}' not found");
    }
}
