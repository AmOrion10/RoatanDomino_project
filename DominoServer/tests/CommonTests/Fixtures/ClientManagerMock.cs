using Application.Interfaces;
using Application.Interfaces.Managers;

namespace CommonTests.Fixtures;

public class ClientManagerMock : IClientManager
{
    private readonly List<IClient> clients = [];

    public void ConfigureMaxClients(int number)
    {
        // Nothing
    }

    public bool HasEnoughSpaceForAnotherClient()
    {
        return true;
    }

    public void HandleNewClient(IClient newClient)
    {
        clients.Add(newClient);
    }

    public List<IClient> GetAllLoggedInClients()
    {
        return clients.Where(c => c.IsLoggedIn).ToList();
    }

    public List<IClient> GetAllClients()
    {
        return clients;
    }
}
