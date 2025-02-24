using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.ExtensionMethods
{
    public static class ObjectExtensionMethods
    {
        public static string ToJson(this object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
    }
}
