using Application.Exceptions;
using Application.Exceptions.Connection;
using Application.Interfaces;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

using Infrastructure.Exceptions;
using Infrastructure.ExtensionMethods;

using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class MessageFrameHandler(ILogger<MessageFrameHandler> logger) : IMessageFrameHandler
{
    private readonly Dictionary<FrameCommandType, Func<MessageFrame, IClient, Task>> handlers = [];
    private readonly Dictionary<FrameCommandType, bool> commandTypesNeedToBeLoggedIn = [];

    public void RegisterHandler(FrameCommandType commandType, Func<MessageFrame, IClient, Task> handler, bool needToBeLoggedIn = true)
    {
        if (!handlers.TryAdd(commandType, handler) || !commandTypesNeedToBeLoggedIn.TryAdd(commandType, needToBeLoggedIn))
        {
            throw new CommandTypeAlreadyHandleException($"The command type {commandType} has already an handler");
        }
    }

    public async Task HandleAsync(MessageFrame frame, IClient client)
    {
        if (!handlers.TryGetValue(frame.CommandType, out Func<MessageFrame, IClient, Task>? handler))
        {
            throw new CommandTypeNotHandledException($"The command type {frame.CommandType} doesn't have an handler");
        }

        try
        {
            if (commandTypesNeedToBeLoggedIn[frame.CommandType] && !client.IsLoggedIn)
            {
                throw new ClientNotLoggedInException($"Client is not logged in for the command type {frame.CommandType}");
            }
            logger.LogInformation("Receiving [{type}] from {clientId} : {message}", frame.CommandType, client.Id, frame.Message);
            await handler(frame, client);
        }
        catch (BaseApplicationException exception)
        {
            logger.LogError("Error with client {id} : {ex}", client.Id, exception);
            await SendErrorToClient(client, exception, frame.CommandType);
        } 
    }

    private static async Task SendErrorToClient(IClient client, Exception exception, FrameCommandType sendedCommandType)
    {
        ResponseDto dto = new()
        {
            OkResult = false,
            ErrorType = exception.ToErrorType()
        };
        await client.SendAsync(new NetworkFrame(MessageFrameBuilder.ResponseFrame(dto, sendedCommandType)));
    }
}
