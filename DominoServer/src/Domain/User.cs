namespace Domain;

public class User(string email, string login)
{
    public long Id { get; set; }
    public string Email { get; } = email;
    public string Login { get; set; } = login;
    public int Score { get; set; }
    public DateTime DateSignup { get; set; }
    public DateOnly Birthdate { get; set; }
    public string? Nationality { get; set; }
    public string? AvatarUrl { get; set; }
    public string? AvatarBase64 { get; set; }
    public string AuthProvider { get; set; } = "Email";
    public UserStats? Statistics { get; set; }

    public User(User user) : this(user.Email, user.Login)
    {
        Id = user.Id;
        Score = user.Score;
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
