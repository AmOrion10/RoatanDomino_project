using Infrastructure.Database.Entities;

using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AvatarEntity> Avatars { get; set; }

    public DbSet<BanEntity> Bans { get; set; }

    public DbSet<ChatEntity> Chats { get; set; }

    public DbSet<DominoEntity> Dominos { get; set; }

    public DbSet<FriendRequestEntity> FriendRequests { get; set; }

    public DbSet<FriendshipEntity> Friendships { get; set; }

    public DbSet<GameEntity> Games { get; set; }

    public DbSet<GameTournamentEntity> GameTournaments { get; set; }

    public DbSet<MoveEntity> Moves { get; set; }

    public DbSet<NationalityEntity> Nationalities { get; set; }

    public DbSet<RankEntity> Ranks { get; set; }
    
    public DbSet<ReportEntity> Reports { get; set; }

    public DbSet<RoleEntity> Roles { get; set; }

    public DbSet<TournamentEntity> Tournaments { get; set; }

    public DbSet<UserEntity> Users { get; set; }

    public DbSet<UserGameEntity> UserGames { get; set; }
    
    public DbSet<UserRoleEntity> UserRoles { get; set; }

    public DbSet<UserTournamentEntity> UserTournaments { get; set; }

    public DbSet<EmailSendedEntity> EmailSended { get; set; }

    public DbSet<ResetPasswordCodeEntity> ResetPasswordCode { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AvatarEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Avatars");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Image).HasColumnName("image");

            entity.HasOne(d => d.User)
                .WithOne(p => p.Avatar)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<BanEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Bans");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.EndBan).HasColumnName("end_time");
            entity.Property(e => e.StartBan).HasColumnName("start_time");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Reason).HasColumnName("reason");

            entity.HasOne(d => d.BannedUser)
                .WithMany(p => p.Bans)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ChatEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Chat");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SenderUserId).HasColumnName("sender_user_id");
            entity.Property(e => e.ReceiverUserId).HasColumnName("receiver_user_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.MessageTime).HasColumnName("send_time");
            entity.Property(e => e.Channel).HasColumnName("channel");

            entity.HasOne(d => d.SenderUser)
                .WithMany(p => p.ChatUsers)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<DominoEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Dominos");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Side1).HasColumnName("side_one");
            entity.Property(e => e.Side2).HasColumnName("side_two");
        });

        modelBuilder.Entity<EmailSendedEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("EmailSended");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DateSended).HasColumnName("date_sended");
            entity.Property(e => e.ToEmailAddress).HasColumnName("to_email_address");
            entity.Property(e => e.EmailType)
                .HasColumnName("email_type")
                .HasConversion<string>();
        });

        modelBuilder.Entity<FriendRequestEntity>(entity =>
        {
            entity.ToTable("FriendRequests");

            entity.HasKey(e => new { e.UserIdApplicant, e.UserIdAsked });

            entity.HasOne(f => f.UserApplicant)
                .WithMany(u => u.FriendRequestsApplicant)
                .HasForeignKey(f => f.UserIdApplicant);

            entity.HasOne(f => f.UserAsked)
                .WithMany(u => u.FriendRequestsAsked)
                .HasForeignKey(f => f.UserIdAsked);

            entity.Property(f => f.UserIdAsked)
                .HasColumnName("user_id_asked");

            entity.Property(f => f.UserIdApplicant)
                .HasColumnName("user_id_applicant");
        });

        modelBuilder.Entity<FriendshipEntity>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.FriendId });

            entity.ToTable("Friendship");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.FriendId).HasColumnName("friend_id");
            entity.Property(e => e.DateStart).HasColumnName("date_start");

            entity.HasOne(d => d.Friend)
                .WithMany(p => p.FriendshipFriends)
                .HasForeignKey(d => d.FriendId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User)
                .WithMany(p => p.FriendshipUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<GameEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Games");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.GameMode)
                .HasColumnName("mode")
                .HasConversion<string>();
            entity.Property(e => e.Bet).HasColumnName("bet");
            entity.Property(e => e.WinnerId).HasColumnName("winner_id");
            entity.Property(e => e.GameDuration).HasColumnName("game_duration");
            entity.Property(e => e.TurnDuration).HasColumnName("turn_duration");

            entity.HasMany(d => d.UserGames)
                .WithOne(p => p.Game)
                .HasForeignKey(d => d.GameId);

            entity.HasMany(e => e.Moves)
                .WithOne(e => e.Game)
                .HasForeignKey(e => e.GameId);

            entity.HasMany(g => g.Users)
                .WithMany(u => u.Games)
                .UsingEntity<UserGameEntity>();

            entity.HasOne(g => g.Winner)
                .WithMany(u => u.GamesWon)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<GameTournamentEntity>(entity =>
        {
            entity.HasKey(e => new { e.TournamentId, e.GameId });

            entity.ToTable("GameTournaments");

            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");

            entity.HasOne(d => d.Game)
                .WithMany(e => e.GameTournaments)
                .HasForeignKey(d => d.GameId);

            entity.HasOne(d => d.Tournament)
                .WithMany(e => e.GameTournaments)
                .HasForeignKey(d => d.TournamentId);
        });

        modelBuilder.Entity<MoveEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Moves");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Row).HasColumnName("row");
            entity.Property(e => e.Col).HasColumnName("col");
            entity.Property(e => e.Orientation)
                .HasColumnName("orientation")
                .HasConversion<string>();
            entity.Property(e => e.Order).HasColumnName("order");
            entity.Property(e => e.DominoId).HasColumnName("domino_id");
            entity.Property(e => e.UserId).HasColumnName("player_id");
            entity.Property(e => e.GameId).HasColumnName("game_id");

            entity.HasOne(d => d.Domino)
                .WithMany()
                .HasForeignKey(d => d.DominoId);

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Game)
                .WithMany(d => d.Moves)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientCascade);
        });

        modelBuilder.Entity<NationalityEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Nationality");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<RankEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Ranks");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.MinPoints).HasColumnName("min_points");
            entity.Property(e => e.MaxPoints).HasColumnName("max_points");
        });

        modelBuilder.Entity<ReportEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Reports");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ReportedUserId).HasColumnName("user_id");
            entity.Property(e => e.ReporterUserId).HasColumnName("reporter_id");
            entity.Property(e => e.DateReport).HasColumnName("date_time");
            entity.Property(e => e.Reason).HasColumnName("reason");

            entity.HasOne(e => e.UserReported)
                .WithMany(e => e.Reports)
                .HasForeignKey(e => e.ReportedUserId);
            /*
            entity.HasOne(e => e.UserReporter)
                .WithMany(e => e.Reports)
                .HasForeignKey(e => e.ReporterUserId);
            */
        });

        modelBuilder.Entity<ResetPasswordCodeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("ResetPasswordCode");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.EmailAddress).HasColumnName("email_address");
            entity.Property(e => e.DateGenerated).HasColumnName("date_generated");
        });

        modelBuilder.Entity<RoleEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Roles");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");

            entity.HasMany(e => e.UserRoles)
                .WithOne(e => e.Role)
                .HasForeignKey(e => e.RoleId);
        });

        modelBuilder.Entity<TournamentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Tournaments");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.ToTable("Users");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Login).HasColumnName("login");
            entity.Property(e => e.Password).HasColumnName("password");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.DateSignup).HasColumnName("date_signup");
            entity.Property(e => e.DateLastLogin).HasColumnName("date_last_login");
            entity.Property(e => e.AvatarId).HasColumnName("avatar");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.AuthProvider).HasColumnName("auth_provider");
            entity.Property(e => e.Score).HasDefaultValue(10000).HasColumnName("score");
            entity.Property(e => e.Birthdate).HasColumnName("birthday");
            entity.Property(e => e.NationalityId).HasColumnName("nationality");
            entity.Property(e => e.RankId).HasColumnName("rank");

            entity.HasOne(d => d.Avatar)
                .WithOne(p => p.User)
                .HasForeignKey<UserEntity>(d => d.AvatarId);

            entity.Navigation(e => e.Avatar).AutoInclude();

            entity.HasOne(d => d.Nationality)
                .WithMany(p => p.Users)
                .HasForeignKey(d => d.NationalityId);

            entity.Navigation(e => e.Nationality).AutoInclude();

            entity.HasOne(d => d.Rank)
                .WithMany(p => p.Users)
                .HasForeignKey(d => d.RankId);

            entity.HasMany(u => u.Games)
                .WithMany(g => g.Users)
                .UsingEntity<UserGameEntity>();
        });

        modelBuilder.Entity<UserGameEntity>(entity =>
        {
            entity.HasKey(e => new { e.GameId, e.UserId });

            entity.ToTable("UserGame");

            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ScoreUpdate).HasColumnName("score_update");

            entity.HasOne(d => d.Game)
                .WithMany(d => d.UserGames)
                .HasForeignKey(d => d.GameId)
                .OnDelete(DeleteBehavior.ClientCascade);

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserGames)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientCascade);
        });

        modelBuilder.Entity<UserRoleEntity>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId });

            entity.ToTable("UserRole");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");

            entity.HasOne(d => d.Role)
                .WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId);

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<UserTournamentEntity>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.TournamentId });
            
            entity.ToTable("UserTournament");

            entity.Property(e => e.TournamentId).HasColumnName("tournament_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Tournament)
                .WithMany(d => d.UserTournaments)
                .HasForeignKey(d => d.TournamentId);

            entity.HasOne(d => d.User)
                .WithMany(d => d.UserTournaments)
                .HasForeignKey(d => d.UserId);
        });
    }
}
