using System.Text;

using DominoCommon.com.domino.common.Scripts.ExtensionMethods;

namespace DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame
{
    /// <summary>
    /// A Message Frame is structured like :
    /// CommandId (int) + SubCommandId (int) + Message Size (int) + Empty Message (1) 
    /// </summary>
    public class MessageFrame
    {
        public const int MinimalMessageFrameSize = sizeof(int) + sizeof(int) + sizeof(int) + 1;

        private const int NumberOfByteForFrameSize = sizeof(int);

        public FrameCommandType CommandType { get; }
        public FrameCommandType SubCommandType { get; } = FrameCommandType.None;
        public int FrameSize { get; }
        public string Message { get; }

        public MessageFrame(FrameCommandType commandType, string message)
        {
            CommandType = commandType;
            Message = message;
            FrameSize = CalculateFrameSize();
        }

        public MessageFrame(FrameCommandType commandType, FrameCommandType subCommandType, string message)
        {
            CommandType = commandType;
            SubCommandType = subCommandType;
            Message = message;
            FrameSize = CalculateFrameSize();
        }

        public MessageFrame(FrameCommandType commandType, FrameCommandType subCommandType, string message, int frameSize)
        {
            CommandType = commandType;
            SubCommandType = subCommandType;
            Message = message;
            FrameSize = frameSize;
        }

        public int CalculateFrameSize()
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(Message);
            return sizeof(int) + sizeof(int) + messageBytes.Length + NumberOfByteForFrameSize;
        }

        public T GetObjectFromMessage<T>()
        {
            return Message.FromJsonToObject<T>();
        }

        public override string ToString()
        {
            return $"MessageFrame of type {CommandType}";
        }
    }
}