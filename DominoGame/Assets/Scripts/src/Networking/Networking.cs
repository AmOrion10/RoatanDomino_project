using System;

using UnityEngine;

public class Networking : MonoBehaviour
{
    public event Action ConnectionEvent;
    public static Networking Instance { get; private set; }
    public bool IsConnected { get; private set; }
    public bool IsOfflineMode { get; private set; }
    public OfflineBotClient ActiveOfflineClient { get; private set; }

    [SerializeField] private int port = 10000;
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private bool mock = false;
    [SerializeField] private string targetHost = "domino6b";
    [SerializeField] private bool useSsl = false;

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
        onlineClient = mock ? new ClientMock() : new Client(port, host, useSsl, targetHost);
        client = onlineClient;
        try
        {
            client.Start();
            IsConnected = true;
        }
        catch (Exception ex)
        {
            Debug.Log($"Error while starting client : {ex}");
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

    public void ConnectOnline(string newHost, int newPort, bool newUseSsl = false)
    {
        StopOfflineMode();

        if (onlineClient != null && IsConnected)
        {
            try { onlineClient.Stop(); } catch { }
        }

        host = string.IsNullOrWhiteSpace(newHost) ? "127.0.0.1" : newHost.Trim();
        port = newPort > 0 ? newPort : 10000;
        useSsl = newUseSsl;

        onlineClient = mock ? new ClientMock() : new Client(port, host, useSsl, targetHost);
        client = onlineClient;

        try
        {
            client.Start();
            IsConnected = true;
        }
        catch (Exception ex)
        {
            Debug.Log($"Error while connecting to {host}:{port} : {ex.Message}");
            IsConnected = false;
        }

        if (ChatManager.Instance != null)
        {
            ChatManager.Instance.RefreshClient();
        }

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

    private void OnDisable()
    {
        if (!isActive || !IsConnected)
        {
            return;
        }
        client.Stop();
    }

    public void RetryConnection()
    {
        try
        {
            client.Start();
            IsConnected = true;
        }
        catch (Exception ex)
        {
            Debug.Log($"Error while starting client : {ex}");
            IsConnected = false;
        }
        MainThread.Instance.AddActionToQueue(() => ConnectionEvent?.Invoke());
    }

    public IClient GetClient()
    {
        return client;
    }
}
