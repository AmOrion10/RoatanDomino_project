using System.Net.WebSockets;

using Application;
using Application.Common;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Managers;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using Infrastructure;
using Infrastructure.Database;
using Infrastructure.Database.Entities;
using Infrastructure.Services;
using Infrastructure.Services.ServerUpdate;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

namespace Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length >= 2 && (args[0] == "server" || args[0] == "client"))
        {
            int port = int.Parse(args[1]);
            switch (args[0])
            {
                case "server":
                {
                    int maxClients = args.Length >= 3 ? int.Parse(args[2]) : 200;
                    await StartServerAsync(ConfigureServices(new ConfigurationManager()).BuildServiceProvider(), port, maxClients, CancellationToken.None);
                    return;
                }
                case "client":
                {
                    ClientTest clientTest = new();
                    bool useSsl = args is [_, _, _, "true"];
                    await clientTest.ConnectAsync(args[2], port, useSsl);
                    return;
                }
            }
        }

        await RunWebHostAsync(args);
    }

    public static async Task RunWebHostAsync(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        string listenUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(listenUrls))
        {
            string portEnv = Environment.GetEnvironmentVariable("PORT") ?? "5000";
            listenUrls = $"http://0.0.0.0:{portEnv}";
            builder.WebHost.UseUrls(listenUrls);
        }

        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        builder.Services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApplication();
        builder.Services.AddHostedService<GameLoopHostedService>();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        WebApplication app = builder.Build();

        int maxClients = 200;
        if (int.TryParse(Environment.GetEnvironmentVariable("DOMINO_MAX_CLIENTS"), out int parsedMax) && parsedMax > 0)
        {
            maxClients = parsedMax;
        }

        IClientManager clientManager = app.Services.GetRequiredService<IClientManager>();
        clientManager.ConfigureMaxClients(maxClients);

        ILobbyManager lobbyManager = app.Services.GetRequiredService<ILobbyManager>();
        lobbyManager.MaintainHouseTable = true;
        lobbyManager.EnsureHouseTable();

        await SeedReferenceDataIfNeededAsync(app.Services, app.Logger);

        app.UseForwardedHeaders();
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(20)
        });

        app.MapGet("/health", (IClientManager cm, ILobbyManager lm) => Results.Ok(new
        {
            status = "ok",
            service = "DominoServer",
            clients = cm.GetAllClients().Count,
            maxClients,
            openTables = lm.GetAllPublicLobbies().Count,
            utc = DateTime.UtcNow
        }));

        app.MapPost("/auth/social", async (SocialAuthDto request, SocialAuthService socialAuth, CancellationToken ct) =>
        {
            try
            {
                Domain.User user = await socialAuth.AuthenticateProfileAsync(request, ct);
                return Results.Ok(new
                {
                    ok = true,
                    user = new
                    {
                        id = user.Id,
                        username = user.Login,
                        email = user.Email,
                        elo = user.Score
                    }
                });
            }
            catch (BaseApplicationException ex)
            {
                return Results.BadRequest(new
                {
                    ok = false,
                    error = ex.Message
                });
            }
        });

        app.Map("/ws", async (HttpContext context, IClientManager cm, ILogger<WebSocketClient> wsLogger) =>
        {
            await HandleWebSocketUpgradeAsync(context, cm, wsLogger);
        });

        app.Map("/", async (HttpContext context, IClientManager cm, ILogger<WebSocketClient> wsLogger) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                await HandleWebSocketUpgradeAsync(context, cm, wsLogger);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new
            {
                service = "DominoServer",
                status = "running",
                ws = "/ws",
                health = "/health"
            });
        });

        app.Logger.LogInformation("DominoServer WebSocket + HTTP host listening on {urls} (maxClients={maxClients})", listenUrls, maxClients);
        await app.RunAsync();
    }

    private static async Task HandleWebSocketUpgradeAsync(HttpContext context, IClientManager clientManager, ILogger<WebSocketClient> wsLogger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Expected a WebSocket connection.");
            return;
        }

        if (!clientManager.HasEnoughSpaceForAnotherClient())
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Server is full.");
            return;
        }

        using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
        WebSocketClient wsClient = new(webSocket, wsLogger);
        wsLogger.LogInformation("New WebSocket client {id} connected from {remoteIp}", wsClient.Id, context.Connection.RemoteIpAddress);
        clientManager.HandleNewClient(wsClient);

        using CancellationTokenRegistration abortReg = context.RequestAborted.Register(() => wsClient.CloseConnection());
        await wsClient.ClosedTask;
    }

    private static async Task SeedReferenceDataIfNeededAsync(IServiceProvider services, ILogger logger)
    {
        try
        {
            using IServiceScope scope = services.CreateScope();
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();

            if (!await db.Dominos.AnyAsync())
            {
                List<DominoEntity> tiles = [];
                for (short i = 0; i < 7; i++)
                {
                    for (short j = i; j < 7; j++)
                    {
                        tiles.Add(new DominoEntity { Side1 = i, Side2 = j });
                    }
                }
                await db.Dominos.AddRangeAsync(tiles);
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded {count} standard domino tiles into database.", tiles.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Skipping automatic database seeding: {message}", ex.Message);
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

internal sealed class GameLoopHostedService(
    IServiceScopeFactory scopeFactory,
    IChronometer chronometer,
    ILogger<GameLoopHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DominoServer game loop started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                TimeSpan deltaTime = chronometer.GetTimedElapsed();
                chronometer.StartFrame();

                using IServiceScope scope = scopeFactory.CreateScope();
                ServerUpdater serverUpdater = scope.ServiceProvider.GetRequiredService<ServerUpdater>();
                await serverUpdater.UpdateAsync(deltaTime);

                chronometer.EndFrame();
                await Task.Delay(16, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DominoServer game loop tick.");
                await Task.Delay(100, stoppingToken);
            }
        }
    }
}
