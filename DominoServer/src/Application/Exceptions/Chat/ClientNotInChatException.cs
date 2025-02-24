namespace Application.Exceptions.Chat;

public class ClientNotInChatException(string message) : BaseApplicationException(message);
