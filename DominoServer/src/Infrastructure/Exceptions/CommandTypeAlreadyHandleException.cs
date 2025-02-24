namespace Infrastructure.Exceptions;

public class CommandTypeAlreadyHandleException(string message) : Exception(message);
