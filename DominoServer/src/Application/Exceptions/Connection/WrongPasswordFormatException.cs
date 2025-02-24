namespace Application.Exceptions.Connection;

public class WrongPasswordFormatException(string message) : BaseApplicationException(message);
