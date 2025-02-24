namespace Domain;

public class LobbyPlayer(User user) : User(user)
{
    public bool IsReady { get; set; } = false;
}
