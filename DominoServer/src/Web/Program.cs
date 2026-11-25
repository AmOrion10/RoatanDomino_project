using System.Net.WebSockets;

using Application;
using Application.Common;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;

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
            googleOAuthConfigured = SocialAuthService.IsProviderConfigured("Google"),
            googleIdTokenConfigured = SocialAuthService.IsGoogleIdTokenConfigured(),
            facebookOAuthConfigured = SocialAuthService.IsProviderConfigured("Facebook"),
            utc = DateTime.UtcNow
        }));

        app.MapGet("/auth/oauth/{provider}/start", (HttpContext context, string provider, SocialAuthService socialAuth) =>
        {
            string baseUrl = ResolvePublicBaseUrl(context);
            string? requestedState = context.Request.Query["state"].FirstOrDefault();
            string? loginHint = context.Request.Query["login_hint"].FirstOrDefault();
            string? reauthParam = context.Request.Query["reauth"].FirstOrDefault();
            bool forceReauth = !string.Equals(reauthParam, "false", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(reauthParam, "0", StringComparison.OrdinalIgnoreCase);
            bool jsonMode = string.Equals(context.Request.Query["mode"].FirstOrDefault(), "json", StringComparison.OrdinalIgnoreCase);

            OAuthStartResult result = socialAuth.StartOAuthFlow(provider, baseUrl, requestedState, loginHint, forceReauth);
            if (!result.Configured || string.IsNullOrWhiteSpace(result.AuthorizeUrl))
            {
                return Results.Ok(new
                {
                    configured = false,
                    idTokenConfigured = result.IdTokenConfigured,
                    provider = result.Provider,
                    clientId = result.ClientId,
                    message = result.Message ?? SocialAuthService.GetMissingConfigurationMessage(provider)
                });
            }

            if (jsonMode)
            {
                return Results.Ok(new
                {
                    configured = true,
                    idTokenConfigured = result.IdTokenConfigured,
                    provider = result.Provider,
                    clientId = result.ClientId,
                    state = result.State,
                    authorizeUrl = result.AuthorizeUrl
                });
            }

            return Results.Redirect(result.AuthorizeUrl);
        });

        app.MapPost("/auth/oauth/google/id-token", async (HttpContext context, SocialAuthService socialAuth, CancellationToken ct) =>
        {
            try
            {
                string? idToken = null;
                using System.Text.Json.JsonDocument body = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body, cancellationToken: ct);
                if (body.RootElement.TryGetProperty("idToken", out System.Text.Json.JsonElement tkEl))
                {
                    idToken = tkEl.GetString();
                }
                else if (body.RootElement.TryGetProperty("id_token", out System.Text.Json.JsonElement snakeEl))
                {
                    idToken = snakeEl.GetString();
                }

                (Domain.User user, string oneTimeCode) = await socialAuth.VerifyGoogleIdTokenAndCreateSessionAsync(idToken, ct);
                return Results.Ok(new
                {
                    ok = true,
                    provider = "Google",
                    oneTimeCode,
                    user = DtoBuilder.GetUserDto(user)
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
            catch (System.Text.Json.JsonException)
            {
                return Results.BadRequest(new
                {
                    ok = false,
                    error = "Invalid JSON payload. Expected { \"idToken\": \"...\" }."
                });
            }
        });

        app.MapGet("/auth/oauth/{provider}/callback", async (HttpContext context, string provider, SocialAuthService socialAuth, CancellationToken ct) =>
        {
            string baseUrl = ResolvePublicBaseUrl(context);
            string? code = context.Request.Query["code"].FirstOrDefault();
            string? state = context.Request.Query["state"].FirstOrDefault();
            string? providerError = context.Request.Query["error"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(providerError))
            {
                if (!string.IsNullOrWhiteSpace(state))
                {
                    await socialAuth.CompleteOAuthCallbackAsync(provider, null, state, baseUrl, ct);
                }
                return Results.Content(BuildOAuthResultHtml(false, provider, string.Empty, "Sign-in was cancelled."), "text/html");
            }

            var (ok, oneTimeCode, normalizedProvider, errorMsg) = await socialAuth.CompleteOAuthCallbackAsync(provider, code, state, baseUrl, ct);
            string deepLink = ok
                ? $"roatandomino://auth?code={Uri.EscapeDataString(oneTimeCode)}&provider={Uri.EscapeDataString(normalizedProvider)}&state={Uri.EscapeDataString(state ?? string.Empty)}"
                : string.Empty;

            return Results.Content(BuildOAuthResultHtml(ok, normalizedProvider, deepLink, errorMsg ?? "Signed in! Returning to Roatan Domino..."), "text/html");
        });

        app.MapGet("/auth/oauth/poll/{state}", (string state, SocialAuthService socialAuth) =>
        {
            OAuthPendingSession? session = socialAuth.PollOAuthSession(state);
            if (session is null)
            {
                return Results.NotFound(new { status = "expired", error = "OAuth session expired." });
            }

            return Results.Ok(new
            {
                status = session.Status,
                provider = session.Provider,
                oneTimeCode = session.OneTimeCode,
                email = session.Email,
                displayName = session.DisplayName,
                avatarUrl = session.AvatarUrl,
                error = session.ErrorMessage
            });
        });

        app.MapPost("/auth/social", async (SocialAuthDto request, SocialAuthService socialAuth, CancellationToken ct) =>
        {
            try
            {
                Domain.User user = await socialAuth.AuthenticateProfileAsync(request, ct);
                return Results.Ok(new
                {
                    ok = true,
                    user = DtoBuilder.GetUserDto(user)
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

        app.MapDelete("/auth/account", async (
            HttpContext context,
            IUserRepository userRepo,
            IClientManager cm,
            ILobbyManager lm,
            ITokenManager tm,
            PresenceService presence) =>
        {
            long userId = 0;
            string? userIdParam = context.Request.Query["userId"].FirstOrDefault();
            string? emailParam = context.Request.Query["email"].FirstOrDefault();
            string? authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            string? tokenParam = !string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : context.Request.Query["token"].FirstOrDefault();

            if (long.TryParse(userIdParam, out long parsedId) && parsedId > 0)
            {
                userId = parsedId;
            }
            else if (!string.IsNullOrWhiteSpace(emailParam))
            {
                Domain.User? byEmail = await userRepo.GetByEmail(emailParam);
                if (byEmail is not null) userId = byEmail.Id;
            }

            if (userId <= 0)
            {
                return Results.BadRequest(new { ok = false, error = "Missing valid userId or email." });
            }

            // If user is currently connected, remove them from any active table and log them out cleanly
            IClient? activeClient = cm.GetAllLoggedInClients().FirstOrDefault(c => c.User is not null && c.User.Id == userId);
            if (activeClient is not null)
            {
                if (!string.IsNullOrWhiteSpace(tokenParam) &&
                    Guid.TryParse(tokenParam, out Guid providedToken) &&
                    tm.GetTokenForClient(activeClient) != providedToken)
                {
                    return Results.Unauthorized();
                }

                Domain.Lobby? activeTable = lm.GetLobbyForClient(activeClient);
                if (activeTable is not null)
                {
                    lm.RemovePlayerFromLobby(activeClient, activeTable.Id);
                }
                tm.RemoveTokenForClientIfPresent(activeClient);
                activeClient.IsLoggedIn = false;
                activeClient.User = null;
            }

            bool deleted = await userRepo.DeleteUser(userId);
            if (!deleted)
            {
                return Results.NotFound(new { ok = false, error = "Account not found." });
            }

            await presence.BroadcastRosterAsync();
            return Results.Ok(new { ok = true, deletedUserId = userId });
        });

        app.MapGet("/api/leaderboard", async (HttpContext context, LeaderboardService leaderboardService) =>
        {
            long? userId = long.TryParse(context.Request.Query["userId"].FirstOrDefault(), out long parsedId) && parsedId > 0 ? parsedId : null;
            int limit = int.TryParse(context.Request.Query["limit"].FirstOrDefault(), out int parsedLimit) && parsedLimit > 0 ? parsedLimit : 100;
            LeaderboardDto board = await leaderboardService.BuildLeaderboardAsync(userId, limit);
            return Results.Ok(board);
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

    private static string ResolvePublicBaseUrl(HttpContext context)
    {
        string? envBase = Environment.GetEnvironmentVariable("PUBLIC_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envBase))
        {
            return envBase.Trim().TrimEnd('/');
        }

        string scheme = context.Request.Scheme;
        string host = context.Request.Host.ToString();
        return $"{scheme}://{host}".TrimEnd('/');
    }

    private static string BuildOAuthResultHtml(bool success, string provider, string deepLink, string message)
    {
        string safeProvider = System.Net.WebUtility.HtmlEncode(provider);
        string safeMessage = System.Net.WebUtility.HtmlEncode(message);
        string redirectScript = success && !string.IsNullOrWhiteSpace(deepLink)
            ? $"<script>setTimeout(function(){{ window.location.href = {System.Text.Json.JsonSerializer.Serialize(deepLink)}; }}, 150);</script>"
            : string.Empty;
        string buttonHtml = success && !string.IsNullOrWhiteSpace(deepLink)
            ? $"<a href=\"{System.Net.WebUtility.HtmlEncode(deepLink)}\" style=\"display:inline-block;margin-top:20px;padding:14px 28px;background:#26A65B;color:#fff;text-decoration:none;border-radius:999px;font-weight:bold;font-size:16px;\">Return to Roatan Domino</a>"
            : string.Empty;

        return $@"<!doctype html>
<html><head><meta charset=""utf-8""/><meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
<title>Roatan Domino — {safeProvider} Sign-In</title></head>
<body style=""margin:0;background:#08202A;color:#F2FAFC;font-family:system-ui,-apple-system,sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;padding:24px;"">
<div style=""background:#0D2E3B;border:1px solid #F7C948;border-radius:20px;padding:32px;max-width:420px;text-align:center;"">
<h2 style=""color:#F7C948;margin-top:0;"">Roatan Domino</h2>
<p style=""font-size:17px;line-height:1.5;"">{safeMessage}</p>
{buttonHtml}
<p style=""font-size:13px;color:#9DC4D0;margin-top:20px;"">You can close this browser tab and return to the game.</p>
</div>{redirectScript}</body></html>";
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

            if (db.Database.IsRelational())
            {
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN avatar_url TEXT NULL"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN auth_provider VARCHAR(32) NULL"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN elo INT NOT NULL DEFAULT 1000"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN wins INT NOT NULL DEFAULT 0"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN losses INT NOT NULL DEFAULT 0"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN win_assists INT NOT NULL DEFAULT 0"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN total_moves INT NOT NULL DEFAULT 0"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN optimal_moves INT NOT NULL DEFAULT 0"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN average_turn_seconds DOUBLE NOT NULL DEFAULT 0"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN online_matches_last_7_days INT NOT NULL DEFAULT 0"); } catch { }
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN last_online_match_utc DATETIME NULL"); } catch { }

                try
                {
                    await db.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE IF NOT EXISTS Teams (
                            id BIGINT AUTO_INCREMENT PRIMARY KEY,
                            name VARCHAR(64) NOT NULL,
                            tag VARCHAR(8) NOT NULL,
                            team_elo INT NOT NULL DEFAULT 1000,
                            team_wins INT NOT NULL DEFAULT 0,
                            team_losses INT NOT NULL DEFAULT 0,
                            max_members INT NOT NULL DEFAULT 12,
                            join_policy INT NOT NULL DEFAULT 0,
                            min_elo INT NOT NULL DEFAULT 100,
                            activity_requirement INT NOT NULL DEFAULT 0,
                            created_at_utc DATETIME NOT NULL
                        )");
                }
                catch { }
                try
                {
                    await db.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE IF NOT EXISTS TeamMembers (
                            team_id BIGINT NOT NULL,
                            user_id BIGINT NOT NULL,
                            role INT NOT NULL DEFAULT 0,
                            joined_at_utc DATETIME NOT NULL,
                            PRIMARY KEY (team_id, user_id),
                            UNIQUE KEY UX_TeamMembers_UserId (user_id)
                        )");
                }
                catch { }
                try
                {
                    await db.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE IF NOT EXISTS TeamJoinRequests (
                            id BIGINT AUTO_INCREMENT PRIMARY KEY,
                            team_id BIGINT NOT NULL,
                            user_id BIGINT NOT NULL,
                            created_at_utc DATETIME NOT NULL,
                            UNIQUE KEY UX_TeamJoinRequests_TeamUser (team_id, user_id)
                        )");
                }
                catch { }
                try
                {
                    await db.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE IF NOT EXISTS TeamInvites (
                            id BIGINT AUTO_INCREMENT PRIMARY KEY,
                            team_id BIGINT NOT NULL,
                            invited_user_id BIGINT NOT NULL,
                            invited_by_user_id BIGINT NOT NULL,
                            created_at_utc DATETIME NOT NULL,
                            UNIQUE KEY UX_TeamInvites_TeamInvited (team_id, invited_user_id)
                        )");
                }
                catch { }
            }

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
    ServerUpdater serverUpdater,
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
