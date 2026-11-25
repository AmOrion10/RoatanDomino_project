using DominoCommon.com.domino.common.Scripts.ExtensionMethods;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

namespace DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame
{
    public static class MessageFrameBuilder
    {
        public static MessageFrame ResponseFrame(ResponseDto response, FrameCommandType subCommandtype)
            => new MessageFrame(FrameCommandType.Response, subCommandtype, response.ToJson());

        public static MessageFrame ConnectionFrame(LoggingDto dto)
            => new MessageFrame(FrameCommandType.Connection, dto.ToJson());

        public static MessageFrame RegistrationFrame(RegistrationDto dto)
            => new MessageFrame(FrameCommandType.Registration, dto.ToJson());

        public static MessageFrame DisconnectionFrame()
            => new MessageFrame(FrameCommandType.Disconnection, string.Empty);

        public static MessageFrame CreateLobbyFrame(CreateLobbyDto dto)
            => new MessageFrame(FrameCommandType.CreationLobby, dto.ToJson());

        public static MessageFrame GetAllLobbyFrame()
            => new MessageFrame(FrameCommandType.GetAllLobbies, string.Empty);

        public static MessageFrame GetLobbiesWithNameFrame(LobbyNameDto dto)
            => new MessageFrame(FrameCommandType.GetLobbiesWithName, dto.ToJson());

        public static MessageFrame JoinLobbyFrame(JoinLobbyDto dto)
            => new MessageFrame(FrameCommandType.JoinLobby, dto.ToJson());

        public static MessageFrame LeaveLobbyFrame()
            => new MessageFrame(FrameCommandType.LeaveLobby, string.Empty);

        public static MessageFrame ReadyLobbyFrame()
            => new MessageFrame(FrameCommandType.ReadyLobbyStateChanged, string.Empty);

        public static MessageFrame JoinRankedFrame()
            => new MessageFrame(FrameCommandType.JoinRanked, string.Empty);

        public static MessageFrame SendMessageFrame(SendChatMessageDto dto)
            => new MessageFrame(FrameCommandType.SendChatMessage, dto.ToJson());

        public static MessageFrame ReceiveMessageFrame(ReceiveChatMessageDto dto)
            => new MessageFrame(FrameCommandType.ReceiveChatMessage, dto.ToJson());

        public static MessageFrame LobbyTimerUpdatedFrame(TimerDto dto)
            => new MessageFrame(FrameCommandType.LobbyTimerUpdated, dto.ToJson());

        public static MessageFrame LobbyTimerExpiredFrame()
            => new MessageFrame(FrameCommandType.LobbyTimerExpired, string.Empty);

        public static MessageFrame GetInfoProfileFrame()
            => new MessageFrame(FrameCommandType.GetInfoProfile, string.Empty);

        public static MessageFrame GetOtherUserInfoFrame(GetOtherUserInfoDto dto)
            => new MessageFrame(FrameCommandType.GetOtherUserInfo, dto.ToJson());

        public static MessageFrame UpdateProfileFrame(UpdateProfileDto dto)
            => new MessageFrame(FrameCommandType.UpdateProfile, dto.ToJson());

        public static MessageFrame UpdatePasswordFrame(PasswordChangeDto dto)
            => new MessageFrame(FrameCommandType.UpdatePassword, dto.ToJson());

        public static MessageFrame GetAllHistoryGames()
            => new MessageFrame(FrameCommandType.GetAllHistoryGames, string.Empty);

        public static MessageFrame GetHistoryGame(GetHistoryGameDto dto)
            => new MessageFrame(FrameCommandType.GetHistoryGame, dto.ToJson());

        public static MessageFrame ReadyInGameFrame()
            => new MessageFrame(FrameCommandType.ReadyInGame, string.Empty);

        public static MessageFrame GameStartingTimerFrame(TimerDto dto)
            => new MessageFrame(FrameCommandType.GameStartingTimer, dto.ToJson());

        public static MessageFrame GameGeneralTimerFrame(TimerDto dto)
            => new MessageFrame(FrameCommandType.GameGeneralTimer, dto.ToJson());

        public static MessageFrame DominoDistributionFrame(HandDto dto)
            => new MessageFrame(FrameCommandType.DominoDistribution, dto.ToJson());

        public static MessageFrame ChangePlayerTurnFrame(PlayerTurnDto dto)
            => new MessageFrame(FrameCommandType.ChangePlayerTurn, dto.ToJson());

        public static MessageFrame PlayerPlaysFrame(PlayerPlayDto dto)
            => new MessageFrame(FrameCommandType.PlayerPlays, dto.ToJson());

        public static MessageFrame PlayerJoinedLobbyFrame(PlayerDto dto)
            => new MessageFrame(FrameCommandType.PlayerJoinedLobby, dto.ToJson());

        public static MessageFrame PlayerLeftLobbyFrame(PlayerDto dto)
            => new MessageFrame(FrameCommandType.PlayerLeftLobby, dto.ToJson());

        public static MessageFrame LobbyCreatorChangedFrame(PlayerDto dto)
            => new MessageFrame(FrameCommandType.LobbyCreatorChanged, dto.ToJson());

        public static MessageFrame PlayerReadyChangedFrame(PlayerReadyDto dto)
            => new MessageFrame(FrameCommandType.LobbyPlayerReadyChanged, dto.ToJson());

