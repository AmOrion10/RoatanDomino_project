using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class RegistrationDto
    {
        [JsonProperty(PropertyName = "l")]
        public string Login { get; set; }

        [JsonProperty(PropertyName = "p")]
        public string Password { get; set; }

        [JsonProperty(PropertyName = "e")]
        public string Email { get; set; }

        [JsonProperty(PropertyName = "b")]
        public string Birthdate { get; set; }
    }
}
