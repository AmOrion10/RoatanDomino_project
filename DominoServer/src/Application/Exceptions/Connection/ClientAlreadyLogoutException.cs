namespace Application.Exceptions.Connection;

public class ClientAlreadyLogoutException(string message) : BaseApplicationException(message);
