using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using TechTalk.SpecFlow;

[Binding]
public class Hooks
{
    private readonly ScenarioContext _ctx;
    public Hooks(ScenarioContext ctx) => _ctx = ctx;

    [BeforeScenario]
    public void StartDriver()
    {
        // Selenium Manager (built into Selenium 4.6+) fetches a matching driver automatically.
        var opts = new ChromeOptions();
        opts.AddArgument("--window-size=1280,900"); // NOT headless
        var driver = new ChromeDriver(opts);
        _ctx["driver"] = driver;
    }

    [AfterScenario]
    public void StopDriver()
    {
        if (_ctx.TryGetValue("driver", out object raw) && raw is IWebDriver driver)
            driver.Quit();
    }
}
