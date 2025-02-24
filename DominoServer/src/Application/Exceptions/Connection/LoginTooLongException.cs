namespace Application.Exceptions.Connection;

public class LoginTooLongException(string message) : BaseApplicationException(message);
