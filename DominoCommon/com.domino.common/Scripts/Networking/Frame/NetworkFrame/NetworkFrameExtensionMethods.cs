using System;
using System.Linq;

namespace DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame
{
    public static class NetworkFrameExtensionMethods
    {
        /// <summary>
        /// Get the portion of the network frame which concerns <param name="field">the field</param>
        /// 0th  - 1th  bytes (2 bytes size) => Number of frames
        /// 2th  - 9th  bytes (8 bytes size) => User id
        /// 10th - 25th bytes (16 bytes size) => Token client
        /// 26th - 29th bytes (4 bytes size) => Network frame size
        /// </summary>
        public static byte[] GetNetworkFrameByte(this byte[] frameData, NetworkFrameFields field)
        {
            switch (field)
            {
                case NetworkFrameFields.NumberOfFrames:
                    return frameData.Skip(0).Take(sizeof(short)).ToArray();
                case NetworkFrameFields.UserId:
                    return frameData.Skip(2).Take(sizeof(long)).ToArray();
                case NetworkFrameFields.TokenClient:
                    return frameData.Skip(10).Take(16).ToArray();
                case NetworkFrameFields.NetworkFrameSize:
                    return frameData.Skip(26).Take(sizeof(int)).ToArray();
                default:
                    throw new ArgumentException("Unknown frame field");
            }
        }
    }
}
