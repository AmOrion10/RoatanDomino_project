namespace Application.Exceptions.Connection;

public class ClientNotLoggedInException(string message) : BaseApplicationException(message);
