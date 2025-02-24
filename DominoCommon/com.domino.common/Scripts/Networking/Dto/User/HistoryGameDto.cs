using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class HistoryGameDto
    {
        [JsonProperty(PropertyName = "i")]
        public long Id { get; set; }

        [JsonProperty(PropertyName = "s")]
        public string StartTime { get; set; }

        [JsonProperty(PropertyName = "e")]
        public string EndTime { get; set; }

        [JsonProperty(PropertyName = "r")]
        public bool Ranked { get; set; }

        [JsonProperty(PropertyName = "b")]
        public int TotalBet { get; set; }

        [JsonProperty(PropertyName = "w")]
        public long? WinnerId { get; set; }

        [JsonProperty(PropertyName = "g")]
        public int GeneralTimer { get; set; }

        [JsonProperty(PropertyName = "t")]
        public int PlayerTurnTimer { get; set; }

        [JsonProperty(PropertyName = "m")]
        public List<MoveDto> Moves { get; set; }

        [JsonProperty(PropertyName = "p")]
        public List<HistoryGamePlayerDto> Players { get; set; }
    }
}
