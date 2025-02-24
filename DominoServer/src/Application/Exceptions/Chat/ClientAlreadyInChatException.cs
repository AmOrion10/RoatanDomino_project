namespace Application.Exceptions.Chat;

public class ClientAlreadyInChatException(string message) : BaseApplicationException(message);
