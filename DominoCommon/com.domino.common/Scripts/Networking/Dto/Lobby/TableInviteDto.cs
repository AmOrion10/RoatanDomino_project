using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class TableInviteDto
    {
        [JsonProperty(PropertyName = "tu")]
        public long TargetUserId { get; set; }

        [JsonProperty(PropertyName = "tn", NullValueHandling = NullValueHandling.Ignore)]
        public string TargetUsername { get; set; }

        [JsonProperty(PropertyName = "fu", NullValueHandling = NullValueHandling.Ignore)]
        public long FromUserId { get; set; }

        [JsonProperty(PropertyName = "fn", NullValueHandling = NullValueHandling.Ignore)]
        public string FromUsername { get; set; }

        [JsonProperty(PropertyName = "ti", NullValueHandling = NullValueHandling.Ignore)]
        public int TableId { get; set; }

        [JsonProperty(PropertyName = "tname", NullValueHandling = NullValueHandling.Ignore)]
        public string TableName { get; set; }

        [JsonProperty(PropertyName = "tc", NullValueHandling = NullValueHandling.Ignore)]
        public string TableCode { get; set; }
    }
}
