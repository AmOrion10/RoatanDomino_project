using System.Collections.Generic;

using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.User
{
    public class AllHistoryGamesDto
    {
        [JsonProperty(PropertyName = "h")]
        public List<ShortHistoryGameDto> HistoryGames { get; set; }
    }
}
