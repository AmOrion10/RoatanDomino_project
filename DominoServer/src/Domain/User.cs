using DominoCommon.com.domino.common.Scripts.Enums;

namespace Domain;

public class User(string email, string login)
{
    public long Id { get; set; }
    public string Email { get; } = email;
    public string Login { get; set; } = login;
    public int Score { get; set; }
    public int Elo { get; set; } = RoatanRankTierHelper.DefaultElo;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int WinAssists { get; set; }
    public int TotalMoves { get; set; }
    public int OptimalMoves { get; set; }
    public double AverageTurnSeconds { get; set; }
    public int OnlineMatchesLast7Days { get; set; }
    public DateTime? LastOnlineMatchUtc { get; set; }
    public int AbandonedMatches { get; set; }
    public DateTime DateSignup { get; set; }
    public DateOnly Birthdate { get; set; }
    public string? Nationality { get; set; }
    public string? AvatarUrl { get; set; }
    public string? AvatarBase64 { get; set; }
    public string AuthProvider { get; set; } = "Email";
    public UserStats? Statistics { get; set; }

    public double AccuracyPercent => RoatanRankTierHelper.CalculateAccuracyPercent(OptimalMoves, TotalMoves);
    public RoatanRankTier RankTier => RoatanRankTierHelper.FromElo(Elo);

    public User(User user) : this(user.Email, user.Login)
    {
        Id = user.Id;
        Score = user.Score;
        Elo = user.Elo;
        Wins = user.Wins;
        Losses = user.Losses;
        WinAssists = user.WinAssists;
        TotalMoves = user.TotalMoves;
        OptimalMoves = user.OptimalMoves;
        AverageTurnSeconds = user.AverageTurnSeconds;
        OnlineMatchesLast7Days = user.OnlineMatchesLast7Days;
        LastOnlineMatchUtc = user.LastOnlineMatchUtc;
        AbandonedMatches = user.AbandonedMatches;
        DateSignup = user.DateSignup;
        Birthdate = user.Birthdate;
        Nationality = user.Nationality;
        AvatarUrl = user.AvatarUrl;
        AvatarBase64 = user.AvatarBase64;
        AuthProvider = user.AuthProvider;
        Statistics = user.Statistics;
    }

    public override bool Equals(object? obj)
    {
        return obj is User user && Id == user.Id;
    }

    public override int GetHashCode()
    {
        return Email.GetHashCode() ^ Login.GetHashCode();
    }
}
