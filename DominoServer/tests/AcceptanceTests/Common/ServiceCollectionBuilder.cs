using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Managers;
using CommonTests.Fixtures;

using Infrastructure.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AcceptanceTests.Common;

public static class ServiceCollectionBuilder
{
    public const string MockIMessageHandler = "mock-imessagehandler";
    public const string MockIMessageDispatcher = "mock-imessagedispatcher";
    public const string DontMockIClientManager = "no-mock-iclientmanager";

    private static readonly IConfiguration Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        { "UseInMemoryDatabase", "true" }
    }).Build();

    public static IServiceCollection BuildServices(ScenarioContext context)
    {
        return Web.Program.ConfigureServices(Configuration)
            .ConfigureInMemoryDatabase()
            .SubstituteServices(context);
    }

    private static IServiceCollection ConfigureInMemoryDatabase(this IServiceCollection services)
    {
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();

        services.AddDbContext<ApplicationDbContext>((_, options) =>
        {
            options.UseInMemoryDatabase("test-db");
            options.UseInternalServiceProvider(serviceProvider);
        });

        return services;
    }

    private static IServiceCollection SubstituteServices(this IServiceCollection services, ScenarioContext context)
    {
        services.ReplaceService<ITcpServer, TcpServerMock>();
        services.ReplaceService<IChronometer, ChronometerMock>();
        services.ReplaceService<ISeedGenerator, SeedGeneratorMock>();
        services.ReplaceService<IMailSender, MailSenderMock>();

        if (context.ContainsKey(MockIMessageHandler))
        {
            services.ReplaceService<IMessageFrameHandler, MessageFrameHandlerMock>();
        }

        if (context.ContainsKey(MockIMessageDispatcher))
        {
            services.ReplaceService<IMessageDispatcher, MessageDispatcherMock>();
        }

        if (!context.ContainsKey(DontMockIClientManager))
        {
            services.ReplaceService<IClientManager, ClientManagerMock>();
        }

        return services;
    }

    private static IServiceCollection ReplaceService<T, U>(this IServiceCollection services)
    {
        ServiceDescriptor servideDescriptor = services.FirstOrDefault(o => o.ServiceType == typeof(T)) ?? throw new Exception($"No service of type {typeof(T).Name} was found.");
        services.Replace(new ServiceDescriptor(typeof(T), _ => Activator.CreateInstance(typeof(U))!, servideDescriptor.Lifetime));
        return services;
    }
}