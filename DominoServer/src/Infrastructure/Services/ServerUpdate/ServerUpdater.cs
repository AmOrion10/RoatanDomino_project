namespace Infrastructure.Services.ServerUpdate;

public class ServerUpdater(LobbyServerUpdater lobbyUpdater, GameServerUpdater gameUpdater)
{
    public async Task UpdateAsync(TimeSpan deltaTime)
    {
        await lobbyUpdater.UpdateAsync(deltaTime);
        await gameUpdater.UpdateAsync(deltaTime);
    }
}
