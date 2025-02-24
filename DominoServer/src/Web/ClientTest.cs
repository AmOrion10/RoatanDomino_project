using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Lobby;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace Web;

public class ClientTest
{
    private readonly TcpClient tcpClient = new();
    private long userId;
    public Guid ClientToken { get; set; }
    public string ClientLogin => "ClientTest";
    public string ClientPassword { get; } = "PasswordTest";
    private readonly Mutex mutex = new();

    private NetworkStream? stream;
    private SslStream? sslStream;
    private bool useSsl;

    private static bool running = true;
    private static int idChoice;

    private readonly List<Choice> choices =
    [
        new Choice(idChoice++, "Exit", _ => running = false),
        new Choice(idChoice++, "Registration", ChoiceLogic.Registration),
        new Choice(idChoice++, "Connection", ChoiceLogic.Connexion),
        new Choice(idChoice++, "Disconnection", ChoiceLogic.Deconnexion),
        new Choice(idChoice++, "Create Lobby", ChoiceLogic.CreateLobby),
        new Choice(idChoice++, "Get all lobbies", ChoiceLogic.GetAllLobbies),
        new Choice(idChoice++, "Join Lobby", ChoiceLogic.JoinLobby),
        new Choice(idChoice++, "Leave Lobby", ChoiceLogic.LeaveLobby),
        new Choice(idChoice++, "Ready Lobby", ChoiceLogic.ReadyLobby),
    ];

    public async Task ConnectAsync(string host, int port, bool usingSsl)
    {
        useSsl = usingSsl;
        IPEndPoint endpoint = new(IPAddress.Parse(host), port);
        Console.WriteLine($"ClientTest trying to connect to {host}:{port}");
        await tcpClient.ConnectAsync(endpoint);
        if (useSsl)
        {
            sslStream = new SslStream(tcpClient.GetStream(), false, ValidateServerCertificate, null);
            await sslStream.AuthenticateAsClientAsync("domino6b");
        }
        else
        {
            stream = tcpClient.GetStream();
        }

        Thread thread = new(ThreadListenMessage);
        thread.Start();

        while (running)
        {
            int choiceId = GetChoiceOfUser();
            Choice? choice = choices.FirstOrDefault(c => c.Id == choiceId);
            if (choice is null)
            {
                Console.WriteLine($"ERROR : There is no choice for id {choiceId}");
                continue;
            }
            choice.Handler(this);
        }
        thread.Interrupt();
    }

    private void ThreadListenMessage()
    {
        while (true)
        {
            Thread.Sleep(250);
            NetworkFrame networkFrame = Receive();
            mutex.WaitOne();
            foreach (MessageFrame frame in networkFrame.Frames)
            {
                Console.WriteLine($"> {frame}");
                ResponseDto response = frame.GetObjectFromMessage<ResponseDto>();
                Console.WriteLine(response.OkResult ? $"> Ok : {response.OkResult}" : $"> Ok : {response.OkResult} {response.ErrorType}");
                Console.WriteLine($"Message : {frame.Message}");
                Console.WriteLine("");
            }
            mutex.ReleaseMutex();
        }
    }

    public void Send(NetworkFrame frame)
    {
        byte[] buffer = NetworkFrameProcess.Encode(frame);
        if (useSsl)
        {
            sslStream!.Write(buffer, 0, buffer.Length);
        }
        else
        {
            stream!.Write(buffer, 0, buffer.Length);
        }
    }

    public NetworkFrame Receive()
    {
        var buffer = new byte[NetworkFrame.BufferSize];
        if (useSsl)
        {
            _ = sslStream!.Read(buffer, 0, NetworkFrame.BufferSize);
        }
        else
        {
            _ = stream!.Read(buffer, 0, NetworkFrame.BufferSize);
        }
        return NetworkFrameProcess.Decode(buffer);
    }

    private int GetChoiceOfUser()
    {
        mutex.WaitOne();
        Console.WriteLine("");
        Console.WriteLine("####################");
        foreach (Choice choice in choices)
        {
            Console.WriteLine($"{choice.Id} : {choice.Name}");
        }
        Console.WriteLine("####################");
        mutex.ReleaseMutex();
        int choiceId = int.Parse(Console.ReadLine()!);
        return choiceId;
    }

    private static bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        // Here we check for "RemoteCertificateChainErrors" because we have a self-signed certificate so it could be the error
        // But we only want this error at most, and no others
        return sslPolicyErrors is SslPolicyErrors.None or SslPolicyErrors.RemoteCertificateChainErrors;
    }
}

public class Choice(int id, string name, Action<ClientTest> handler)
{   
    public string Name { get; } = name;
    public int Id { get; } = id;
    public Action<ClientTest> Handler { get; } = handler;
}

public static class ChoiceLogic
{
    public static void Registration(ClientTest client)
    {
        RegistrationDto dto = new()
        {
            Email = $"{client.ClientLogin}@test.com",
            Login = client.ClientLogin,
            Password = client.ClientPassword,
            Birthdate = "21/04/2002"
        };
        client.Send(new NetworkFrame(MessageFrameBuilder.RegistrationFrame(dto)));
    }

    public static void Connexion(ClientTest client)
    {
        LoggingDto dto = new()
        {
            Login = client.ClientLogin,
            Password = client.ClientPassword
        };
        client.Send(new NetworkFrame(MessageFrameBuilder.ConnectionFrame(dto)));
    }

    public static void Deconnexion(ClientTest client)
    {
        client.Send(new NetworkFrame(MessageFrameBuilder.DisconnectionFrame()));
    }

    public static void CreateLobby(ClientTest client)
    {
        CreateLobbyDto dto = new()
        {
            MaxNumberOfPlayer = 4,
            Name = "MonLobby",
            Public = true
        };
        client.Send(new NetworkFrame(MessageFrameBuilder.CreateLobbyFrame(dto)));
    }

    public static void GetAllLobbies(ClientTest client)
    {
        client.Send(new NetworkFrame(MessageFrameBuilder.GetAllLobbyFrame()));
    }

    public static void JoinLobby(ClientTest client)
    {
        Console.WriteLine("Lobby id ?");
        int lobby = int.Parse(Console.ReadLine()!);
        JoinLobbyDto dto = new()
        {
            LobbyIdToJoin = lobby
        };
        client.Send(new NetworkFrame(MessageFrameBuilder.JoinLobbyFrame(dto)));
    }

    public static void LeaveLobby(ClientTest client)
    {
        client.Send(new NetworkFrame(MessageFrameBuilder.LeaveLobbyFrame()));
    }

    public static void ReadyLobby(ClientTest client)
    {
        client.Send(new NetworkFrame(MessageFrameBuilder.ReadyLobbyFrame()));
    }
}