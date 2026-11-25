using System;

using DominoCommon.com.domino.common.Scripts.Enums;

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

        [JsonProperty(PropertyName = "el")]
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

        [JsonProperty(PropertyName = "am")]
        public int AbandonedMatches { get; set; }

        [JsonProperty(PropertyName = "d")]
        public string DateSignup { get; set; }

        [JsonProperty(PropertyName = "b")]
        public string BirthDate { get; set; }

        /// <summary>
        /// Can be null
        /// </summary>
        [JsonProperty(PropertyName = "n")]
        public string Nationality { get; set; }

        [JsonProperty(PropertyName = "au", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarUrl { get; set; }

        [JsonProperty(PropertyName = "ab", NullValueHandling = NullValueHandling.Ignore)]
        public string AvatarBase64 { get; set; }

        [JsonProperty(PropertyName = "ap", NullValueHandling = NullValueHandling.Ignore)]
        public string AuthProvider { get; set; }

        [JsonIgnore]
        public double AccuracyPercent => RoatanRankTierHelper.CalculateAccuracyPercent(OptimalMoves, TotalMoves);
    }
}
