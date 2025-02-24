using Application.Interfaces.Managers;

using CommonTests.Fixtures;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "Chat")]
public class ChatStepsDefinition : BaseStepsDefinition
{
    private const string Message = "HelloWorld!";
    private ClientMock? clientSender;
    private int chatId;
    
    protected ChatStepsDefinition(ScenarioContext context) : base(context)
    {
        RegisterHandlers();
    }
    
    [Given("Un client d'id (.*) est connecté à un chat")]
    public void GivenUnClientDidEstConnecteAUnChat(int clientId)
    {
        if (chatId == 0)
        {
            chatId = GetService<IChatManager>().CreateChat("MonChat");
        }
        GetService<IChatManager>().ClientJoinChat(GetClientWithId(clientId), chatId);
    }

    [Given("Trois clients d'id (.*), (.*) et (.*) sont connecté à un chat")]
    public void GivenTroisClientsDidEtSontConnecteAUnChat(int clientId1, int clientId2, int clientId3)
    {
        GivenUnClientDidEstConnecteAUnChat(clientId1);
        GivenUnClientDidEstConnecteAUnChat(clientId2);
        GivenUnClientDidEstConnecteAUnChat(clientId3);
    }

    [When("Le client (.*) envoie un message dans le chat")]
    public async Task WhenLeClientEnvoieUnMessageDansLeChat(int clientId)
    {
        clientSender = GetClientWithId(clientId);
        await SendRequest(MessageFrameBuilder.SendMessageFrame(new SendChatMessageDto
        {
            Message = Message,
            ChatId = chatId
        }), GetClientWithId(clientId));
    }

    [Then("Tous les clients connectés au chat le reçoivent")]
    public void ThenTousLesClientsConnectesAuChatLeRecoivent()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.ReceiveChatMessage);
            ReceiveChatMessageDto dto = frame.GetObjectFromMessage<ReceiveChatMessageDto>();
            Check.That(dto.Message).IsEqualTo(Message);
            Check.That(dto.ChatId).IsEqualTo(chatId);
            Check.That(dto.Sender).IsEqualTo(clientSender!.User!.Login);
        }
    }

    [When("Le client d'id (.*) envoie un message dans le chat global")]
    public async Task WhenLeClientDidEnvoieUnMessageDansLeChatGlobal(int clientId)
    {
        SendChatMessageDto dto = new()
        {
            ChatId = GetService<IChatManager>().GlobalChatId,
            Message = Message
        };
        clientSender = GetClientWithId(clientId);
        await SendRequest(MessageFrameBuilder.SendMessageFrame(dto), clientSender);
    }

    [Then("Tous les clients connectés au chat global le reçoivent")]
    public void ThenTousLesClientsConnectesAuChatGlobalLeRecoivent()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.ReceiveChatMessage);
            ReceiveChatMessageDto dto = frame.GetObjectFromMessage<ReceiveChatMessageDto>();
            Check.That(dto.Message).IsEqualTo(Message);
            Check.That(dto.ChatId).IsEqualTo(GetService<IChatManager>().GlobalChatId);
            Check.That(dto.Sender).IsEqualTo(clientSender!.User!.Login);
        }
    }
}