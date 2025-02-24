namespace Application.Exceptions.Connection;

public class InvalidPasswordFormatException(string message) : BaseApplicationException(message);
