using Application.Exceptions.Game;
using Application.Interfaces;
using Application.Interfaces.Managers;

using CommonTests;
using CommonTests.Fixtures;

using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Chat;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Infrastructure.Services.Managers;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "ServerUpdate")]
public class ServerUpdateStepsDefinition : BaseStepsDefinition
{
    private long currentPlayerId;
    private long previousPlayerId;

    protected ServerUpdateStepsDefinition(ScenarioContext context) : base(context)
    {
        GetService<ITcpServer, TcpServerMock>().ThreadSleepActive = true;
        GetService<ISeedGenerator, SeedGeneratorMock>().Seed = 2002;
        RegisterHandlers();
    }

    [Given("Le timer de la game a débuté")]
    public void GivenLeTimerDeLaGameADebute()
    {
        GetService<IGameManager>().StartTimerForGame(Game!.Id);
    }

    [Given("Le timer d'avant partie a expiré")]
    public async Task GivenLeTimerDavantPartieAExpire()
    {
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = new TimeSpan(0, 0, 0, ITimerManager.LobbyTimerAtLeastOneReady);
        await WaitOneCycle();
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = default;
    }

    [Given("Le timer de cours de partie a débuté")]
    public async Task GivenLeTimerDeCoursDePartieADebute()
    {
        GetService<IGameManager>().StartTimerGeneralGame(Game!.Id);
        await Task.Delay(3000);
    }

    [When("Le timer d'avant partie arrive à terme")]
    public async Task WhenLeTimerDavantPartieArriveATerme()
    {
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = new TimeSpan(0, 0, 0, ITimerManager.LobbyTimerAtLeastOneReady);
        await WaitOneCycle();
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = default;
    }

    [When("Le timer du début de partie est arrivé à expiration")]
    public async Task WhenLeTimerDuDebutDePartieEstArriveAExpiration()
    {
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = new TimeSpan(0, 0, 0, IGameManager.GameTimerStarting);
        await WaitOneCycle();
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = default;
    }

    [When("Le timer de cours de partie est arrivé à expiration")]
    public async Task WhenLeTimerDeCoursDePartieEstArriveAExpiration()
    {
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = new TimeSpan(0, 0, 0, IGameManager.GameTimerGeneral);
        await WaitOneCycle();
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = default;
    }

    [When("Le timer de tour d'un joueur est arrivé à expiration")]
    public async Task WhenLeTimerDeTourDunJoueurEstArriveAExpiration()
    {
        await Task.Delay(2000);
        previousPlayerId = Game!.PlayerPlaying.Id;
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = new TimeSpan(0, 0, 0, IGameManager.GameTimerPerPlayer);
        await WaitOneCycle();
        GetService<IChronometer, ChronometerMock>().GetTimedElapsedReturns = default;
    }

    [Then("Les clients reçoivent l'information du début de partie")]
    public async Task ThenLesClientsRecoiventLinformationDuDebutDePartie()
    {
        await WaitOneCycle();
        foreach (ClientMock client in Clients)
        {
            Check.ThatCode(() => client.GetMessageFrameByCommandType(FrameCommandType.LobbyTimerExpired)).DoesNotThrow();
        }
    }

    [Then("Le lobby est convertit en game")]
    public void ThenLeLobbyEstConvertitEnGame()
    {
        Check.ThatCode(() => GetService<IGameManager>().GetById(Lobby!.Id)).DoesNotThrow();
    }

