using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

namespace UnitTests;

public class MessageFrameTests
{
    [Fact]
    public void ValidateConnexionEncodeDecode()
    {
        MessageFrame connexionFrame = new(FrameCommandType.Connection, "identifiant et mot de passe");

        byte[] frameData = MessageFrameProcess.Encode(connexionFrame);
        MessageFrame decodedFrame = MessageFrameProcess.Decode(frameData);

        Check.That(decodedFrame.CommandType).IsEqualTo(connexionFrame.CommandType);
        Check.That(decodedFrame.Message).IsEqualTo(connexionFrame.Message);
        Check.That(decodedFrame.FrameSize).IsEqualTo(connexionFrame.FrameSize);
        Check.That(decodedFrame.FrameSize).IsGreaterOrEqualThan(MessageFrame.MinimalMessageFrameSize);
    }

    [Fact]
    public void ValidateDefaultEncodeDecode()
    {
        MessageFrame originalFrame = new(FrameCommandType.None, FrameCommandType.None, "Hello Guillaume!");

        byte[] frameData = MessageFrameProcess.Encode(originalFrame);
        MessageFrame decodedFrame = MessageFrameProcess.Decode(frameData);

        Check.That(originalFrame.CommandType).IsEqualTo(decodedFrame.CommandType);
        Check.That(originalFrame.FrameSize).IsEqualTo(decodedFrame.FrameSize);
        Check.That(originalFrame.Message).IsEqualTo(decodedFrame.Message);
        Check.That(decodedFrame.FrameSize).IsGreaterOrEqualThan(MessageFrame.MinimalMessageFrameSize);
    }

    [Fact]
    public void InvalidEncodeDecode()
    {
        byte[] invalidFrameData = new byte[MessageFrame.MinimalMessageFrameSize - 1];

        Check.ThatCode(() => MessageFrameProcess.Decode(invalidFrameData))
            .Throws<FrameFormatException>()
            .WithMessage($"Size of the frame must be higher than {MessageFrame.MinimalMessageFrameSize} bytes");

        byte[] validFrameData = new byte[MessageFrame.MinimalMessageFrameSize];
        Check.ThatCode(() => MessageFrameProcess.Decode(validFrameData))
            .DoesNotThrow();
    }
}
