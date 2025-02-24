using System;

using UnityEngine;

public class Networking : MonoBehaviour
{
    public event Action ConnectionEvent;
    public static Networking Instance { get; private set; }
    public bool IsConnected { get; private set; }

    [SerializeField] private int port = 10000;
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private bool mock = false;
    [SerializeField] private string targetHost = "domino6b";
    [SerializeField] private bool useSsl = false;

    private IClient client;
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
        client = mock ? new ClientMock() : new Client(port, host, useSsl, targetHost);
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
