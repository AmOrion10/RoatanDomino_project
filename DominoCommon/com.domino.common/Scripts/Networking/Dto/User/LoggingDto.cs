using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class LoggingDto
    {
        [JsonProperty(PropertyName = "l")]
        public string Login { get; set; }

        [JsonProperty(PropertyName = "p")]
        public string Password { get; set; }
    }
}
