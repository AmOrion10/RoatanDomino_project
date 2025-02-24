using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class UpdateProfileDto
    {
        [JsonProperty(PropertyName = "l")]
        public string Login { get; set; }

        [JsonProperty(PropertyName = "b")]
        public string Birthdate { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string Nationality { get; set; }
    }
}
