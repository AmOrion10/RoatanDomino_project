namespace DominoCommon.com.domino.common.Scripts.Networking
{
    public enum ErrorTypes
    {
        InvalidTokenClient,

        // User
        UserAlreadyExist,
        WrongLoggingOrPassword,
        UserAlreadyLoggedIn,
        UserAlreadyLoggedOut,
        UserNotLoggedIn,
        UserNotFound,
        InvalidPasswordFormat,
        LoginTooLong,
        InvalidEmailFormat,
        InvalidDateFormat,
        InvalidCountry,
        CodeAlreadySent,
        InvalidCode,
        EmailSendError,

        // Lobby
        CreationLobbyFailed,
        LobbyFull,
        AlreadyInALobby,
        PlayerNotInALobby,
        LobbyNotFound,
        CantAccessRankedLobbyByCode,

        // Chat
        UserAlreadyConnectedToChat,
        UserNotInChat,

        // InGame
        WrongPositionForDomino,
        EmptyStack,

        // Friends
        NoFriendRequest,
        AlreadyFriend,
        NotInFriendsList,

        Unknown
    }
}
