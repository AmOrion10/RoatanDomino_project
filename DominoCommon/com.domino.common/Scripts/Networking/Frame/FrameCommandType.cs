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

        // Match Rejoin & Disconnect Persistence
        CheckActiveMatch,
        RejoinMatch,
        AbandonMatch,

        // Social Auth, Player Presence & Table Waiting Room
        SocialAuth,
        GetPlayerRoster,
        PlayerRosterUpdated,
        SendTableInvite,
        TableInviteReceived,
        SwitchTableSeat,
        AddTableBot,
        RemoveTableBot,
        StartTableMatch,
        TableStateUpdated,

        // Profile Avatars & Account Deletion
        UpdateProfileAvatar,
        DeleteAccount,

        // Elo Ranking & Player Leaderboard
        GetLeaderboard,
        MatchEloBreakdown,

        // Teams / Crews System
        GetTeams,
        GetMyTeam,
        CreateTeam,
        UpdateTeamSettings,
        JoinOrRequestTeam,
        RespondTeamJoinRequest,
        InviteToTeam,
        RespondTeamInvite,
        PromoteOrDemoteMember,
        KickTeamMember,
        LeaveTeam,

        // Team Challenges & Team Leaderboard
        GetTeamLeaderboard,
        SendTeamChallenge,
        RespondTeamChallenge,
        TeamChallengeNotification,

        // Live Match Spectating, Direct Player Challenges & Team/DM Chat
        JoinAsSpectator,
        LeaveSpectator,
        SpectatorCountUpdated,
        SendDirectChallenge,
        RespondDirectChallenge,
        DirectChallengeNotification,
        GetChatHistory,
        ChatMessageReceived,

        // Open Team Challenge Board & Ask Leaders Flow
        GetOpenTeamChallenges,
        PostOpenTeamChallenge,
        CancelOpenTeamChallenge,
        RequestAcceptTeamChallenge,
        AcceptOpenTeamChallenge,
    }
}
