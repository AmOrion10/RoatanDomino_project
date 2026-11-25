using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class JoinSpectatorDto
    {
        [JsonProperty(PropertyName = "g")]
        public int GameId { get; set; }
    }

    public class LeaveSpectatorDto
    {
        [JsonProperty(PropertyName = "g")]
        public int GameId { get; set; }
    }

    public class SpectatorCountUpdateDto
    {
        [JsonProperty(PropertyName = "g")]
        public int GameId { get; set; }

        [JsonProperty(PropertyName = "sc")]
        public int SpectatorCount { get; set; }
    }

    /// <summary>
    /// Public-only live match snapshot delivered to spectators.
    /// Intentionally excludes all player hand tile faces and possible moves to prevent ghosting.
    /// </summary>
    public class SpectatorMatchSnapshotDto
    {
        [JsonProperty(PropertyName = "g")]
        public int GameId { get; set; }

        [JsonProperty(PropertyName = "ttl")]
        public string MatchTitle { get; set; }

        [JsonProperty(PropertyName = "mf")]
        public MatchFormat MatchFormat { get; set; }

        [JsonProperty(PropertyName = "ts")]
        public TableStyle TableStyle { get; set; }

        [JsonProperty(PropertyName = "hn")]
        public int HandNumber { get; set; }

        [JsonProperty(PropertyName = "turn")]
        public long CurrentTurnPlayerId { get; set; }

        [JsonProperty(PropertyName = "tt", NullValueHandling = NullValueHandling.Ignore)]
        public TimerDto TurnTimer { get; set; }

        [JsonProperty(PropertyName = "bm")]
        public List<MoveDto> BoardMoves { get; set; } = new List<MoveDto>();

        [JsonProperty(PropertyName = "nd")]
        public Dictionary<long, int> NumberOfDominoPerPlayer { get; set; } = new Dictionary<long, int>();

        [JsonProperty(PropertyName = "tsm")]
        public Dictionary<int, int> TeamScores { get; set; } = new Dictionary<int, int>();

        [JsonProperty(PropertyName = "ps")]
        public Dictionary<long, int> PlayerScores { get; set; } = new Dictionary<long, int>();

        [JsonProperty(PropertyName = "pl")]
        public List<PlayerDto> Players { get; set; } = new List<PlayerDto>();

        [JsonProperty(PropertyName = "sc")]
        public int SpectatorCount { get; set; }

        [JsonProperty(PropertyName = "tc", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsTeamChallenge { get; set; }

        [JsonProperty(PropertyName = "tan", NullValueHandling = NullValueHandling.Ignore)]
        public string TeamAName { get; set; }

        [JsonProperty(PropertyName = "tbn", NullValueHandling = NullValueHandling.Ignore)]
        public string TeamBName { get; set; }
    }
}
