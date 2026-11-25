using Application.Interfaces;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Teams;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Infrastructure.Services.FrameHandlers;

public class TeamFrameHandlers(TeamService teamService) : BaseFrameHandlers
{
    public override void RegisterHandlers(IMessageFrameHandler handler)
    {
        handler.RegisterHandler(FrameCommandType.GetTeams, GetTeamsCallback);
        handler.RegisterHandler(FrameCommandType.GetMyTeam, GetMyTeamCallback);
        handler.RegisterHandler(FrameCommandType.CreateTeam, CreateTeamCallback);
        handler.RegisterHandler(FrameCommandType.UpdateTeamSettings, UpdateTeamSettingsCallback);
        handler.RegisterHandler(FrameCommandType.JoinOrRequestTeam, JoinOrRequestTeamCallback);
        handler.RegisterHandler(FrameCommandType.RespondTeamJoinRequest, RespondTeamJoinRequestCallback);
        handler.RegisterHandler(FrameCommandType.InviteToTeam, InviteToTeamCallback);
        handler.RegisterHandler(FrameCommandType.RespondTeamInvite, RespondTeamInviteCallback);
        handler.RegisterHandler(FrameCommandType.PromoteOrDemoteMember, PromoteOrDemoteMemberCallback);
        handler.RegisterHandler(FrameCommandType.KickTeamMember, KickTeamMemberCallback);
        handler.RegisterHandler(FrameCommandType.LeaveTeam, LeaveTeamCallback);
    }

    private async Task GetTeamsCallback(MessageFrame frame, IClient client)
    {
        GetTeamsRequestDto? req = string.IsNullOrWhiteSpace(frame.Message)
            ? null
            : frame.GetObjectFromMessage<GetTeamsRequestDto>();

        TeamsListDto result = await teamService.GetTeamsAsync(client.User!.Id, req?.SearchQuery);
        ResponseObjectDto<TeamsListDto> response = new()
        {
            OkResult = true,
            Object = result
        };
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }

    private async Task GetMyTeamCallback(MessageFrame frame, IClient client)
    {
        MyTeamDetailsDto details = await teamService.GetMyTeamAsync(client.User!.Id);
        ResponseObjectDto<MyTeamDetailsDto> response = new()
        {
            OkResult = true,
            Object = details
        };
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(response, frame.CommandType)));
    }

    private async Task CreateTeamCallback(MessageFrame frame, IClient client)
    {
        CreateTeamDto dto = frame.GetObjectFromMessage<CreateTeamDto>();
        TeamActionResult<MyTeamDetailsDto> result = await teamService.CreateTeamAsync(client.User!.Id, dto);
        await SendTeamResultAsync(client, frame.CommandType, result);
    }

    private async Task UpdateTeamSettingsCallback(MessageFrame frame, IClient client)
    {
        UpdateTeamSettingsDto dto = frame.GetObjectFromMessage<UpdateTeamSettingsDto>();
        TeamActionResult<MyTeamDetailsDto> result = await teamService.UpdateTeamSettingsAsync(client.User!.Id, dto);
        await SendTeamResultAsync(client, frame.CommandType, result);
    }

    private async Task JoinOrRequestTeamCallback(MessageFrame frame, IClient client)
    {
        JoinOrRequestTeamDto dto = frame.GetObjectFromMessage<JoinOrRequestTeamDto>();
        TeamActionResult<MyTeamDetailsDto> result = await teamService.JoinOrRequestTeamAsync(client.User!.Id, dto.TeamId);
        await SendTeamResultAsync(client, frame.CommandType, result);
    }

    private async Task RespondTeamJoinRequestCallback(MessageFrame frame, IClient client)
    {
        RespondTeamJoinRequestDto dto = frame.GetObjectFromMessage<RespondTeamJoinRequestDto>();
        TeamActionResult<MyTeamDetailsDto> result = await teamService.RespondTeamJoinRequestAsync(client.User!.Id, dto.RequestId, dto.Approve);
        await SendTeamResultAsync(client, frame.CommandType, result);
    }

    private async Task InviteToTeamCallback(MessageFrame frame, IClient client)
    {
        InviteToTeamDto dto = frame.GetObjectFromMessage<InviteToTeamDto>();
        TeamActionResult<MyTeamDetailsDto> result = await teamService.InviteToTeamAsync(client.User!.Id, dto);
        await SendTeamResultAsync(client, frame.CommandType, result);
    }

    private async Task RespondTeamInviteCallback(MessageFrame frame, IClient client)
    {
        RespondTeamInviteDto dto = frame.GetObjectFromMessage<RespondTeamInviteDto>();
        TeamActionResult<MyTeamDetailsDto> result = await teamService.RespondTeamInviteAsync(client.User!.Id, dto.InviteId, dto.Accept);
        await SendTeamResultAsync(client, frame.CommandType, result);
    }

    private async Task PromoteOrDemoteMemberCallback(MessageFrame frame, IClient client)
    {
        PromoteOrDemoteMemberDto dto = frame.GetObjectFromMessage<PromoteOrDemoteMemberDto>();
        TeamActionResult<MyTeamDetailsDto> result = await teamService.PromoteOrDemoteMemberAsync(client.User!.Id, dto.TargetUserId, dto.NewRole);
        await SendTeamResultAsync(client, frame.CommandType, result);
    }

    private async Task KickTeamMemberCallback(MessageFrame frame, IClient client)
    {
        KickTeamMemberDto dto = frame.GetObjectFromMessage<KickTeamMemberDto>();
        TeamActionResult<MyTeamDetailsDto> result = await teamService.KickTeamMemberAsync(client.User!.Id, dto.TargetUserId);
        await SendTeamResultAsync(client, frame.CommandType, result);
    }

    private async Task LeaveTeamCallback(MessageFrame frame, IClient client)
    {
        TeamActionResult<MyTeamDetailsDto> result = await teamService.LeaveTeamAsync(client.User!.Id);
        await SendTeamResultAsync(client, frame.CommandType, result);
    }

    private static async Task SendTeamResultAsync(
        IClient client,
        FrameCommandType commandType,
        TeamActionResult<MyTeamDetailsDto> result)
    {
        if (result.Ok && result.Data is not null)
        {
            ResponseObjectDto<MyTeamDetailsDto> okResponse = new()
            {
                OkResult = true,
                Object = result.Data
            };
            await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(okResponse, commandType)));
            return;
        }

        ResponseObjectDto<MyTeamDetailsDto> errorResponse = new()
        {
            OkResult = false,
            ErrorType = ErrorTypes.Unknown,
            ErrorMessage = result.ErrorMessage ?? "Team action failed."
        };
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(errorResponse, commandType)));
    }
}