    [Then("Chaque joueur reçoit une liste de ses domino")]
    public async Task ThenChaqueJoueurRecoitUneListeDeSesDomino()
    {
        await Task.Delay(10000);
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.DominoDistribution);
            HandDto dto = frame.GetObjectFromMessage<HandDto>();
            Check.That(dto.Dominoes).CountIs(7);
        }
    }

    [Then("Tous les clients sont connectés au chat de la partie")]
    public void ThenTousLesClientsSontConnectesAuChatDeLaPartie()
    {
        Check.That(GetService<IChatManager>().GetById(Game!.ChatId)).IsNotNull();
        foreach (ClientMock client in Clients)
        {
            Check.That(GetService<IChatManager>().ClientIsInChat(client, Game!.ChatId)).IsTrue();

            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.JoinChat);
            ChatDto chatDto = frame.GetObjectFromMessage<ChatDto>();
            Check.That(chatDto.Id).IsEqualTo(Game!.ChatId);
        }
    }

    [Then("Un premier domino est placé automatiquement")]
    public void ThenUnPremierDominoEstPlaceAutomatiquement()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.PlayerPlays);
            PlayerPlayDto dto = frame.GetObjectFromMessage<PlayerPlayDto>();
            Check.That(dto.PlayActionType).IsEqualTo(PlayActionType.PlaceDomino);
            Check.That(dto.IdUserPlayed).IsStrictlyGreaterThan(0);
        }
    }

    [Then("Chaque joueur savent qui commence à jouer")]
    public void ThenChaqueJoueurSaventQuiCommenceAJouer()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.ChangePlayerTurn);
            PlayerTurnDto dto = frame.GetObjectFromMessage<PlayerTurnDto>();
            Check.That(dto.IdUserPlaying).IsStrictlyGreaterThan(0);
            currentPlayerId = dto.IdUserPlaying;
            Check.That(dto.IdUserPlaying).IsNotEqualTo(game.LastGameMove.PlayerId);
        }
    }

    [Then("Le client qui commence à jouer reçoit la liste des mouvements possibles")]
    public void ThenLeClientQuiCommenceAJouerRecoitLaListeDesMouvementsPossibles()
    {
        foreach (ClientMock client in Clients)
        {
            if (client.User!.Id == currentPlayerId)
            {
                MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.PossibleMoves);
                PossibleMovesDto dto = frame.GetObjectFromMessage<PossibleMovesDto>();
                Check.That(dto.Moves.Count).IsStrictlyGreaterThan(0);
                Check.That(client.User!.Id).IsEqualTo(currentPlayerId);
            }
            else
            {
                Check.ThatCode(() => client.GetMessageFrameByCommandType(FrameCommandType.PossibleMoves)).Throws<MessageFrameNotFoundException>();
                Check.That(client.User!.Id).IsNotEqualTo(currentPlayerId);
            }
        }
    }

    [Then("Les clients reçoivent le nombre de domino de chaque joueurs")]
    public void ThenLesClientsRecoiventLeNombreDeDominoDeChaqueJoueurs()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.NumberOfDominoPerPlayer);
            NumberOfDominoPerPlayerDto dto = frame.GetObjectFromMessage<NumberOfDominoPerPlayerDto>();
            Check.That(dto.NumberOfDomino.Count).IsEqualTo(Clients.Count);
        }
    }

    [Then("Le timer de cours de partie débute")]
    public void ThenLeTimerDeCoursDePartieDebute()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.GameGeneralTimer);
            TimerDto dto = frame.GetObjectFromMessage<TimerDto>();
            Check.That(dto.HasStarted).IsTrue();
        }
    }

    [Then("Le classement est envoyé aux joueurs")]
    public async Task ThenLeClassementEstEnvoyeAuxJoueurs()
    {
        await Task.Delay(3000);
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.EndGame);
            EndGameDto dto = frame.GetObjectFromMessage<EndGameDto>();
            Check.That(dto.NormalFinish).IsTrue();
        }
    }

    [Then("Le joueur suivant joue")]
    public async Task ThenLeJoueurSuivantJoue()
    {
        await Task.Delay(10000);
        Game = GetService<IGameManager>().GetById(Game!.Id);
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetLastMessageFrameByCommandType(FrameCommandType.ChangePlayerTurn);
            PlayerTurnDto dto = frame.GetObjectFromMessage<PlayerTurnDto>();
            Check.That(dto.IdUserPlaying).IsNotEqualTo(previousPlayerId);
            Check.That(dto.Timer.HasStarted).IsTrue();
            Check.That(dto.Timer.Duration).IsLessOrEqualThan(IGameManager.GameTimerPerPlayer);
            
            if (client.User!.Id == Game!.PlayerPlaying.Id)
            {
                MessageFrame movesFrame = client.GetLastMessageFrameByCommandType(FrameCommandType.PossibleMoves);
                PossibleMovesDto movesDto = movesFrame.GetObjectFromMessage<PossibleMovesDto>();
                Check.That(movesDto.Moves).IsNotNull();

                MessageFrame drawFrame = client.GetLastMessageFrameByCommandType(FrameCommandType.PlayerPlays);
                PlayerPlayDto drawDto = drawFrame.GetObjectFromMessage<PlayerPlayDto>();
                Check.That(drawDto.Moves).IsNull();
            }
            else if (client.User!.Id == previousPlayerId)
            {
                MessageFrame drawFrame = client.GetLastMessageFrameByCommandType(FrameCommandType.PlayerPlays);
                PlayerPlayDto drawDto = drawFrame.GetObjectFromMessage<PlayerPlayDto>();
                Check.That(client.User!.Id).IsEqualTo(drawDto.IdUserPlayed);
                Check.That(drawDto.Moves).IsNotNull();
            }
            else
            {
                MessageFrame drawFrame = client.GetLastMessageFrameByCommandType(FrameCommandType.PlayerPlays);
                PlayerPlayDto drawDto = drawFrame.GetObjectFromMessage<PlayerPlayDto>();
                Check.That(drawDto.Moves).IsNull();
            }
        }
    }

    [Then("Les clients sont déconnectés du chat du lobby")]
    public void ThenLesClientsSontDeconnectesDuChatDuLobby()
    {
        Check.That(GetService<IChatManager>().GetById(Lobby!.ChatId)).IsNull();
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.LeaveChat);
            ChatDto chatDto = frame.GetObjectFromMessage<ChatDto>();
            Check.That(chatDto.Id).IsEqualTo(Lobby!.ChatId);
        }
    }
}
