namespace Application.Exceptions.Connection;

public class UserNotExistingException(string message) : BaseApplicationException(message);
