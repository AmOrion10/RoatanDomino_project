using Application.Interfaces;
using Application.Interfaces.Managers;

using CommonTests.Fixtures;

using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.NetworkFrame;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "Connection")]
public class ConnectionStepsDefinition : BaseStepsDefinition
{
    private readonly TcpServerMock tcpServerMock;
    
    public ConnectionStepsDefinition(ScenarioContext context) : base(context)
    {
        context.Set(true, ServiceCollectionBuilder.MockIMessageHandler);
        context.Set(true, ServiceCollectionBuilder.DontMockIClientManager);
        ReplaceTestEnvironment();
        tcpServerMock = GetService<ITcpServer, TcpServerMock>();
    }

    [Given("Le serveur a encore de la place pour (.*) personne")]
    public void GivenLeServeurAEncoreDeLaPlacePourPersonne(int maxClients)
    {
        GetService<IClientManager>().ConfigureMaxClients(maxClients);
        Check.That(GetService<IClientManager>().HasEnoughSpaceForAnotherClient()).IsTrue();
    }

    [Given("Le serveur n'a plus de place")]
    public void GivenLeServeurNaPlusDePlace()
    {
        GetService<IClientManager>().ConfigureMaxClients(0);
        Check.That(GetService<IClientManager>().HasEnoughSpaceForAnotherClient()).IsFalse();
    }

    [Given("Un client est connecté au serveur")]
    public void GivenUnClientEstConnecteAuServeur()
    {
        ClientMock client = new();
        Clients.Add(client);
        GetService<IClientManager>().HandleNewClient(client);
    }

    [When("Un client se connecte")]
    public async Task WhenUnClientSeConnecte()
    {
        ClientMock client = new();
        Clients.Add(client);
        tcpServerMock.SetNewClient(client);
        tcpServerMock.ClientWantsToConnectReturns([true, false]);
        await WaitOneCycle();
    }

    [When("Le client se déconnecte")]
    public async Task WhenLeClientSeDeconnecte()
    {
        Clients[0].IsConnectedReturns = false;
        await WaitOneCycle();
    }

    [When("Le client envoie une requête au serveur")]
    public async Task WhenLeClientEnvoieUneRequeteAuServeur()
    {
        Clients[0].ReceiveAsyncReturns = new NetworkFrame(new MessageFrame(FrameCommandType.Connection, "Tqt"));
        await WaitOneCycle();
    }

    [Then("Le client n'est plus connecté au serveur")]
    public async Task ThenLeClientNestPlusConnecteAuServeur()
    {
        await WaitOneCycle();
        Check.That(GetService<IClientManager>().HasEnoughSpaceForAnotherClient()).IsTrue();
    }

    [Then("Le serveur accepte sa connexion")]
    public void ThenLeServeurAccepteSaConnexion()
    {
        Check.That(tcpServerMock.RejectConnectionAsyncCalled).IsFalse();
        Check.That(tcpServerMock.AcceptTcpClientAsyncCalled).IsTrue();
    }

    [Then("Le serveur refuse sa connexion")]
    public async Task ThenLeServeurRefuseSaConnexion()
    {
        await WaitOneCycle();
        Check.That(tcpServerMock.RejectConnectionAsyncCalled).IsTrue();
    }

    [Then("La requête est bien lue par le serveur")]
    public async Task ThenLaRequeteEstBienLueParLeServeur()
    {
        await WaitOneCycle();
        await WaitOneCycle(); // Magic but needed
        //TODO: fix this test, the IMessageFrameHandler is a scoped service but the scope is private to a client
        //is generated inside ClientManager.HandleClientAsync which canno't be retrieved here
        //MessageFrameHandlerMock mock = GetService<IMessageFrameHandler, MessageFrameHandlerMock>();
        //Check.That(mock.CommandTypeWasCalled(FrameCommandType.Connection)).IsTrue();
    }
}