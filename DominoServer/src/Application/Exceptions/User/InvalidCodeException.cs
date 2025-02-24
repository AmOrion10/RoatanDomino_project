namespace Application.Exceptions.User;

public class InvalidCodeException(string message) : BaseApplicationException(message);
