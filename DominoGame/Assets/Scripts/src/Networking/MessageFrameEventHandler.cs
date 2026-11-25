using System;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Friends;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

/// <summary>
/// Cette classe est utilisée par Client.cs
/// On peut se subscribe à un event en passant par Client.EventHandler.NomDeLEvent += NomDeLaMethode
/// ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠
/// Il ne faut pas oublier de s'unsubscribe en faisant Client.EventHandler.NomDeLEvent -= NomDeLaMethode
/// dans l'event OnDisable ou OnDestroy du GameObject.
/// ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠ ⚠
/// </summary>
public class MessageFrameEventHandler
{
    public event Action<ResponseDto> RegistrationResponseEvent;
    public event Action<ResponseDto> ConnectionResponseEvent;
    public event Action<ResponseDto> DisconnectionResponseEvent;
    public event Action<ResponseObjectDto<LobbiesInfoDto>> GetAllLobbiesResponseEvent;
    public event Action<ResponseObjectDto<LobbiesInfoDto>> GetLobbiesWithNameResponseEvent;
    public event Action<ResponseObjectDto<LobbyDto>> CreateLobbyResponseEvent;
    public event Action<ResponseObjectDto<LobbyDto>> JoinLobbyResponseEvent;
    public event Action<ResponseDto> JoinLobbyWithCodeResponseEvent;
    public event Action<ResponseDto> PlayerPlaysResponseEvent;
    public event Action<ResponseDto> LeaveGameResponseEvent;
    public event Action<ResponseObjectDto<FriendsDto>> GetAllFriendsResponseEvent;
    public event Action<ResponseObjectDto<FriendRequestsDto>> GetAllFriendRequestsResponseEvent;
    public event Action<ResponseObjectDto<InfoProfileDto>> GetInfoProfileResponseEvent;
    public event Action<ResponseDto> AskResetPasswordResponseEvent;
    public event Action<ResponseDto> ResetPasswordResponseEvent;
    public event Action<ResponseObjectDto<OtherUserInfoDto>> GetOtherUserInfoResponseEvent;
    public event Action<ResponseDto> SendFriendRequestResponseEvent;
    public event Action<ResponseObjectDto<AllHistoryGamesDto>> GetAllHistoryGamesResponseEvent;
    public event Action<ResponseDto> UpdatePasswordResponseEvent;
    public event Action<ResponseObjectDto<ActiveMatchCheckResponseDto>> CheckActiveMatchResponseEvent;
    public event Action<ResponseObjectDto<RejoinMatchSnapshotDto>> RejoinMatchResponseEvent;
    public event Action<ResponseDto> AbandonMatchResponseEvent;
    public event Action<ResponseDto> SocialAuthResponseEvent;
    public event Action<ResponseObjectDto<PlayerRosterDto>> GetPlayerRosterResponseEvent;
    public event Action<ResponseObjectDto<LobbyDto>> SwitchTableSeatResponseEvent;
    public event Action<ResponseObjectDto<LobbyDto>> AddTableBotResponseEvent;
    public event Action<ResponseObjectDto<LobbyDto>> RemoveTableBotResponseEvent;
    public event Action<ResponseDto> StartTableMatchResponseEvent;
    public event Action<ResponseDto> SendTableInviteResponseEvent;
    public event Action<ResponseObjectDto<UserDto>> UpdateProfileAvatarResponseEvent;
    public event Action<ResponseDto> DeleteAccountResponseEvent;
    public event Action<ResponseObjectDto<LeaderboardDto>> GetLeaderboardResponseEvent;

