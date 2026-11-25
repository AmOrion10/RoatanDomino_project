using System.Collections.Generic;

using DominoCommon.com.domino.common.Scripts.Enums;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class PlayerEloBreakdownDto
    {
        [JsonProperty(PropertyName = "i")]
        public long PlayerId { get; set; }

        [JsonProperty(PropertyName = "u")]
        public string Username { get; set; }

        [JsonProperty(PropertyName = "pe")]
        public int PreviousElo { get; set; }

        [JsonProperty(PropertyName = "ne")]
        public int NewElo { get; set; }

        [JsonProperty(PropertyName = "d")]
        public int Delta { get; set; }

        [JsonProperty(PropertyName = "pt")]
        public RoatanRankTier PreviousTier { get; set; }

        [JsonProperty(PropertyName = "nt")]
        public RoatanRankTier NewTier { get; set; }

        [JsonProperty(PropertyName = "tn")]
        public string TierName { get; set; }

        [JsonProperty(PropertyName = "tc")]
        public string TierColorHex { get; set; }

        [JsonProperty(PropertyName = "res")]
        public string ResultLabel { get; set; }

        [JsonProperty(PropertyName = "bd")]
        public int BaseEloDelta { get; set; }

        [JsonProperty(PropertyName = "sb")]
        public int SpecialWinBonus { get; set; }

        [JsonProperty(PropertyName = "pm")]
        public int PerformanceModifier { get; set; }

        [JsonProperty(PropertyName = "wa")]
        public int WinAssists { get; set; }

        [JsonProperty(PropertyName = "acc")]
        public double AccuracyPercent { get; set; }

        [JsonProperty(PropertyName = "ats")]
        public double AverageTurnSeconds { get; set; }

        [JsonProperty(PropertyName = "tm")]
        public int TotalMoves { get; set; }

        [JsonProperty(PropertyName = "om")]
        public int OptimalMoves { get; set; }

        [JsonProperty(PropertyName = "ra")]
        public bool IsRankedApplied { get; set; }
    }

    public class TeamEloChangeDto
    {
        [JsonProperty(PropertyName = "i")]
        public long TeamId { get; set; }

        [JsonProperty(PropertyName = "n")]
        public string TeamName { get; set; }

        [JsonProperty(PropertyName = "t")]
        public string TeamTag { get; set; }

        [JsonProperty(PropertyName = "pe")]
        public int PreviousElo { get; set; }

        [JsonProperty(PropertyName = "ne")]
        public int NewElo { get; set; }

        [JsonProperty(PropertyName = "d")]
        public int Delta { get; set; }

        [JsonProperty(PropertyName = "pt")]
        public RoatanRankTier PreviousTier { get; set; }

        [JsonProperty(PropertyName = "nt")]
        public RoatanRankTier NewTier { get; set; }

        [JsonProperty(PropertyName = "tn")]
        public string TierName { get; set; }

        [JsonProperty(PropertyName = "tc")]
        public string TierColorHex { get; set; }

        [JsonProperty(PropertyName = "res")]
        public string ResultLabel { get; set; }

        [JsonProperty(PropertyName = "bd")]
        public int BaseEloDelta { get; set; }

        [JsonProperty(PropertyName = "sb")]
        public int SpecialWinBonus { get; set; }
    }

    public class TeamEloBreakdownDto
    {
        [JsonProperty(PropertyName = "ci")]
        public long ChallengeId { get; set; }

        [JsonProperty(PropertyName = "a")]
        public TeamEloChangeDto TeamA { get; set; }

        [JsonProperty(PropertyName = "b")]
        public TeamEloChangeDto TeamB { get; set; }
    }

    public class MatchEloBreakdownDto
    {
        [JsonProperty(PropertyName = "g")]
        public int GameId { get; set; }

        [JsonProperty(PropertyName = "rk")]
        public bool IsRankedMatch { get; set; }

        [JsonProperty(PropertyName = "p")]
        public List<PlayerEloBreakdownDto> Players { get; set; } = new List<PlayerEloBreakdownDto>();

        [JsonProperty(PropertyName = "tb", NullValueHandling = NullValueHandling.Ignore)]
        public TeamEloBreakdownDto TeamBreakdown { get; set; }
    }
}
