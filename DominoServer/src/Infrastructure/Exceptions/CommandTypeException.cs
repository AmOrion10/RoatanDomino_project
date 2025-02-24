namespace Infrastructure.Exceptions;

public class CommandTypeNotHandledException(string message) : Exception(message);
