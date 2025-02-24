using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame
{
    using MessageFrame;

    /// <summary>
    /// A network frame is structure like
    /// NumberOfFrame (short) + UserId (long) + TokenClient (Guid) + NetworkFrameSize (int) + Message frames
    /// </summary>
    public class NetworkFrame
    {
        public const int BufferSize = 4096;
        public const int IndexFirstFrame = sizeof(short) + sizeof(long) + 16 + sizeof(int);

        public short NumberOfFrames { get; }
        public long UserId { get; }
        public Guid TokenClient { get; }
        public int NetworkFrameSize { get; }
        public List<MessageFrame> Frames { get; }

        public NetworkFrame(MessageFrame frame)
        {
            NumberOfFrames = 1;
            Frames = new List<MessageFrame>
            {
                frame
            };
            NetworkFrameSize = CalculateNetworkFrameSize();
        }

        public NetworkFrame(List<MessageFrame> frames)
        {
            NumberOfFrames = (short)frames.Count;
            Frames = frames;
            NetworkFrameSize = CalculateNetworkFrameSize();
        }

        public NetworkFrame(short numberOfFrames, long userId, Guid tokenClient, int networkFrameSize, List<MessageFrame> frames)
        {
            NumberOfFrames = numberOfFrames;
            UserId = userId;
            TokenClient = tokenClient;
            NetworkFrameSize = networkFrameSize;
            Frames = frames;
        }

        public NetworkFrame(MessageFrame frame, long userId, Guid tokenClient)
        {
            NumberOfFrames = 1;
            Frames = new List<MessageFrame>
            {
                frame
            };
            UserId = userId;
            TokenClient = tokenClient;
        }

        public NetworkFrame(List<MessageFrame> frames, long userId, Guid tokenClient)
        {
            NumberOfFrames = (short)frames.Count;
            Frames = frames;
            UserId = userId;
            TokenClient = tokenClient;
        }

        private int CalculateNetworkFrameSize()
        {
            return sizeof(short) + sizeof(long) + 16 + sizeof(int) + Frames.Sum(mf => mf.FrameSize);
        }
    }
}
