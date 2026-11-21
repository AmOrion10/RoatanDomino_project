using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class RejoinMatchSnapshotDto
    {
        [JsonProperty(PropertyName = "mid")]
        public int MatchId { get; set; }

        [JsonProperty(PropertyName = "lid")]
        public int LobbyId { get; set; }

        [JsonProperty(PropertyName = "pid")]
        public long PlayerId { get; set; }

        [JsonProperty(PropertyName = "tok")]
        public string ReconnectToken { get; set; }

        [JsonProperty(PropertyName = "atok")]
        public string AuthToken { get; set; }

        [JsonProperty(PropertyName = "usr")]
        public UserDto User { get; set; }

        [JsonProperty(PropertyName = "ttl")]
        public string MatchTitle { get; set; }

        [JsonProperty(PropertyName = "mf")]
        public MatchFormat MatchFormat { get; set; }

        [JsonProperty(PropertyName = "ts")]
        public TableStyle TableStyle { get; set; }

        [JsonProperty(PropertyName = "oop")]
        public bool OneOnePlayoff { get; set; }

        [JsonProperty(PropertyName = "hn")]
        public int HandNumber { get; set; }

        [JsonProperty(PropertyName = "turn")]
        public long CurrentTurnPlayerId { get; set; }

        [JsonProperty(PropertyName = "tt")]
        public TimerDto TurnTimer { get; set; }

        [JsonProperty(PropertyName = "ap")]
        public bool IsAutoPassTurn { get; set; }

        [JsonProperty(PropertyName = "bm")]
        public List<MoveDto> BoardMoves { get; set; } = new List<MoveDto>();

        [JsonProperty(PropertyName = "h")]
        public List<DominoDto> Hand { get; set; } = new List<DominoDto>();

        [JsonProperty(PropertyName = "pm")]
        public PossibleMovesDto PossibleMoves { get; set; }

        [JsonProperty(PropertyName = "nd")]
        public Dictionary<long, int> NumberOfDominoPerPlayer { get; set; } = new Dictionary<long, int>();

        [JsonProperty(PropertyName = "tsm")]
        public Dictionary<int, int> TeamScores { get; set; } = new Dictionary<int, int>();

        [JsonProperty(PropertyName = "ps")]
        public Dictionary<long, int> PlayerScores { get; set; } = new Dictionary<long, int>();

        [JsonProperty(PropertyName = "pl")]
        public List<PlayerDto> Players { get; set; } = new List<PlayerDto>();
    }
}
