namespace Application.Exceptions.Game;

public class GameNotFoundException(string message) : BaseApplicationException(message);