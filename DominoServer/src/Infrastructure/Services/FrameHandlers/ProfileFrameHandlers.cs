using Application.Common;
using Application.Interfaces;
using Application.Messages.Commands.User;
using Application.Messages.Queries.User;

using Domain;
using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.FrameHandlers;

public class ProfileFrameHandlers(IMessageDispatcher dispatcher) : BaseFrameHandlers
{
    public override void RegisterHandlers(IMessageFrameHandler handler)
    {
        handler.RegisterHandler(FrameCommandType.GetInfoProfile, GetInfoProfileCallback);
        handler.RegisterHandler(FrameCommandType.UpdateProfile, UpdateProfileCallback);
        handler.RegisterHandler(FrameCommandType.UpdatePassword, UpdatePasswordCallback);
        handler.RegisterHandler(FrameCommandType.GetOtherUserInfo, GetOtherUserInfoCallback);
        handler.RegisterHandler(FrameCommandType.GetAllHistoryGames, GetAllHistoryGamesCallback);
        handler.RegisterHandler(FrameCommandType.GetHistoryGame, GetHistoryGameCallback);
    }

    private async Task GetInfoProfileCallback(MessageFrame frame, IClient client)
    {
        User user = await dispatcher.Dispatch(new GetUserInfoQuery(client));
        ResponseObjectDto<InfoProfileDto> response = DtoBuilder.GetInfoProfileDto(user);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }

    private async Task UpdateProfileCallback(MessageFrame frame, IClient client)
    {
        UpdateProfileDto dto = frame.GetObjectFromMessage<UpdateProfileDto>();
        await dispatcher.Dispatch(new UpdateUserInfoCommand
        {
            Login = dto.Login,
            Birthdate = dto.Birthdate,
            Nationality = string.IsNullOrWhiteSpace(dto.Nationality) ? null : dto.Nationality,
            Client = client
        });

        User user = await dispatcher.Dispatch(new GetUserInfoQuery(client));
        ResponseObjectDto<InfoProfileDto> response = DtoBuilder.GetInfoProfileDto(user);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, FrameCommandType.GetInfoProfile)));
    }

    private async Task UpdatePasswordCallback(MessageFrame frame, IClient client)
    {
        PasswordChangeDto dto = frame.GetObjectFromMessage<PasswordChangeDto>();
        await dispatcher.Dispatch(new UpdateUserPasswordCommand
        {
            ActualPassword = dto.ActualPassword,
            NewPassword = dto.NewPassword,
            Client = client
        });
        await SendOkToClient(client, frame.CommandType);
    }

    private async Task GetOtherUserInfoCallback(MessageFrame frame, IClient client)
    {
        GetOtherUserInfoDto dto = frame.GetObjectFromMessage<GetOtherUserInfoDto>();
        User user = await dispatcher.Dispatch(new GetUserInfoByLoginQuery(dto.Login));
        ResponseObjectDto<OtherUserInfoDto> response = DtoBuilder.GetOtherUserInfoDto(user);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }

    private async Task GetAllHistoryGamesCallback(MessageFrame frame, IClient client)
    {
        List<HistoryGame> historyGames = await dispatcher.Dispatch(new GetAllHistoryGamesQuery(client));
        ResponseObjectDto<AllHistoryGamesDto> response = DtoBuilder.GetAllHistoryGamesDto(historyGames);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }

    private async Task GetHistoryGameCallback(MessageFrame frame, IClient client)
    {
        GetHistoryGameDto dto = frame.GetObjectFromMessage<GetHistoryGameDto>();
        HistoryGame historyGame = await dispatcher.Dispatch(new GetHistoryGameQuery(dto.Id));
        ResponseObjectDto<HistoryGameDto> response = DtoBuilder.GetHistoryGameDto(historyGame);
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }
}