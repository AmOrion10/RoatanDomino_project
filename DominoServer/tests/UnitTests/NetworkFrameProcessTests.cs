using DominoCommon.com.domino.common.Scripts.ExtensionMethods;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace UnitTests;

public class NetworkFrameProcessTests
{
    [Fact]
    public void MustDecodeEncodeMultipleFrame()
    {
        MessageFrame frame1 = new(FrameCommandType.Connection, new LoggingDto
        {
            Login = "MonLogin",
            Password = "MonPassword"
        }.ToJson());
        MessageFrame frame2 = new(FrameCommandType.Response, FrameCommandType.GetAllLobbies, new LobbiesInfoDto
        {
            Lobbies =
            [
                new LobbyInfoDto
                {
                    Creator = "Me",
                    Id = 1
                },
                new LobbyInfoDto
                {
                    Creator = "You",
                    Id = 2
                }
            ]
        }.ToJson());
        MessageFrame frame3 = new(FrameCommandType.Response, FrameCommandType.JoinLobby, ResponseDto.Ok().ToJson());

        byte[] data = NetworkFrameProcess.Encode(new NetworkFrame([frame1, frame2, frame3]));
        NetworkFrame networkFrame = NetworkFrameProcess.Decode(data);

        Check.That(networkFrame.NetworkFrameSize).IsEqualTo(frame1.FrameSize + frame2.FrameSize + frame3.FrameSize + sizeof(long) + 16 + sizeof(short) + sizeof(int));
        Check.That(networkFrame.NumberOfFrames).IsEqualTo(3);
        Check.That(networkFrame.Frames).CountIs(3);

        MessageFrame frame1Decoded = networkFrame.Frames[0];
        Check.That(frame1Decoded.CommandType).IsEqualTo(FrameCommandType.Connection);
        LoggingDto decodedLoggingDto = frame1Decoded.GetObjectFromMessage<LoggingDto>();
        Check.That(decodedLoggingDto.Login).IsEqualTo("MonLogin");

        MessageFrame frame2Decoded = networkFrame.Frames[1];
        Check.That(frame2Decoded.CommandType).IsEqualTo(FrameCommandType.Response);
        Check.That(frame2Decoded.SubCommandType).IsEqualTo(FrameCommandType.GetAllLobbies);
        LobbiesInfoDto decodedLobbiesInfoDto = frame2Decoded.GetObjectFromMessage<LobbiesInfoDto>();
        Check.That(decodedLobbiesInfoDto.Lobbies).CountIs(2);

        MessageFrame frame3Decoded = networkFrame.Frames[2];
        Check.That(frame3Decoded.CommandType).IsEqualTo(FrameCommandType.Response);
    }

    [Fact]
    public void MustDecodeEncodeSingleFrame()
    {
        MessageFrame frame = new MessageFrame(FrameCommandType.Registration, new RegistrationDto
        {
            Email = "test@test.com"
        }.ToJson());

        byte[] data = NetworkFrameProcess.Encode(new NetworkFrame(frame));
        NetworkFrame networkFrame = NetworkFrameProcess.Decode(data);
        Check.That(networkFrame.NumberOfFrames).IsEqualTo(1);
        Check.That(networkFrame.Frames).CountIs(1);

        MessageFrame frameDecoded = networkFrame.Frames[0];
        Check.That(frameDecoded.CommandType).IsEqualTo(FrameCommandType.Registration);
    }
}
