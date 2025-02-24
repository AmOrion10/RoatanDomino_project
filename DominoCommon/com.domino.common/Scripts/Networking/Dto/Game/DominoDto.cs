using Newtonsoft.Json;

namespace DominoCommon.com.domino.common.Scripts.Networking.Dto.Game
{
    public class DominoDto
    {
        [JsonProperty(PropertyName = "s1")]
        public int Side1 { get; set; }

        [JsonProperty(PropertyName = "s2")]
        public int Side2 { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is DominoDto other)
            {
                return Side1 == other.Side1 && Side2 == other.Side2;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Side1.GetHashCode() + Side2.GetHashCode();
        }

        public bool IsDouble()
        {
            return Side1 == Side2;
        }
    }
}
