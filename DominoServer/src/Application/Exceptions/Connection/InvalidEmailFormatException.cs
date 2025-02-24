namespace Application.Exceptions.Connection;

public class InvalidEmailFormatException(string message) : BaseApplicationException(message);
