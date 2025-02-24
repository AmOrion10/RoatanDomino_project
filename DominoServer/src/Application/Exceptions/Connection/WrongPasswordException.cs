namespace Application.Exceptions.Connection;

public class WrongPasswordException(string message) : BaseApplicationException(message);
