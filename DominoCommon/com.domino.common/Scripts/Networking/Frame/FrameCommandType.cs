namespace DominoCommon.com.domino.common.Scripts.Networking.Frame
{
    public enum FrameCommandType
    {
        None = 1,

        // Account Command Type
        Registration,
        Connection,
        Disconnection,
        GetInfoProfile,
        GetOtherUserInfo,
        UpdateProfile,
        UpdatePassword,
        ReadyConnected,
        GetAllHistoryGames,
        GetHistoryGame,
        AskResetPassword,
        ResetPassword,

        // Lobby Creation Command Type
        CreationLobby,
        JoinLobby,
        JoinLobbyWithCode,
        LeaveLobby,
        JoinRanked,
        GetAllLobbies,
        GetLobbiesWithName,
        ReadyLobbyStateChanged,
        LobbyTimerUpdated,
        LobbyTimerExpired,
        PlayerJoinedLobby,
        PlayerLeftLobby,
        LobbyPlayerReadyChanged,
        LobbyCreatorChanged,

        // In Game Command Type
        ReadyInGame,
        GameStartingTimer,
        GameGeneralTimer,
        DominoDistribution,
        ChangePlayerTurn,
        PlayerPlays,
        PossibleMoves,
        NumberOfDominoPerPlayer,
        EndGame,
        LeaveGame,
        PlayerLeftGame,

        // Chat Command Type
        JoinChat,
        SendChatMessage,
        ReceiveChatMessage,
        LeaveChat,

        // Friends
        SendFriendRequest,
        AcceptFriendRequest,
        DeclineFriendRequest,
        GetAllFriendRequests,
        RemoveFriend,
        GetAllFriends,

        // Other
        Response, // Good or bad result
    }
}
