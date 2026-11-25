using DominoCommon.com.domino.common.Scripts.Enums;

namespace Domain;

public class Lobby(
    int id,
    LobbyPlayer creator,
    int maxNumberOfPlayer,
    bool isPublic,
    string name,
    bool ranked,
    string code,
    TableStyle? tableStyle = null,
    MatchFormat? matchFormat = null,
    bool? oneOnePlayoff = null,
    bool isHouseTable = false)
{
    public int Id { get; } = id;
    public LobbyPlayer Creator { get; set; } = creator;
    public int MaxNumberOfPlayer { get; } = maxNumberOfPlayer;
    public List<LobbyPlayer> PlayersInLobby { get; set; } = [];
    public Timer Timer { get; } = new();
    public bool IsPublic { get; } = isPublic;
    public string Name { get; } = name;
    public bool Ranked { get; } = ranked;
    public string Code { get; } = code;
    public TableStyle? TableStyle { get; } = tableStyle;
    public MatchFormat? MatchFormat { get; } = matchFormat;
    public bool? OneOnePlayoff { get; } = oneOnePlayoff;
    public bool IsHouseTable { get; } = isHouseTable;
    public int ChatId { get; set; }
    public bool IsTeamChallenge { get; set; }
    public long? TeamChallengeId { get; set; }
    public long? TeamAId { get; set; }
    public string? TeamAName { get; set; }
    public string? TeamATag { get; set; }
    public long? TeamBId { get; set; }
    public string? TeamBName { get; set; }
    public string? TeamBTag { get; set; }
    public bool RequireFullHumanSquad { get; set; }
    public DateTime? PlayDeadlineUtc { get; set; }

    public bool IsEmpty()
    {
        return PlayersInLobby.Count == 0;
    }

    public bool IsFull()
    {
        return PlayersInLobby.Count >= MaxNumberOfPlayer;
    }

    public List<LobbyPlayer> GetHumanPlayers()
    {
        return PlayersInLobby.Where(p => !p.IsBot).OrderBy(p => p.SeatIndex).ToList();
    }

    public List<LobbyPlayer> GetSeatedPlayersOrdered()
    {
        return PlayersInLobby.OrderBy(p => p.SeatIndex).ToList();
    }

    public LobbyPlayer? GetPlayerAtSeat(int seatIndex)
    {
        return PlayersInLobby.FirstOrDefault(p => p.SeatIndex == seatIndex);
    }

    public int FindFirstOpenSeat()
    {
        for (int i = 0; i < MaxNumberOfPlayer; i++)
        {
            if (PlayersInLobby.All(p => p.SeatIndex != i))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Auto-seats Team A on North/South seats (0 &amp; 2 in 2v2, or 0 in 1v1)
    /// and Team B on East/West seats (1 &amp; 3 in 2v2, or 1 in 1v1).
    /// </summary>
    public int FindOpenSeatForTeam(long? playerTeamId)
    {
        if (!IsTeamChallenge)
        {
            return FindFirstOpenSeat();
        }
        if (!playerTeamId.HasValue)
        {
            return -1;
        }

        int[] allowedSeats;
        if (TeamAId.HasValue && playerTeamId.Value == TeamAId.Value)
        {
            allowedSeats = MaxNumberOfPlayer == 4 ? [0, 2] : [0];
        }
        else if (TeamBId.HasValue && playerTeamId.Value == TeamBId.Value)
        {
            allowedSeats = MaxNumberOfPlayer == 4 ? [1, 3] : [1];
        }
        else
        {
            return -1;
        }

        foreach (int seat in allowedSeats)
        {
            if (seat < MaxNumberOfPlayer && PlayersInLobby.All(p => p.SeatIndex != seat))
            {
                return seat;
            }
        }

        return -1;
    }

    public bool IsHost(long userId)
    {
        if (userId <= 0) return false;
        if (Creator.Id == userId) return true;
        if (IsHouseTable && Creator.Id == 0)
        {
            LobbyPlayer? firstHuman = GetHumanPlayers().FirstOrDefault();
            return firstHuman is not null && firstHuman.Id == userId;
        }
        return false;
    }

    public bool TrySwitchSeat(long playerId, int targetSeatIndex)
    {
        if (targetSeatIndex < 0 || targetSeatIndex >= MaxNumberOfPlayer) return false;
        if (GetPlayerAtSeat(targetSeatIndex) is not null) return false;

        LobbyPlayer? player = PlayersInLobby.FirstOrDefault(p => p.Id == playerId && !p.IsBot);
        if (player is null) return false;

        player.SeatIndex = targetSeatIndex;
        return true;
    }
}
