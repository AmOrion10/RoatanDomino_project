using Application.Interfaces;
using Application.Interfaces.Managers;

using Domain;

namespace Infrastructure.Services.Managers;

public class ChatManager : IChatManager
{
    public int GlobalChatId => 1;
    private static int chatIdCounter = 2;
    private readonly List<Chat> chats;

    public ChatManager()
    {
        chats = [ new Chat(GlobalChatId, "Global") ];
    }

    public bool ClientIsInChat(IClient client, int chatId)
    {
        lock (chats)
        {
            Chat chat = chats.First(c => c.Id == chatId);
            return chat.Users.Any(u => u.Id == client.User!.Id);
        }
    }

    public void ClientJoinChat(IClient client, int chatId)
    {
        lock (chats)
        {
            Chat chat = chats.First(c => c.Id == chatId);
            chat.Users.Add(client.User!);
        }
    }

    public void ClientLeaveChat(IClient client, int chatId)
    {
        lock (chats)
        {
            Chat chat = chats.First(c => c.Id == chatId);
            chat.Users.Remove(client.User!);
        }
    }

    public List<Chat> GetChatsForClient(IClient client)
    {
        lock (chats)
        {
            return chats.Where(c => c.Users.Any(u => u.Id == client.User!.Id)).ToList();
        }
    }

    public Chat? GetById(int chatId)
    {
        lock (chats)
        {
            return chats.FirstOrDefault(c => c.Id == chatId);
        }
    }

    public int CreateChat(string name)
    {
        int chatId = chatIdCounter++;
        lock (chats)
        {
            chats.Add(new Chat(chatId, name));
        }
        return chatId;
    }

    public void DeleteChat(int chatId)
    {
        lock (chats)
        {
            chats.Remove(chats.First(c => c.Id == chatId));
        }
    }
}
