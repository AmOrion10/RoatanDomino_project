using Application.Exceptions;
using Application.Exceptions.Chat;
using Application.Exceptions.Connection;
using Application.Exceptions.Friends;
using Application.Exceptions.Lobby;
using Application.Exceptions.Player;
using Application.Exceptions.User;

using DominoCommon.com.domino.common.Scripts.Networking;

namespace Infrastructure.ExtensionMethods;

public static class ExceptionExtensionMethods
{
    private static readonly Dictionary<Type, ErrorTypes> TypesDict = new()
    {
        { typeof(UserAlreadyExistException), ErrorTypes.UserAlreadyExist },
        { typeof(UserNotExistingException), ErrorTypes.UserNotFound },
        { typeof(PlayerAlreadyCreatedALobbyException), ErrorTypes.CreationLobbyFailed },
        { typeof(MaxNumberOfPlayerReachedException), ErrorTypes.LobbyFull },
        { typeof(CannotJoinRankedLobbyByCodeException), ErrorTypes.CantAccessRankedLobbyByCode },
        { typeof(PlayerAlreadyInALobbyException), ErrorTypes.AlreadyInALobby },
        { typeof(PlayerNotInALobbyException), ErrorTypes.PlayerNotInALobby },
        { typeof(LobbyNotExistingException), ErrorTypes.LobbyNotFound },
        { typeof(WrongPasswordException), ErrorTypes.WrongLoggingOrPassword },
        { typeof(ClientAlreadyLoggedInException), ErrorTypes.UserAlreadyLoggedIn },
        { typeof(ClientAlreadyLogoutException), ErrorTypes.UserAlreadyLoggedOut },
        { typeof(ClientNotLoggedInException), ErrorTypes.UserNotLoggedIn },
        { typeof(ClientAlreadyInChatException), ErrorTypes.UserAlreadyConnectedToChat },
        { typeof(WrongPositionForDominoException), ErrorTypes.WrongPositionForDomino },
        { typeof(EmptyStackException), ErrorTypes.EmptyStack },
        { typeof(FriendRequestNotFoundException), ErrorTypes.NoFriendRequest },
        { typeof(AlreadyFriendException), ErrorTypes.AlreadyFriend },
        { typeof(NotInFriendListException), ErrorTypes.NotInFriendsList },
        { typeof(ClientNotInChatException), ErrorTypes.UserNotInChat },
        { typeof(LoginTooLongException), ErrorTypes.LoginTooLong },
        { typeof(InvalidPasswordFormatException), ErrorTypes.InvalidPasswordFormat },
        { typeof(InvalidEmailFormatException), ErrorTypes.InvalidEmailFormat },
        { typeof(InvalidDateFormatException), ErrorTypes.InvalidDateFormat },
        { typeof(InvalidCountryException), ErrorTypes.InvalidCountry },
        { typeof(EmailAlreadySentException), ErrorTypes.CodeAlreadySent },
        { typeof(InvalidCodeException), ErrorTypes.InvalidCode },
        { typeof(EmailCouldNotBeSentException), ErrorTypes.EmailSendError }
    };

    public static ErrorTypes ToErrorType(this Exception exception)
    {
        return TypesDict.ContainsKey(exception.GetType()) ? TypesDict[exception.GetType()] : ErrorTypes.Unknown;
    }
}
