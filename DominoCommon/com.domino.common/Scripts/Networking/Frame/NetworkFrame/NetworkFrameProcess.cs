using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame
{
    using MessageFrame;

    public static class NetworkFrameProcess
    {
        public static byte[] Encode(NetworkFrame networkFrame)
        {
            byte[] bytesNumberOfFrame = BitConverter.GetBytes(networkFrame.NumberOfFrames);
            byte[] bytesUserId = BitConverter.GetBytes(networkFrame.UserId);
            byte[] bytesTokenClient = networkFrame.TokenClient.ToByteArray();
            byte[] bytesNetworkFrameSize = BitConverter.GetBytes(networkFrame.NetworkFrameSize);

            List<byte[]> messageFramesBytes = new List<byte[]>();
            foreach (MessageFrame messageFrame in networkFrame.Frames)
            {
                messageFramesBytes.Add(MessageFrameProcess.Encode(messageFrame));
            }

            int sizeOfMessageFrames = messageFramesBytes.Sum(segment => segment.Length);
            int currentOffset = 0;
            byte[] bytesMessageFrames = new byte[sizeOfMessageFrames];
            foreach (byte[] messageFrame in messageFramesBytes)
            {
                Buffer.BlockCopy(messageFrame, 0, bytesMessageFrames, currentOffset, messageFrame.Length);
                currentOffset += messageFrame.Length;
            }

            return bytesNumberOfFrame
                .Concat(bytesUserId)
                .Concat(bytesTokenClient)
                .Concat(bytesNetworkFrameSize)
                .Concat(bytesMessageFrames)
                .Concat(bytesMessageFrames)
                .ToArray();
        }

        public static NetworkFrame Decode(byte[] data)
        {
            byte[] bytesNumberOfFrames = data.GetNetworkFrameByte(NetworkFrameFields.NumberOfFrames);
            short numberOfFrames = BitConverter.ToInt16(bytesNumberOfFrames, 0);
            
            byte[] bytesUserId = data.GetNetworkFrameByte(NetworkFrameFields.UserId);
            long userId = BitConverter.ToInt64(bytesUserId, 0);
            
            byte[] bytesTokenClient = data.GetNetworkFrameByte(NetworkFrameFields.TokenClient);
            Guid tokenClient = new Guid(bytesTokenClient);

            byte[] bytesNetworkFrameSize = data.GetNetworkFrameByte(NetworkFrameFields.NetworkFrameSize);
            int networkFrameSize = BitConverter.ToInt32(bytesNetworkFrameSize, 0);

            List<MessageFrame> messageFrames = new List<MessageFrame>();
            int currentOffset = NetworkFrame.IndexFirstFrame;
            for (short i = 0; i < numberOfFrames; i++)
            {
                MessageFrame frame = MessageFrameProcess.Decode(data.Skip(currentOffset).ToArray());
                currentOffset += frame.FrameSize;
                messageFrames.Add(frame);
            }

            return new NetworkFrame(numberOfFrames, userId, tokenClient, networkFrameSize, messageFrames);
        }
    }
}
