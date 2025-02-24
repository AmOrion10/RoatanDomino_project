using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto
{
    public class ResponseObjectDto<T> : ResponseDto
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "o")]
        public T Object { get; set; }
    }
}
