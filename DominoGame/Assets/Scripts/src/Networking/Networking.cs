using System;

using UnityEngine;

public class Networking : MonoBehaviour
{
    public event Action ConnectionEvent;
    public static Networking Instance { get; private set; }
    public bool IsConnected { get; private set; }
    public bool IsOfflineMode { get; private set; }
    public OfflineBotClient ActiveOfflineClient { get; private set; }

    [SerializeField] private int port = 5000;
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private bool mock = false;
    [SerializeField] private string targetHost = "domino6b";
    [SerializeField] private bool useSsl = false;

    private string currentServerUrl = string.Empty;
    private IClient client;
    private IClient onlineClient;
    private bool isActive = true;

    private void Awake()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("Networking");
        if (objs.Length > 1)
        {
            isActive = false;
            Destroy(gameObject);
            return;
        }

        currentServerUrl = RoatanOnlineController.GetSavedServerUrl();

        if (mock)
        {
            onlineClient = new ClientMock();
            client = onlineClient;
            try
            {
                client.Start();
                IsConnected = true;
            }
            catch (Exception ex)
            {
                Debug.Log($"Error while starting mock client : {ex}");
                IsConnected = false;
            }
        }
        else
        {
            // Keep onlineClient non-null before first connect without blocking mobile app launch on a remote socket.
            onlineClient = new Client(port, host, useSsl, targetHost);
            client = onlineClient;
            IsConnected = false;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public OfflineBotClient StartOfflineMode(BotDifficulty difficulty, int botCount = 3)
    {
        if (ActiveOfflineClient != null)
        {
            ActiveOfflineClient.Stop();
        }

        ActiveOfflineClient = new OfflineBotClient(difficulty, botCount);
        ActiveOfflineClient.Start();
        client = ActiveOfflineClient;
        IsOfflineMode = true;
        IsConnected = true;

        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.RefreshClient();
        }

        return ActiveOfflineClient;
    }

    public void StopOfflineMode()
    {
        if (ActiveOfflineClient != null)
        {
            ActiveOfflineClient.Stop();
            ActiveOfflineClient = null;
        }

        IsOfflineMode = false;
        client = onlineClient;

        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.RefreshClient();
        }
    }

    /// <summary>
    /// Connects to a DominoServer endpoint using https://, wss://, http://, ws://,
    /// bare domain (e.g. random-words.trycloudflare.com), or legacy tcp://host:port.
    /// </summary>
    public void ConnectOnlineUrl(string rawServerUrl)
    {
        StopOfflineMode();

        if (onlineClient != null && IsConnected)
        {
            try { onlineClient.Stop(); } catch { }
        }

        if (!RoatanOnlineController.TryResolveServerEndpoint(
            rawServerUrl,
            out Uri wsUri,
            out string normalizedDisplayUrl,
            out bool isLegacyTcp,
            out string tcpHost,
            out int tcpPort,
            out bool tcpUseSsl,
            out string errorMessage))
        {
            Debug.LogWarning($"Invalid server URL '{rawServerUrl}': {errorMessage}");
            IsConnected = false;
            NotifyConnectionChanged();
            return;
        }

        currentServerUrl = normalizedDisplayUrl;

        if (mock)
        {
            onlineClient = new ClientMock();
        }
        else if (isLegacyTcp)
        {
            host = tcpHost;
            port = tcpPort;
            useSsl = tcpUseSsl;
            onlineClient = new Client(port, host, useSsl, targetHost);
        }
        else
        {
            host = wsUri.Host;
            port = wsUri.Port;
            useSsl = string.Equals(wsUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase);
            onlineClient = new Client(wsUri);
        }

        client = onlineClient;

        try
        {
            client.Start();
            IsConnected = true;
        }
        catch (Exception ex)
        {
            Debug.Log($"Error while connecting to {normalizedDisplayUrl} : {ex.Message}");
            IsConnected = false;
        }

        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.RefreshClient();
        }

        NotifyConnectionChanged();
    }

    public void ConnectOnline(string newHost, int newPort, bool newUseSsl = false)
    {
        string trimmed = string.IsNullOrWhiteSpace(newHost) ? "127.0.0.1" : newHost.Trim();

        // Automatically handle full URLs (https://, wss://, http://, ws://, tcp://) or domain names cleanly
        if (trimmed.Contains("://") || (!System.Net.IPAddress.TryParse(trimmed, out _) && !string.Equals(trimmed, "localhost", StringComparison.OrdinalIgnoreCase)))
        {
            ConnectOnlineUrl(trimmed);
            return;
        }

        // Default HTTP/WebSocket container port 5000 uses WebSocket transport
        if (newPort <= 0 || newPort == 5000)
        {
            string scheme = newUseSsl ? "wss" : "ws";
            ConnectOnlineUrl($"{scheme}://{trimmed}:{(newPort > 0 ? newPort : 5000)}");
            return;
        }

        // Legacy raw TCP port (e.g. 10000)
        string legacyScheme = newUseSsl ? "tcps" : "tcp";
        ConnectOnlineUrl($"{legacyScheme}://{trimmed}:{newPort}");
    }

    private void NotifyConnectionChanged()
    {
        if (MainThread.Instance != null)
        {
            MainThread.Instance.AddActionToQueue(() => ConnectionEvent?.Invoke());
        }
        else
        {
            ConnectionEvent?.Invoke();
        }
    }

    public string CurrentHost => host;
    public int CurrentPort => port;
    public string CurrentServerUrl => string.IsNullOrWhiteSpace(currentServerUrl) ? $"{host}:{port}" : currentServerUrl;

    private void OnDisable()
    {
        if (!isActive || !IsConnected || client == null)
        {
            return;
        }
        client.Stop();
    }

    public void RetryConnection()
    {
        ConnectOnlineUrl(CurrentServerUrl);
    }

    public IClient GetClient()
    {
        return client;
    }
}
