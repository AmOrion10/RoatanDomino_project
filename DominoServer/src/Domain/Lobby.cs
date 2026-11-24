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
    bool? oneOnePlayoff = null)
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
    public int ChatId { get; set; }

    public bool IsEmpty()
    {
        return PlayersInLobby.Count == 0;
    }

    public bool IsFull()
    {
        return PlayersInLobby.Count == MaxNumberOfPlayer;
    }
}
