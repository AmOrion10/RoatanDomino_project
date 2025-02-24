using System;
using System.Linq;
using System.Text;

namespace DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame
{
    public static class MessageFrameProcess
    {
        public static byte[] Encode(MessageFrame frame)
        {
            byte[] bytesCommandType = BitConverter.GetBytes((int)frame.CommandType);
            byte[] bytesSubCommantType = BitConverter.GetBytes((int)frame.SubCommandType);
            byte[] bytesFrameSize = BitConverter.GetBytes(frame.FrameSize);
            byte[] bytesMessage = Encoding.UTF8.GetBytes(frame.Message);

            return bytesCommandType
                .Concat(bytesSubCommantType)
                .Concat(bytesFrameSize)
                .Concat(bytesMessage)
                .ToArray();
        }

        public static MessageFrame Decode(byte[] frameData)
        {
            if (frameData.Length < MessageFrame.MinimalMessageFrameSize)
            {
                throw new FrameFormatException($"Size of the frame must be higher than {MessageFrame.MinimalMessageFrameSize} bytes");
            }

            byte[] idCmdBytes = frameData.GetMessageFrameByte(MessageFrameFields.CommandId);
            byte[] idSubCmdBytes = frameData.GetMessageFrameByte(MessageFrameFields.SubCommandId);
            byte[] frameSizeBytes = frameData.GetMessageFrameByte(MessageFrameFields.FrameSize);

            int frameSize = BitConverter.ToInt32(frameSizeBytes, 0);
            byte[] messageBytes = frameData.GetMessageFrameByte(MessageFrameFields.Message, frameSize);

            FrameCommandType idCmd = (FrameCommandType)BitConverter.ToInt32(idCmdBytes, 0);
            FrameCommandType subCommandType = (FrameCommandType)BitConverter.ToInt32(idSubCmdBytes, 0);
            string message = Encoding.UTF8.GetString(messageBytes);

            return new MessageFrame(idCmd, subCommandType, message, frameSize);
        }
    }
}