        public static MessageFrame PossibleMovesFrame(PossibleMovesDto dto)
            => new MessageFrame(FrameCommandType.PossibleMoves, dto.ToJson());

        public static MessageFrame NumberOfDominoPerPlayerFrame(NumberOfDominoPerPlayerDto dto)
            => new MessageFrame(FrameCommandType.NumberOfDominoPerPlayer, dto.ToJson());

        public static MessageFrame EndGameFrame(EndGameDto dto)
            => new MessageFrame(FrameCommandType.EndGame, dto.ToJson());

        public static MessageFrame FriendRequestFrame(FriendRequestDto dto)
            => new MessageFrame(FrameCommandType.SendFriendRequest, dto.ToJson());

        public static MessageFrame AcceptFriendRequestFrame(FriendRequestDto dto)
            => new MessageFrame(FrameCommandType.AcceptFriendRequest, dto.ToJson());

        public static MessageFrame DeclineFriendRequestFrame(FriendRequestDto dto)
            => new MessageFrame(FrameCommandType.DeclineFriendRequest, dto.ToJson());

        public static MessageFrame GetAllFriendRequestsFrame()
            => new MessageFrame(FrameCommandType.GetAllFriendRequests, string.Empty);

        public static MessageFrame RemoveFriendFrame(FriendDto dto)
            => new MessageFrame(FrameCommandType.RemoveFriend, dto.ToJson());

        public static MessageFrame GetAllFriendsFrame()
            => new MessageFrame(FrameCommandType.GetAllFriends, string.Empty);

        public static MessageFrame JoinLobbyWithCodeFrame(LobbyCodeDto dto)
            => new MessageFrame(FrameCommandType.JoinLobbyWithCode, dto.ToJson());

        public static MessageFrame JoinChatFrame(ChatDto dto)
            => new MessageFrame(FrameCommandType.JoinChat, dto.ToJson());

        public static MessageFrame ReadyConnectedFrame()
            => new MessageFrame(FrameCommandType.ReadyConnected, string.Empty);

        public static MessageFrame LeaveGameFrame()
            => new MessageFrame(FrameCommandType.LeaveGame, string.Empty);

        public static MessageFrame PlayerLeftGameFrame(PlayerDto dto)
            => new MessageFrame(FrameCommandType.PlayerLeftGame, dto.ToJson());

        public static MessageFrame LeaveChatFrame(ChatDto dto)
            => new MessageFrame(FrameCommandType.LeaveChat, dto.ToJson());

        public static MessageFrame AskResetPasswordFrame(AskResetPasswordDto dto)
            => new MessageFrame(FrameCommandType.AskResetPassword, dto.ToJson());

        public static MessageFrame ResetPasswordFrame(ResetPasswordDto dto)
            => new MessageFrame(FrameCommandType.ResetPassword, dto.ToJson());

        public static MessageFrame CheckActiveMatchFrame(RejoinSessionRequestDto dto)
            => new MessageFrame(FrameCommandType.CheckActiveMatch, dto.ToJson());

        public static MessageFrame RejoinMatchFrame(RejoinSessionRequestDto dto)
            => new MessageFrame(FrameCommandType.RejoinMatch, dto.ToJson());

        public static MessageFrame AbandonMatchFrame(RejoinSessionRequestDto dto)
            => new MessageFrame(FrameCommandType.AbandonMatch, dto.ToJson());

        public static MessageFrame SocialAuthFrame(SocialAuthDto dto)
            => new MessageFrame(FrameCommandType.SocialAuth, dto.ToJson());

        public static MessageFrame GetPlayerRosterFrame()
            => new MessageFrame(FrameCommandType.GetPlayerRoster, string.Empty);

        public static MessageFrame PlayerRosterUpdatedFrame(PlayerRosterDto dto)
            => new MessageFrame(FrameCommandType.PlayerRosterUpdated, dto.ToJson());

        public static MessageFrame SendTableInviteFrame(TableInviteDto dto)
            => new MessageFrame(FrameCommandType.SendTableInvite, dto.ToJson());

        public static MessageFrame TableInviteReceivedFrame(TableInviteDto dto)
            => new MessageFrame(FrameCommandType.TableInviteReceived, dto.ToJson());

        public static MessageFrame SwitchTableSeatFrame(SwitchTableSeatDto dto)
            => new MessageFrame(FrameCommandType.SwitchTableSeat, dto.ToJson());

        public static MessageFrame AddTableBotFrame(AddTableBotDto dto)
            => new MessageFrame(FrameCommandType.AddTableBot, dto.ToJson());

        public static MessageFrame RemoveTableBotFrame(RemoveTableBotDto dto)
            => new MessageFrame(FrameCommandType.RemoveTableBot, dto.ToJson());

        public static MessageFrame StartTableMatchFrame()
            => new MessageFrame(FrameCommandType.StartTableMatch, string.Empty);

        public static MessageFrame TableStateUpdatedFrame(LobbyDto dto)
            => new MessageFrame(FrameCommandType.TableStateUpdated, dto.ToJson());

        public static MessageFrame UpdateProfileAvatarFrame(UpdateProfileAvatarDto dto)
            => new MessageFrame(FrameCommandType.UpdateProfileAvatar, dto.ToJson());

        public static MessageFrame DeleteAccountFrame()
            => new MessageFrame(FrameCommandType.DeleteAccount, string.Empty);
    }
}
