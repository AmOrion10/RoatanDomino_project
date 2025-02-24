using Application;
using Application.Common;

using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage :\n" +
                              "./Program client port addr [useSsl]\n" +
                              "./Program server port maxClients");
            return;
        }

        int port = int.Parse(args[1]);
        switch (args[0])
        {
            case "server":
            {
                int maxClients = int.Parse(args[2]);
                await StartServerAsync(ConfigureServices(new ConfigurationManager()).BuildServiceProvider(), port, maxClients, CancellationToken.None);
                break;
            }
            case "client":
            {
                ClientTest clientTest = new();
                bool useSsl = args is [_, _, _, "true"];
                await clientTest.ConnectAsync(args[2], port, useSsl);
                break;
            }
        }
    }

    public static async Task StartServerAsync(IServiceProvider provider, int port, int maxClients, CancellationToken token)
    {
        Server server = new(provider, port, maxClients);
        await server.StartAsync(token);
    }

    public static IServiceCollection ConfigureServices(IConfiguration configuration)
    {
        ServiceCollection services = [];

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        services.AddInfrastructure(configuration);
        services.AddApplication();

        return services;
    }
}
