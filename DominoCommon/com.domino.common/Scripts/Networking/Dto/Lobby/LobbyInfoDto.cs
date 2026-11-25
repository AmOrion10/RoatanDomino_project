using System;
using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby
{
    public class LobbyInfoDto
    {
        [JsonProperty(PropertyName = "i")]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "c")]
        public string Creator { get; set; } // Can be null

        [JsonProperty(PropertyName = "m")]
        public int MaxNumberOfPlayer { get; set; }

        [JsonProperty(PropertyName = "np")]
        public int NumberOfPlayer { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "p")]
        public bool IsPublic { get; set; }

        [JsonProperty(PropertyName = "ts", NullValueHandling = NullValueHandling.Ignore)]
        public TableStyle? TableStyle { get; set; }

        [JsonProperty(PropertyName = "mf", NullValueHandling = NullValueHandling.Ignore)]
        public MatchFormat? MatchFormat { get; set; }

        [JsonProperty(PropertyName = "oop", NullValueHandling = NullValueHandling.Ignore)]
        public bool? OneOnePlayoff { get; set; }

        [JsonProperty(PropertyName = "sp", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> SeatedPlayers { get; set; } = new List<string>();

        [JsonProperty(PropertyName = "ht", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsHouseTable { get; set; }

        [JsonProperty(PropertyName = "tc", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsTeamChallenge { get; set; }

        [JsonProperty(PropertyName = "tai", NullValueHandling = NullValueHandling.Ignore)]
        public long? TeamAId { get; set; }

        [JsonProperty(PropertyName = "tan", NullValueHandling = NullValueHandling.Ignore)]
        public string TeamAName { get; set; }

        [JsonProperty(PropertyName = "tbi", NullValueHandling = NullValueHandling.Ignore)]
        public long? TeamBId { get; set; }

        [JsonProperty(PropertyName = "tbn", NullValueHandling = NullValueHandling.Ignore)]
        public string TeamBName { get; set; }

        [JsonProperty(PropertyName = "inpg", NullValueHandling = NullValueHandling.Ignore)]
        public bool IsInProgress { get; set; }

        [JsonProperty(PropertyName = "sc", NullValueHandling = NullValueHandling.Ignore)]
        public int SpectatorCount { get; set; }

        [JsonProperty(PropertyName = "rfh", NullValueHandling = NullValueHandling.Ignore)]
        public bool RequireFullHumanSquad { get; set; }

        [JsonProperty(PropertyName = "pdl", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? PlayDeadlineUtc { get; set; }
    }
}
