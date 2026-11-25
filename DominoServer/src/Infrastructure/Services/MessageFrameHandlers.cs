using Application.Interfaces;

using Infrastructure.Services.FrameHandlers;

namespace Infrastructure.Services;

public class MessageFrameHandlers(IMessageFrameHandler handler, LoggingFrameHandlers loggingFrameHandlers, LobbyFrameHandlers lobbyFrameHandlers, 
    ChatFrameHandlers chatFrameHandlers, ProfileFrameHandlers profileFrameHandlers, GameFrameHandlers gameFrameHandlers, FriendFrameHandlers friendFrameHandlers,
    TeamFrameHandlers teamFrameHandlers, DirectChallengeFrameHandlers directChallengeFrameHandlers)
{
    public void RegisterHandlers()
    {
        loggingFrameHandlers.RegisterHandlers(handler);
        lobbyFrameHandlers.RegisterHandlers(handler);
        chatFrameHandlers.RegisterHandlers(handler);
        profileFrameHandlers.RegisterHandlers(handler);
        gameFrameHandlers.RegisterHandlers(handler);
        friendFrameHandlers.RegisterHandlers(handler);
        teamFrameHandlers.RegisterHandlers(handler);
        directChallengeFrameHandlers.RegisterHandlers(handler);
    }
}