namespace Application.Exceptions.User;

public class EmailCouldNotBeSentException(string message) : BaseApplicationException(message);
