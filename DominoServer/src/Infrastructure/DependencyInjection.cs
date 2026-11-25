using Application.Interfaces;
using Application.Interfaces.Managers;
using Application.Interfaces.Repositories;

using Infrastructure.Database;
using Infrastructure.Services;
using Infrastructure.Services.FrameHandlers;
using Infrastructure.Services.Managers;
using Infrastructure.Services.Repositories;
using Infrastructure.Services.ServerUpdate;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureDatabase(configuration);
        
        services.AddSingleton<ITcpServer, TcpServer>();
        services.AddScoped<IMessageFrameHandler, MessageFrameHandler>();

        // Scoped necessary because it consumes scoped service
        services.AddScoped<MessageFrameHandlers>(); 
        services.AddScoped<LobbyFrameHandlers>();
        services.AddScoped<ChatFrameHandlers>();
        services.AddScoped<GameFrameHandlers>();
        services.AddScoped<FriendFrameHandlers>();
        services.AddScoped<ProfileFrameHandlers>();
        services.AddScoped<LoggingFrameHandlers>();
        services.AddScoped<TeamFrameHandlers>();
        services.AddScoped<DirectChallengeFrameHandlers>();

        services.AddScoped<IMailSender, MailSender>();

        services.AddSingleton<ServerUpdater>();
        services.AddSingleton<LobbyServerUpdater>();
        services.AddSingleton<GameServerUpdater>();
        services.AddSingleton<OAuthStateStore>();
        services.AddSingleton<DirectChallengeService>();
        services.AddScoped<ClientDisconnectionManager>();
        services.AddScoped<ResetPasswordManager>();
        services.AddScoped<SocialAuthService>();
        services.AddScoped<PresenceService>();
        services.AddScoped<LeaderboardService>();
        services.AddScoped<TeamService>();
        services.AddScoped<ChatService>();
        services.AddScoped<MatchAbandonmentService>();
        services.AddScoped<ITeamChallengeEloUpdater>(sp => sp.GetRequiredService<TeamService>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFriendRequestRepository, FriendRequestRepository>();
        services.AddScoped<IFriendshipRepository, FriendshipRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IMoveRepository, MoveRepository>();
        services.AddScoped<IDominoRepository, DominoRepository>();
        services.AddScoped<INationalityRepository, NationalityRepository>();

        services.AddScoped<IEmailSenderRepository, EmailSenderRepository>();
        services.AddScoped<IResetPasswordCodeRepository, ResetPasswordCodeRepository>();

        services.AddSingleton<ILobbyManager, LobbyManager>();
        services.AddSingleton<ITimerManager, TimerManager>();
        services.AddSingleton<IClientManager, ClientManager>();
        services.AddSingleton<ITokenManager, TokenManager>();
        services.AddSingleton<IGameManager, GameManager>();
        services.AddSingleton<ICodeGenerator, CodeGenerator>();

        services.AddSingleton<IChronometer, Chronometer>();
        services.AddSingleton<IChatManager, ChatManager>();

        services.AddSingleton<ISeedGenerator, SeedGenerator>();
        services.AddSingleton<IRandomGenerator, RandomGenerator>();

        return services;
    }

    private static void ConfigureDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string? param = configuration["UseInMemoryDatabase"];
        bool hasInMemoryDatabase = param?.Equals("true") ?? false;
        string? mysqlConnectionString = Environment.GetEnvironmentVariable("MysqlConnectionString");

        if (!hasInMemoryDatabase && !string.IsNullOrWhiteSpace(mysqlConnectionString))
        {
            services.AddDbContext<ApplicationDbContext>(options => options.UseMySql(mysqlConnectionString, ServerVersion.AutoDetect(mysqlConnectionString)));
        }
        else
        {
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase("DominoDb"));
        }
    }
}
