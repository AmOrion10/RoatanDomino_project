using System;

using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class PlayerPresenceDto
    {
        [JsonProperty(PropertyName = "i")]
        public long UserId { get; set; }

        [JsonProperty(PropertyName = "u")]
        public string Username { get; set; }

        [JsonProperty(PropertyName = "e")]
        public int Elo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "w")]
        public int Wins { get; set; }

        [JsonProperty(PropertyName = "lo")]
        public int Losses { get; set; }

        [JsonProperty(PropertyName = "wa")]
        public int WinAssists { get; set; }

        [JsonProperty(PropertyName = "tm")]
        public int TotalMoves { get; set; }

        [JsonProperty(PropertyName = "om")]
        public int OptimalMoves { get; set; }

        [JsonProperty(PropertyName = "ats")]
        public double AverageTurnSeconds { get; set; }

        [JsonProperty(PropertyName = "m7")]
        public int OnlineMatchesLast7Days { get; set; }

        [JsonProperty(PropertyName = "lom", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastOnlineMatchUtc { get; set; }

        [JsonProperty(PropertyName = "on")]
        public bool IsOnline { get; set; }

        [JsonProperty(PropertyName = "st")]
        public string Status { get; set; } // "Available", "At Table", "In Match", "Offline"

        [JsonProperty(PropertyName = "ti", NullValueHandling = NullValueHandling.Ignore)]
        public int? TableId { get; set; }

        [JsonProperty(PropertyName = "tn", NullValueHandling = NullValueHandling.Ignore)]
        public string TableName { get; set; }

        [JsonProperty(PropertyName = "au", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarUrl { get; set; }

        [JsonProperty(PropertyName = "ab", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarBase64 { get; set; }

        [JsonProperty(PropertyName = "tmi", NullValueHandling = NullValueHandling.Ignore)]
        public long? TeamId { get; set; }

        [JsonProperty(PropertyName = "tmt", NullValueHandling = NullValueHandling.Ignore)]
        public string TeamTag { get; set; }

        [JsonIgnore]
        public double AccuracyPercent => RoatanRankTierHelper.CalculateAccuracyPercent(OptimalMoves, TotalMoves);
    }
}
