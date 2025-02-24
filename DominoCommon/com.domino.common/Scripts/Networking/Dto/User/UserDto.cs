using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class UserDto
    {
        [JsonProperty(PropertyName = "i")]
        public long Id { get; set; }

        [JsonProperty(PropertyName = "l")]
        public string Login { get; set; }

        [JsonProperty(PropertyName = "e")]
        public string Email { get; set; }

        [JsonProperty(PropertyName = "s")]
        public int Score { get; set; }

        [JsonProperty(PropertyName = "d")]
        public string DateSignup { get; set; }

        [JsonProperty(PropertyName = "b")] 
        public string BirthDate { get; set; }

        /// <summary>
        /// Can be null
        /// </summary>
        [JsonProperty(PropertyName = "n")]
        public string Nationality { get; set; } 
    }
}
