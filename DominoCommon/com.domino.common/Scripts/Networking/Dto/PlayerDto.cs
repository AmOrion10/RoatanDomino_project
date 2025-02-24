using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto
{
    public class PlayerDto
    {
        [JsonProperty(PropertyName = "i")]
        public long Id { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "e")]
        public int Elo { get; set; }
    }
}
