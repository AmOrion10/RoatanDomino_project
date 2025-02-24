using Microsoft.Extensions.DependencyInjection;

namespace AcceptanceTests.Common;

public class TestEnvironment : IDisposable
{
    public IServiceProvider ServiceProvider => serviceScope.ServiceProvider;

    private readonly IServiceScope serviceScope;

    public TestEnvironment(IServiceScope serviceScope)
    {
        this.serviceScope = serviceScope;
    }

    public static TestEnvironment BuildScope(IServiceCollection services)
    {
        return new TestEnvironment(services.BuildServiceProvider(true).CreateScope());
    }

    public void Dispose()
    {
        serviceScope.Dispose();
    }
}