    public event Action<PlayerDto> PlayerJoinedLobbyEvent;
    public event Action<PlayerDto> PlayerLeftLobbyEvent;
    public event Action<PlayerDto> LobbyCreatorChangedEvent;
    public event Action<TimerDto> LobbyTimerUpdatedEvent;
    public event Action<PlayerReadyDto> LobbyPlayerReadyStateChangedEvent;
    public event Action LobbyTimerExpiredEvent;
    public event Action<PlayerRosterDto> PlayerRosterUpdatedEvent;
    public event Action<TableInviteDto> TableInviteReceivedEvent;
    public event Action<LobbyDto> TableStateUpdatedEvent;
    public event Action<TimerDto> GameStartingTimerEvent;
    public event Action<TimerDto> GameGeneralTimerEvent;
    public event Action<HandDto> DominoDistrubitionEvent;
    public event Action<PlayerPlayDto> PlayerPlaysEvent;
    public event Action<PlayerTurnDto> ChangePlayerTurnEvent;
    public event Action<PossibleMovesDto> PossiblesMovesEvent;
    public event Action<NumberOfDominoPerPlayerDto> NumberOfDominoPerPlayerEvent;
    public event Action<EndGameDto> EndGameEvent;
    public event Action<MatchEloBreakdownDto> MatchEloBreakdownEvent;
    public event Action<ReceiveChatMessageDto> ReceiveChatMessageEvent;
    public event Action<ChatDto> JoinChatEvent;
    public event Action<ChatDto> LeaveChatEvent;
    public event Action<PlayerDto> PlayerLeftGameEvent;

