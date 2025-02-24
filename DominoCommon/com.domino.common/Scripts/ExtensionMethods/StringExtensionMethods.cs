using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.ExtensionMethods
{
    public static class StringExtensionMethods
    {
        public static T FromJsonToObject<T>(this string message)
        {
            return JsonConvert.DeserializeObject<T>(message);
        }
    }
}
