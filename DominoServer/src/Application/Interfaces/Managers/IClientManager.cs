namespace Application.Interfaces.Managers;

public interface IClientManager
{
    void ConfigureMaxClients(int number);
    bool HasEnoughSpaceForAnotherClient();
    void HandleNewClient(IClient newClient);
    List<IClient> GetAllLoggedInClients();
    List<IClient> GetAllClients();
}