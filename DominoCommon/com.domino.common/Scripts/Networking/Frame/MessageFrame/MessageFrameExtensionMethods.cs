using System;
using System.Linq;

namespace DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame
{
    public static class MessageFrameExtensionMethods
    {
        /// <summary>
        /// Get the portion of the frame which concerns <param name="field">the field</param>
        /// 0th  - 3th  bytes (4 bytes size) => Command Id
        /// 4th  - 7th  bytes (4 bytes size) => Sub Command Id
        /// 8th - 11th bytes (4 bytes size) => Frame size
        /// 12th - xxth bytes (XX bytes size) => Message
        /// </summary>
        public static byte[] GetMessageFrameByte(this byte[] frameData, MessageFrameFields field, int? frameSize = null)
        {
            switch (field)
            {
                case MessageFrameFields.CommandId:
                    return frameData.Skip(0).Take(sizeof(int)).ToArray();
                case MessageFrameFields.SubCommandId:
                    return frameData.Skip(4).Take(sizeof(int)).ToArray();
                case MessageFrameFields.FrameSize:
                    return frameData.Skip(8).Take(sizeof(int)).ToArray();
                case MessageFrameFields.Message:
                    return frameSize is null ? frameData.Skip(12).ToArray() : frameData.Skip(12).Take(frameSize.Value - 12).ToArray();
                default:
                    throw new ArgumentException("Unknown frame field");
            }
        }
    }
}
