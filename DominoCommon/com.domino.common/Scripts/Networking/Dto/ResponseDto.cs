using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto
{
    public class ResponseDto
    {
        [JsonProperty(PropertyName = "ok")]
        public bool OkResult { get; set; } = true;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "e")]

        public ErrorTypes? ErrorType { get; set; }

        public static ResponseDto Ok() => new ResponseDto { OkResult = true };
        public static ResponseDto Error(ErrorTypes reason) => new ResponseDto { OkResult = false, ErrorType = reason };
    }
}
