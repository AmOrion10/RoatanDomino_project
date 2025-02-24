namespace Application.Exceptions.Chat;

public class ChatNotFoundException(string message) : BaseApplicationException(message);
