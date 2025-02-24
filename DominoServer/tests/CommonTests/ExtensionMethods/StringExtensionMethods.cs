using DominoCommon.com.domino.common.Scripts.Networking;

namespace CommonTests.ExtensionMethods;

public static class StringExtensionMethods
{
    public static ErrorTypes ToErrorType(this string error)
    {
        return Enum.Parse<ErrorTypes>(error);
    }
}
