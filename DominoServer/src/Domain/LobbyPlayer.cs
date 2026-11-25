namespace Domain;

public class LobbyPlayer(User user) : User(user)
{
    public bool IsReady { get; set; } = false;
    public int SeatIndex { get; set; } = 0;
    public bool IsBot { get; set; } = false;
    public string? BotDifficulty { get; set; }

    public static LobbyPlayer CreateBot(long botId, string botName, int seatIndex, string difficulty)
    {
        string normalizedDifficulty = NormalizeDifficulty(difficulty);
        int botElo = normalizedDifficulty switch
        {
            "Novice" => 850,
            "Master" => 1550,
            _ => 1200
        };
        User botUser = new($"bot.{Math.Abs(botId)}@roatandomino.local", botName)
        {
            Id = botId,
            Score = botElo,
            Elo = botElo
        };

        return new LobbyPlayer(botUser)
        {
            IsReady = true,
            SeatIndex = seatIndex,
            IsBot = true,
            BotDifficulty = normalizedDifficulty
        };
    }

    public static string NormalizeDifficulty(string? difficulty)
    {
        if (string.Equals(difficulty, "Novice", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(difficulty, "Beginner", StringComparison.OrdinalIgnoreCase))
        {
            return "Novice";
        }
        if (string.Equals(difficulty, "Master", StringComparison.OrdinalIgnoreCase))
        {
            return "Master";
        }
        return "Pro";
    }
}
