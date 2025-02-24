namespace AcceptanceTests.Common;

[Binding]
public class ScenarioBuilder
{
    private readonly ScenarioContext context;
    private TestEnvironment? environment;

    public ScenarioBuilder(ScenarioContext context)
    {
        this.context = context;
    }

    [BeforeScenario]
    public void Init()
    {
        CleanEnvironment();
        environment = TestEnvironment.BuildScope(ServiceCollectionBuilder.BuildServices(context));
        context.Set(environment);
    }

    [AfterScenario]
    public void CleanEnvironment()
    {
        environment?.Dispose();
        environment = null;
    }
}
