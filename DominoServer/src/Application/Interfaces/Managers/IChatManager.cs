using Domain;

namespace Application.Interfaces.Managers;

public interface IChatManager
{
    int GlobalChatId { get; }
    bool ClientIsInChat(IClient client, int chatId);
    void ClientJoinChat(IClient client, int chatId);
    void ClientLeaveChat(IClient client, int chatId);
    List<Chat> GetChatsForClient(IClient client);
    Chat? GetById(int chatId);
    int CreateChat(string name);
    void DeleteChat(int chatId);
}
