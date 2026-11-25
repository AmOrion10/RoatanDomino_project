using System.Collections.Concurrent;

using Application.Interfaces;
using Application.Interfaces.Managers;

using Domain;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services;

public sealed class DirectChallengeResult
{
    public bool Ok { get; init; }
    public string? ErrorMessage { get; init; }
    public DirectChallengeActionResponseDto? Data { get; init; }

    public static DirectChallengeResult Success(DirectChallengeActionResponseDto data) => new() { Ok = true, Data = data };
    public static DirectChallengeResult Fail(string message) => new() { Ok = false, ErrorMessage = message };
}

public class DirectChallengeService(
    IClientManager clientManager,
    ILobbyManager lobbyManager)
{
    private long challengeIdSeq;
    private readonly ConcurrentDictionary<long, DirectChallengeDto> challenges = new();

    public async Task<DirectChallengeResult> SendDirectChallengeAsync(IClient challenger, SendDirectChallengeDto dto)
    {
        if (challenger.User is null || challenger.User.Id <= 0)
        {
            return DirectChallengeResult.Fail("You must be signed in to challenge another player.");
        }
        if (dto is null || dto.TargetUserId <= 0)
        {
            return DirectChallengeResult.Fail("Select a player to challenge.");
        }
        if (dto.TargetUserId == challenger.User.Id)
        {
            return DirectChallengeResult.Fail("You cannot challenge yourself.");
        }

        // Direct player challenges support Two-Best 1v1 or Cut-Throat 1v1 (Draw or Block)
        MatchFormat mode = dto.GameMode == MatchFormat.CUT_THROAT ? MatchFormat.CUT_THROAT : MatchFormat.TWO_BEST;
        TableStyle variant = dto.Variant == TableStyle.BLOCK ? TableStyle.BLOCK : TableStyle.DRAW;

        IClient? targetClient = clientManager.GetAllLoggedInClients()
            .FirstOrDefault(c => c.User is not null && c.User.Id == dto.TargetUserId);
        if (targetClient?.User is null)
        {
            return DirectChallengeResult.Fail("That player is currently offline.");
        }

        long id = Interlocked.Increment(ref challengeIdSeq);
        int challengerElo = challenger.User.Elo >= RoatanRankTierHelper.MinElo
            ? challenger.User.Elo
            : RoatanRankTierHelper.DefaultElo;

        DirectChallengeDto challenge = new()
        {
            ChallengeId = id,
            ChallengerUserId = challenger.User.Id,
            ChallengerUsername = challenger.User.Login,
            ChallengerElo = challengerElo,
            TargetUserId = targetClient.User.Id,
            TargetUsername = targetClient.User.Login,
            GameMode = mode,
            Variant = variant,
            Status = DirectChallengeStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
            Message = $"{challenger.User.Login} challenged you to a 1v1 match!"
        };
        challenges[id] = challenge;

        try
        {
            await targetClient.SendAsync(new NetworkFrame(MessageFrameBuilder.DirectChallengeNotificationFrame(challenge)));
        }
        catch { }

        return DirectChallengeResult.Success(new DirectChallengeActionResponseDto
        {
            Challenge = challenge
        });
    }

    public async Task<DirectChallengeResult> RespondDirectChallengeAsync(IClient responder, RespondDirectChallengeDto dto)
    {
        if (responder.User is null || responder.User.Id <= 0)
        {
            return DirectChallengeResult.Fail("You must be signed in to respond.");
        }
        if (dto is null || !challenges.TryGetValue(dto.ChallengeId, out DirectChallengeDto? challenge))
        {
            return DirectChallengeResult.Fail("Challenge not found or expired.");
        }
        if (challenge.TargetUserId != responder.User.Id)
        {
            return DirectChallengeResult.Fail("Only the challenged player can respond to this challenge.");
        }
        if (challenge.Status != DirectChallengeStatus.Pending)
        {
            return DirectChallengeResult.Fail("This challenge has already been handled.");
        }

        IClient? challengerClient = clientManager.GetAllLoggedInClients()
            .FirstOrDefault(c => c.User is not null && c.User.Id == challenge.ChallengerUserId);

        if (!dto.Accept)
        {
            challenge.Status = DirectChallengeStatus.Ignored;
            challenge.Message = $"{responder.User.Login} ignored your challenge.";
            if (challengerClient is not null)
            {
                try
                {
                    await challengerClient.SendAsync(new NetworkFrame(MessageFrameBuilder.DirectChallengeNotificationFrame(challenge)));
                }
                catch { }
            }

            return DirectChallengeResult.Success(new DirectChallengeActionResponseDto
            {
                Challenge = challenge
            });
        }

        if (challengerClient?.User is null)
        {
            challenge.Status = DirectChallengeStatus.Ignored;
            return DirectChallengeResult.Fail($"{challenge.ChallengerUsername} is no longer online.");
        }

        // Remove either player from any existing waiting lobby before seating them into the private 1v1 table
        Lobby? challengerLobby = lobbyManager.GetLobbyForClient(challengerClient);
        if (challengerLobby is not null)
        {
            lobbyManager.RemovePlayerFromLobby(challengerClient, challengerLobby.Id);
        }
        Lobby? responderLobby = lobbyManager.GetLobbyForClient(responder);
        if (responderLobby is not null)
        {
            lobbyManager.RemovePlayerFromLobby(responder, responderLobby.Id);
        }

        string tableName = $"{challengerClient.User.Login} vs {responder.User.Login}";
        Lobby lobby = lobbyManager.CreateLobby(
            challengerClient,
            maxNumberOfPlayer: 2,
            lobbyPublic: false,
            name: tableName,
            ranked: false,
            code: null,
            tableStyle: challenge.Variant,
            matchFormat: challenge.GameMode,
            oneOnePlayoff: false);

        lobbyManager.AddPlayerToLobby(responder, lobby.Id);

        challenge.Status = DirectChallengeStatus.Accepted;
        challenge.TableId = lobby.Id;
        challenge.TableCode = lobby.Code;
        challenge.Message = $"{responder.User.Login} accepted your 1v1 challenge!";

        LobbyDto lobbyDto = DtoBuilder.GetLobbyDtoObject(lobby);

        // Notify challenger so both players enter the 2-seat private table immediately!
        try
        {
            await challengerClient.SendAsync(new NetworkFrame([
                MessageFrameBuilder.TableStateUpdatedFrame(lobbyDto),
                MessageFrameBuilder.DirectChallengeNotificationFrame(challenge)
            ]));
        }
        catch { }

        return DirectChallengeResult.Success(new DirectChallengeActionResponseDto
        {
            Challenge = challenge,
            Table = lobbyDto
        });
    }
}
