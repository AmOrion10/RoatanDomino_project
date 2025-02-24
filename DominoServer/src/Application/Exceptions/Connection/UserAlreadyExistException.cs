namespace Application.Exceptions.Connection;

public class UserAlreadyExistException(string message) : BaseApplicationException(message);
