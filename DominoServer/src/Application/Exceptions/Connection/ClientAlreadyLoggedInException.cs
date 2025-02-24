namespace Application.Exceptions.Connection;

public class ClientAlreadyLoggedInException(string message) : BaseApplicationException(message);
