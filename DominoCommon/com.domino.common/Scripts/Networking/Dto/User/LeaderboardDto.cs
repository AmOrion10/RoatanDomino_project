using System;
using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class LeaderboardEntryDto
    {
        [JsonProperty(PropertyName = "r")]
        public int Rank { get; set; }

        [JsonProperty(PropertyName = "i")]
        public long UserId { get; set; }

        [JsonProperty(PropertyName = "u")]
        public string Username { get; set; }

        [JsonProperty(PropertyName = "au", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarUrl { get; set; }

        [JsonProperty(PropertyName = "ab", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarBase64 { get; set; }

        [JsonProperty(PropertyName = "e")]
        public int Elo { get; set; } = RoatanRankTierHelper.DefaultElo;

        [JsonProperty(PropertyName = "t")]
        public RoatanRankTier Tier { get; set; } = RoatanRankTier.Sailor;

        [JsonProperty(PropertyName = "tn")]
        public string TierName { get; set; }

        [JsonProperty(PropertyName = "tc")]
        public string TierColorHex { get; set; }

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

        [JsonProperty(PropertyName = "acc")]
        public double AccuracyPercent { get; set; }

        [JsonProperty(PropertyName = "ats")]
        public double AverageTurnSeconds { get; set; }

        [JsonProperty(PropertyName = "m7")]
        public int OnlineMatchesLast7Days { get; set; }

        [JsonProperty(PropertyName = "lom", NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastOnlineMatchUtc { get; set; }
    }

    public class LeaderboardDto
    {
        [JsonProperty(PropertyName = "e")]
        public List<LeaderboardEntryDto> Entries { get; set; } = new List<LeaderboardEntryDto>();

        [JsonProperty(PropertyName = "me", NullValueHandling = NullValueHandling.Ignore)]
        public LeaderboardEntryDto LocalPlayerEntry { get; set; }

        [JsonProperty(PropertyName = "tot")]
        public int TotalRankedPlayers { get; set; }
    }
}