    public void MessageFrameReceive(MessageFrame frame)
    {
        switch (frame.SubCommandType)
        {
            case FrameCommandType.Registration:
                RegistrationResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.Connection:
                // Even if the server gives us more than a simple ResponseDto, we don't need to pass this information to the event
                ConnectionResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.Disconnection:
                DisconnectionResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.GetAllLobbies:
                GetAllLobbiesResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<LobbiesInfoDto>>());
                break;
            case FrameCommandType.GetLobbiesWithName:
                GetLobbiesWithNameResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<LobbiesInfoDto>>());
                break;
            case FrameCommandType.CreationLobby:
                CreateLobbyResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<LobbyDto>>());
                break;
            case FrameCommandType.JoinLobby:
                JoinLobbyResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<LobbyDto>>());
                break;
            case FrameCommandType.PlayerPlays:
                PlayerPlaysResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.JoinLobbyWithCode:
                JoinLobbyWithCodeResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.LeaveGame:
                LeaveGameResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.GetAllFriendRequests:
                GetAllFriendRequestsResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<FriendRequestsDto>>());
                break;
            case FrameCommandType.GetAllFriends:
                GetAllFriendsResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<FriendsDto>>());
                break;
            case FrameCommandType.GetInfoProfile:
                GetInfoProfileResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<InfoProfileDto>>());
                break;
            case FrameCommandType.AskResetPassword:
                AskResetPasswordResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.ResetPassword:
                ResetPasswordResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.GetOtherUserInfo:
                GetOtherUserInfoResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<OtherUserInfoDto>>());
                break;
            case FrameCommandType.SendFriendRequest:
                SendFriendRequestResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.GetAllHistoryGames:
                GetAllHistoryGamesResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<AllHistoryGamesDto>>());
                break;
            case FrameCommandType.UpdatePassword:
                UpdatePasswordResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.CheckActiveMatch:
                CheckActiveMatchResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<ActiveMatchCheckResponseDto>>());
                break;
            case FrameCommandType.RejoinMatch:
                RejoinMatchResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<RejoinMatchSnapshotDto>>());
                break;
            case FrameCommandType.AbandonMatch:
                AbandonMatchResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.SocialAuth:
                SocialAuthResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.GetPlayerRoster:
                GetPlayerRosterResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<PlayerRosterDto>>());
                break;
            case FrameCommandType.SwitchTableSeat:
                SwitchTableSeatResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<LobbyDto>>());
                break;
            case FrameCommandType.AddTableBot:
                AddTableBotResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<LobbyDto>>());
                break;
            case FrameCommandType.RemoveTableBot:
                RemoveTableBotResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<LobbyDto>>());
                break;
            case FrameCommandType.StartTableMatch:
                StartTableMatchResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.SendTableInvite:
                SendTableInviteResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.UpdateProfileAvatar:
                UpdateProfileAvatarResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<UserDto>>());
                break;
            case FrameCommandType.DeleteAccount:
                DeleteAccountResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseDto>());
                break;
            case FrameCommandType.GetLeaderboard:
                GetLeaderboardResponseEvent?.Invoke(frame.GetObjectFromMessage<ResponseObjectDto<LeaderboardDto>>());
                break;
        }

        switch (frame.CommandType)
        {
            case FrameCommandType.PlayerRosterUpdated:
                PlayerRosterUpdatedEvent?.Invoke(frame.GetObjectFromMessage<PlayerRosterDto>());
                break;
            case FrameCommandType.TableInviteReceived:
                TableInviteReceivedEvent?.Invoke(frame.GetObjectFromMessage<TableInviteDto>());
                break;
            case FrameCommandType.TableStateUpdated:
                TableStateUpdatedEvent?.Invoke(frame.GetObjectFromMessage<LobbyDto>());
                break;
            case FrameCommandType.PlayerJoinedLobby:
                PlayerJoinedLobbyEvent?.Invoke(frame.GetObjectFromMessage<PlayerDto>());
                break;
            case FrameCommandType.PlayerLeftLobby:
                PlayerLeftLobbyEvent?.Invoke(frame.GetObjectFromMessage<PlayerDto>());
                break;
            case FrameCommandType.LobbyCreatorChanged:
                LobbyCreatorChangedEvent?.Invoke(frame.GetObjectFromMessage<PlayerDto>());
                break;
            case FrameCommandType.LobbyTimerUpdated:
                LobbyTimerUpdatedEvent?.Invoke(frame.GetObjectFromMessage<TimerDto>());
                break;
            case FrameCommandType.LobbyPlayerReadyChanged:
                LobbyPlayerReadyStateChangedEvent?.Invoke(frame.GetObjectFromMessage<PlayerReadyDto>());
                break;
            case FrameCommandType.LobbyTimerExpired:
                LobbyTimerExpiredEvent?.Invoke();
                break;
            case FrameCommandType.GameStartingTimer:
                GameStartingTimerEvent?.Invoke(frame.GetObjectFromMessage<TimerDto>());
                break;
            case FrameCommandType.DominoDistribution:
                DominoDistrubitionEvent?.Invoke(frame.GetObjectFromMessage<HandDto>());
                break;
            case FrameCommandType.PlayerPlays:
                PlayerPlaysEvent?.Invoke(frame.GetObjectFromMessage<PlayerPlayDto>());
                break;
            case FrameCommandType.ChangePlayerTurn:
                ChangePlayerTurnEvent?.Invoke(frame.GetObjectFromMessage<PlayerTurnDto>());
                break;
            case FrameCommandType.PossibleMoves:
                PossiblesMovesEvent?.Invoke(frame.GetObjectFromMessage<PossibleMovesDto>());
                break;
            case FrameCommandType.NumberOfDominoPerPlayer:
                NumberOfDominoPerPlayerEvent?.Invoke(frame.GetObjectFromMessage<NumberOfDominoPerPlayerDto>());
                break;
            case FrameCommandType.EndGame:
                EndGameEvent?.Invoke(frame.GetObjectFromMessage<EndGameDto>());
                break;
            case FrameCommandType.MatchEloBreakdown:
                MatchEloBreakdownEvent?.Invoke(frame.GetObjectFromMessage<MatchEloBreakdownDto>());
                break;
            case FrameCommandType.GameGeneralTimer:
                GameGeneralTimerEvent?.Invoke(frame.GetObjectFromMessage<TimerDto>());
                break;
            case FrameCommandType.ReceiveChatMessage:
                ReceiveChatMessageEvent?.Invoke(frame.GetObjectFromMessage<ReceiveChatMessageDto>());
                break;
            case FrameCommandType.JoinChat:
                JoinChatEvent?.Invoke(frame.GetObjectFromMessage<ChatDto>());
                break;
            case FrameCommandType.LeaveChat:
                LeaveChatEvent?.Invoke(frame.GetObjectFromMessage<ChatDto>());
                break;
            case FrameCommandType.PlayerLeftGame:
                PlayerLeftGameEvent?.Invoke(frame.GetObjectFromMessage<PlayerDto>());
                break;
        }
    }
}
