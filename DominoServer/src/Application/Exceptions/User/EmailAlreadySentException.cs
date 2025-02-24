namespace Application.Exceptions.User;

public class EmailAlreadySentException(string message) : BaseApplicationException(message);
