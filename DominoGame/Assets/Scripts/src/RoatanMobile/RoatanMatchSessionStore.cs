using System;

using Newtonsoft.Json;

using UnityEngine;

public static class RoatanMatchSessionStore
{
    private const string PrefSessionKey = "RoatanDomino_ActiveMatchSession";

    public class ActiveMatchSessionData
    {
        [JsonProperty("matchId")]
        public int MatchId { get; set; }

        [JsonProperty("lobbyId")]
        public int LobbyId { get; set; }

        [JsonProperty("playerId")]
        public long PlayerId { get; set; }

        [JsonProperty("reconnectToken")]
        public string ReconnectToken { get; set; }

        [JsonProperty("expiresAtUtc")]
        public string ExpiresAtUtc { get; set; }

        [JsonProperty("serverHost")]
        public string ServerHost { get; set; }

        [JsonProperty("serverPort")]
        public int ServerPort { get; set; }

        [JsonProperty("serverUrl")]
        public string ServerUrl { get; set; }

        public bool IsExpired()
        {
            if (string.IsNullOrWhiteSpace(ExpiresAtUtc)) return true;
            if (!DateTime.TryParse(ExpiresAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return true;
            }
            return DateTime.UtcNow >= parsed.ToUniversalTime();
        }
    }

    public static void SaveSession(
        int matchId,
        int lobbyId,
        long playerId,
        string reconnectToken,
        string serverHost,
        int serverPort,
        int ttlSeconds = 1800,
        string serverUrl = null)
    {
        if (playerId <= 0 || string.IsNullOrWhiteSpace(reconnectToken))
        {
            return;
        }

        ActiveMatchSessionData data = new ActiveMatchSessionData
        {
            MatchId = matchId,
            LobbyId = lobbyId,
            PlayerId = playerId,
            ReconnectToken = reconnectToken,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(40, ttlSeconds)).ToString("O"),
            ServerHost = string.IsNullOrWhiteSpace(serverHost) ? "127.0.0.1" : serverHost.Trim(),
            ServerPort = serverPort > 0 ? serverPort : 5000,
            ServerUrl = string.IsNullOrWhiteSpace(serverUrl) ? null : serverUrl.Trim()
        };

        PlayerPrefs.SetString(PrefSessionKey, JsonConvert.SerializeObject(data));
        PlayerPrefs.Save();
    }

    public static ActiveMatchSessionData LoadValidSession()
    {
        if (!PlayerPrefs.HasKey(PrefSessionKey))
        {
            return null;
        }

        string raw = PlayerPrefs.GetString(PrefSessionKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            ClearSession();
            return null;
        }

        try
        {
            ActiveMatchSessionData data = JsonConvert.DeserializeObject<ActiveMatchSessionData>(raw);
            if (data == null || data.PlayerId <= 0 || string.IsNullOrWhiteSpace(data.ReconnectToken) || data.IsExpired())
            {
                ClearSession();
                return null;
            }
            return data;
        }
        catch
        {
            ClearSession();
            return null;
        }
    }

    public static void ClearSession()
    {
        if (PlayerPrefs.HasKey(PrefSessionKey))
        {
            PlayerPrefs.DeleteKey(PrefSessionKey);
            PlayerPrefs.Save();
        }
    }
}
