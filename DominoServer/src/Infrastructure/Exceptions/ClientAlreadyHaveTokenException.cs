namespace Infrastructure.Exceptions;

public class ClientAlreadyHaveTokenException(string message) : Exception(message);