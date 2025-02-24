using Application.Exceptions.Game;
using Application.Interfaces.Managers;
using Application.Messages.Commands.Game;

using CommonTests;
using CommonTests.Fixtures;

using Domain.Game;

using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.Game;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Infrastructure.Database.Entities;
using Infrastructure.Services.Managers;

using Microsoft.EntityFrameworkCore;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "Game")]
public class GameStepsDefinition : BaseStepsDefinition
{
    private long currentPlayerId;

    protected GameStepsDefinition(ScenarioContext context) : base(context)
    {
        RegisterHandlers();
    }

    [Given("Un client a déjà placé un domino")]
    public void GivenUnClientADejaPlaceUnDomino()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        game.PlaceDomino(new GameMove(new Domino(5, 6), 30, 29, Orientation.HorizontalLeft, game.PlayerPlaying.Id));
        game.NextTurn();
    }

    [Given("La pioche est vide")]
    public void GivenLaPiocheEstVide()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        while (!game.Stack.IsEmpty())
        {
            game.Stack.Draw();
        }
    }

    [Given("Un des joueurs n'a plus qu'un domino a posé")]
    public void GivenUnDesJoueursNunDominoAPose()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        game.PlayerPlaying.Hand.Clear();
        game.PlayerPlaying.Hand.Add(new Domino(1, 5));
    }

    [Given("Un des joueurs n'a plus qu'un domino non plaçable a posé")]
    public void GivenUnDesJoueursNunDominoNonPlacableAPose()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        game.PlayerPlaying.Hand.Clear();
        game.PlayerPlaying.Hand.Add(new Domino(3, 4));
        game.NextTurn();
    }

    [Given("Deux des joueurs n'a plus qu'un domino non plaçable a posé")]
    public void GivenDeuxDesJoueursNunDominoNonPlacableAPose()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        game.PlayerPlaying.Hand.Clear();
        game.PlayerPlaying.Hand.Add(new Domino(3, 4));
        game.NextTurn();
        game.PlayerPlaying.Hand.Clear();
        game.PlayerPlaying.Hand.Add(new Domino(6, 4));
        game.NextTurn();
    }

    [Given("Les joueurs ne peuvent poser aucun domino")]
    public void GivenLesJoueursNePeuventPoserAucunDomino()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        game.PlayerPlaying.Hand.Clear();
        game.PlayerPlaying.Hand.Add(new Domino(3, 4));
        game.NextTurn();
        game.PlayerPlaying.Hand.Clear();
        game.PlayerPlaying.Hand.Add(new Domino(6, 4));
        game.NextTurn();
        game.PlayerPlaying.Hand.Clear();
        game.PlayerPlaying.Hand.Add(new Domino(2, 4));
        game.NextTurn();
    }

    [Given("La pioche a le domino (.*), (.*)")]
    public void GivenLaPiocheALeDomino(int side1, int side2)
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        while (game.Stack.GetDominoInStack() > 0)
        {
            game.Stack.Draw();
        }
        game.Stack.Add(new Domino(side1, side2));
    }


    [Given("La pioche a les domino (.*), (.*) et (.*), (.*)")]
    public void GivenLaPiocheALesDominoEt(int d1Side1, int d1Side2, int d2Side1, int d2Side2)
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        while (game.Stack.GetDominoInStack() > 0)
        {
            game.Stack.Draw();
        }
        game.Stack.Add(new Domino(d1Side1, d1Side2));
        game.Stack.Add(new Domino(d2Side1, d2Side2));
    }

    [Given("Un joueur a les dominos (.*), (.*) et (.*), (.*) en mains")]
    public void GivenUnJoueurALesDominosEtEnMains(int side11, int side21, int side12, int side22)
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id)!;
        game.PlayerPlaying.Hand.Clear();
        game.PlayerPlaying.Hand.Add(new Domino(side11, side21));
        game.PlayerPlaying.Hand.Add(new Domino(side12, side22));
    }

    [Given(@"Les deux clients d'id (.*) et (.*) ont un score de classée égale à (.*)")]
    public async Task GivenLesDeuxClientsDidEtOntUnScoreDeClasseeEgaleA(int clientId1, int clientId2, int score)
    {
        ClientMock client1 = GetClientWithId(clientId1);
        UserEntity entity1 = await DbContext.Users.FirstAsync(u => u.Id == client1.User!.Id);
        entity1.Score = score;
        await DbContext.SaveChangesAsync();
        ClientMock client2 = GetClientWithId(clientId2);
        UserEntity entity2 = await DbContext.Users.FirstAsync(u => u.Id == client2.User!.Id);
        entity2.Score = score;
        await DbContext.SaveChangesAsync();
    }

    [When("La partie débute")]
    public async Task WhenLaPartieDebute()
    {
        _ = await Dispatcher.Dispatch(new GameStartingCommand(Game!.Id));
    }

    [When("Le client (.*) se met prêt dans la game")]
    public async Task WhenLeClientSeMetPretDansLaGame(int clientId)
    {
        await SendRequest(MessageFrameBuilder.ReadyInGameFrame(), GetClientWithId(clientId));
    }

    [When("Le client place un domino")]
    public async Task WhenLeClientPlaceUnDomino()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");

        PlayerPlayDto dto = new()
        {
            IdUserPlayed = game.PlayerPlaying.Id,
            PlayActionType = PlayActionType.PlaceDomino,
            Moves = new MoveDto
            {
                Domino = new DominoDto
                {
                    Side1 = 5,
                    Side2 = 6
                },
                Col = 29,
                Row = 30,
                Orientation = Orientation.HorizontalLeft,
                PlayerId = game.PlayerPlaying.Id
            }
        };

        await SendRequest(MessageFrameBuilder.PlayerPlaysFrame(dto), Clients.First(c => c.User!.Id == game.PlayerPlaying.Id));
        await WaitOneCycle();
    }

    [When("Le client souhaite placer un mauvais domino")]
    public async Task WhenLeClientSouhaitePlacerUnMauvaisDomino()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");

        PlayerPlayDto dto = new()
        {
            IdUserPlayed = game.PlayerPlaying.Id,
            PlayActionType = PlayActionType.PlaceDomino,
            Moves = new MoveDto
            {
                Domino = new DominoDto
                {
                    Side1 = 0,
                    Side2 = 4
                },
                Col = 29,
                Row = 30,
                Orientation = Orientation.HorizontalLeft,
                PlayerId = game.PlayerPlaying.Id
            }
        };

        await SendRequest(MessageFrameBuilder.PlayerPlaysFrame(dto), Clients.First(c => c.User!.Id == game.PlayerPlaying.Id));
        await WaitOneCycle();
    }

    [When("Le client pioche un domino")]
    public async Task WhenLeClientPiocheUnDomino()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");

        PlayerPlayDto dto = new()
        {
            IdUserPlayed = game.PlayerPlaying.Id,
            PlayActionType = PlayActionType.Draw,
            Moves = new MoveDto()
        };

        await SendRequest(MessageFrameBuilder.PlayerPlaysFrame(dto), Clients.First(c => c.User!.Id == game.PlayerPlaying.Id));
        await WaitOneCycle();
    }

    [When("Le client place son dernier domino")]
    public async Task WhenLeClientPlaceSonDernierDomino()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");

        PlayerPlayDto dto = new()
        {
            IdUserPlayed = game.PlayerPlaying.Id,
            PlayActionType = PlayActionType.PlaceDomino,
            Moves = new MoveDto
            {
                Domino = new DominoDto
                {
                    Side1 = 1,
                    Side2 = 5
                },
                Col = 29,
                Row = 30,
                Orientation = Orientation.HorizontalLeft,
                PlayerId = game.PlayerPlaying.Id
            }
        };

        await SendRequest(MessageFrameBuilder.PlayerPlaysFrame(dto), Clients.First(c => c.User!.Id == game.PlayerPlaying.Id));
        await WaitOneCycle();
    }

    [When("L'autre client place un domino")]
    public async Task WhenLautreClientPlaceUnDomino()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");
        game.PlayerPlaying.Hand.Add(new Domino(1, 5));

        PlayerPlayDto dto = new()
        {
            IdUserPlayed = game.PlayerPlaying.Id,
            PlayActionType = PlayActionType.PlaceDomino,
            Moves = new MoveDto
            {
                Domino = new DominoDto
                {
                    Side1 = 1,
                    Side2 = 5
                },
                Col = 29,
                Row = 30,
                Orientation = Orientation.HorizontalLeft,
                PlayerId = game.PlayerPlaying.Id
            }
        };

        await SendRequest(MessageFrameBuilder.PlayerPlaysFrame(dto), Clients.First(c => c.User!.Id == game.PlayerPlaying.Id));
        await WaitOneCycle();
    }

    [When("Le client place le domino (.*), (.*) sur l'emplacement (.*), (.*)")]
    public async Task WhenLeClientPlaceLeDominoSurLemplacement(int side1, int side2, int row, int col)
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");

        PlayerPlayDto dto = new()
        {
            IdUserPlayed = game.PlayerPlaying.Id,
            PlayActionType = PlayActionType.PlaceDomino,
            Moves = new MoveDto
            {
                Domino = new DominoDto
                {
                    Side1 = side1,
                    Side2 = side2
                },
                Col = col,
                Row = row,
                Orientation = Orientation.HorizontalLeft,
                PlayerId = game.PlayerPlaying.Id
            }
        };

        await SendRequest(MessageFrameBuilder.PlayerPlaysFrame(dto), Clients.First(c => c.User!.Id == game.PlayerPlaying.Id));
        await WaitOneCycle();
    }

    [When(@"Le client d'id (.*) quitte la partie")]
    public async Task WhenLeClientDidQuitteLaPartie(int clientId)
    {
        await SendRequest(MessageFrameBuilder.LeaveGameFrame(), GetClientWithId(clientId));
    }

    [Then("La game n'existe plus")]
    public void ThenLaGameNexistePlus()
    {
        Check.That(GetService<IGameManager>().GetById(Game!.Id)).IsNull();
    }

    [Then("Tous les clients sont informés du départ du client (.*) excepté le client (.*)")]
    public void ThenTousLesClientsSontInformesDuDepartDuClientExcepteLeClient(int clientId, int _)
    {
        ClientMock clientLeft = GetClientWithId(clientId);
        Check.ThatCode(() => clientLeft.GetMessageFrameByCommandType(FrameCommandType.PlayerLeftGame)).Throws<MessageFrameNotFoundException>();
        foreach (ClientMock client in Clients)
        {
            if (client.Id == clientId) continue;
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.PlayerLeftGame);
            PlayerDto playerLeft = frame.GetObjectFromMessage<PlayerDto>();
            Check.That(playerLeft.Id).IsEqualTo(clientLeft.User!.Id);
            Check.That(playerLeft.Name).IsEqualTo(clientLeft.User!.Login);
        }
    }

    [Then("Les dominos du client (.*) sont remis dans la pioche")]
    public void ThenLesDominosDuClientSontRemisDansLaPioche(int _)
    {
        // Pour vérifier que tous les dominos du client sont dans la pioche on vérifie qu'on peut piocher au moins 13 fois
        // Car il y avait 3 joueurs à la base et chacun d'entre eux a 7 dominos et il y a 28 dominos au total, -1 domino pour le placement initial
        for (int i = 0; i < 13; i++)
        {
            Check.That(Game!.Draw(Game.PlayerPlaying.Id)).IsNotNull();
        }
    }

    [Then("Le client (.*) ne fait plus parti de la game")]
    public void ThenLeClientNeFaitPlusPartiDeLaGame(int clientId)
    {
        Check.That(GetService<IGameManager>().GetForClient(GetClientWithId(clientId))).IsNull();
    }

    [Then("Le client (.*) est prêt dans la game")]
    public void ThenLeClientEstPretDansLaGame(int clientId)
    {
        ClientMock mock = GetClientWithId(clientId);
        Check.That(GetService<IGameManager>().GetById(Game!.Id)!.Players.First(gp => gp.Id == mock.User!.Id).IsReady).IsTrue();
    }

    [Then("Un timer est envoyé aux clients pour indiquer que la game commence")]
    public void ThenUnTimerEstEnvoyeAuxClientsPourIndiquerQueLaGameCommence()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.GameStartingTimer);
            TimerDto dto = frame.GetObjectFromMessage<TimerDto>();
            Check.That(dto.Duration).IsEqualTo(IGameManager.GameTimerStarting);
        }
    }

    [Then("Le domino est placé et les informations de jeu sont envoyés aux joueurs")]
    public void ThenLeDominoEstPlaceEtLesInformationsDeJeuSontEnvoyesAuxJoueurs()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");
        foreach (ClientMock client in Clients)
        {
            MessageFrame frameMove = client.GetMessageFrameByCommandType(FrameCommandType.PlayerPlays);
            PlayerPlayDto dtoMove = frameMove.GetObjectFromMessage<PlayerPlayDto>();
            Check.That(dtoMove.PlayActionType).IsEqualTo(PlayActionType.PlaceDomino);

            MessageFrame frameChangeTurn = client.GetMessageFrameByCommandType(FrameCommandType.ChangePlayerTurn);
            PlayerTurnDto dtoChangeTurn = frameChangeTurn.GetObjectFromMessage<PlayerTurnDto>();
            currentPlayerId = dtoChangeTurn.IdUserPlaying;
            Check.That(dtoChangeTurn.IdUserPlaying).IsStrictlyGreaterThan(0);

            MessageFrame frameDominoPerPlayer = client.GetMessageFrameByCommandType(FrameCommandType.NumberOfDominoPerPlayer);
            NumberOfDominoPerPlayerDto dtoDominoPerPlayer = frameDominoPerPlayer.GetObjectFromMessage<NumberOfDominoPerPlayerDto>();
            Check.That(dtoDominoPerPlayer.NumberOfDomino.Count).IsEqualTo(Clients.Count);
            Check.That(dtoDominoPerPlayer.NumberOfDomino.Values).ContainsOnlyElementsThatMatch(value => value == 6);

            if (client.User!.Id == dtoMove.IdUserPlayed)
            {
                GamePlayer player = game.Players.First(p => p.Id == client.User!.Id);
                Check.That(dtoMove.IdUserPlayed).IsStrictlyGreaterThan(0);
                Check.That(player.Hand.Count).IsStrictlyLessThan(7);

                Check.That(dtoChangeTurn.IdUserPlaying).IsNotEqualTo(client.User!.Id);
            }
            else if (client.User!.Id == currentPlayerId)
            {
                Check.That(dtoMove.IdUserPlayed).IsStrictlyGreaterThan(0);

                Check.That(dtoChangeTurn.IdUserPlaying).IsEqualTo(client.User!.Id);

                MessageFrame framePossibleMoves = client.GetMessageFrameByCommandType(FrameCommandType.PossibleMoves);
                PossibleMovesDto dtoPossibleMoves = framePossibleMoves.GetObjectFromMessage<PossibleMovesDto>();
                Check.That(dtoPossibleMoves.Moves).IsNotNull();
                Check.That(dtoPossibleMoves.Moves.Count).IsStrictlyGreaterThan(0);
            }
            else
            {
                Check.That(dtoMove.IdUserPlayed).IsNotEqualTo(client.User!.Id);
                Check.That(dtoChangeTurn.IdUserPlaying).IsEqualTo(client.User!.Id);
            }
        }
    }

    [Then("Un domino est récupéré de la pioche et les informations de jeu sont envoyés aux joueurs")]
    public void ThenUnDominoEstRecupereDeLaPiocheEtLesInformationsDeJeuSontEnvoyesAuxJoueurs()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");
        foreach (ClientMock client in Clients)
        {
            MessageFrame frameMove = client.GetMessageFrameByCommandType(FrameCommandType.PlayerPlays);
            PlayerPlayDto dtoMove = frameMove.GetObjectFromMessage<PlayerPlayDto>();
            Check.That(dtoMove.PlayActionType).IsEqualTo(PlayActionType.Draw);

            MessageFrame frameChangeTurn = client.GetMessageFrameByCommandType(FrameCommandType.ChangePlayerTurn);
            PlayerTurnDto dtoChangeTurn = frameChangeTurn.GetObjectFromMessage<PlayerTurnDto>();
            currentPlayerId = dtoChangeTurn.IdUserPlaying;
            Check.That(dtoChangeTurn.IdUserPlaying).IsStrictlyGreaterThan(0);

            MessageFrame frameDominoPerPlayer = client.GetMessageFrameByCommandType(FrameCommandType.NumberOfDominoPerPlayer);
            NumberOfDominoPerPlayerDto dtoDominoPerPlayer = frameDominoPerPlayer.GetObjectFromMessage<NumberOfDominoPerPlayerDto>();
            Check.That(dtoDominoPerPlayer.NumberOfDomino.Count).IsEqualTo(Clients.Count);
            Check.That(dtoDominoPerPlayer.NumberOfDomino.Values).ContainsOnlyElementsThatMatch(value => value == 6 || value == 7);

            if (client.User!.Id == dtoMove.IdUserPlayed)
            {
                GamePlayer player = game.Players.First(p => p.Id == client.User!.Id);
                Check.That(dtoMove.IdUserPlayed).IsStrictlyGreaterThan(0);
                Check.That(player.Hand.Count).IsEqualTo(7);

                Check.That(dtoChangeTurn.IdUserPlaying).IsNotEqualTo(client.User!.Id);
            }
            else if (client.User!.Id == currentPlayerId)
            {
                Check.That(dtoMove.IdUserPlayed).IsStrictlyGreaterThan(0);

                Check.That(dtoChangeTurn.IdUserPlaying).IsEqualTo(client.User!.Id);

                MessageFrame framePossibleMoves = client.GetMessageFrameByCommandType(FrameCommandType.PossibleMoves);
                PossibleMovesDto dtoPossibleMoves = framePossibleMoves.GetObjectFromMessage<PossibleMovesDto>();
                Check.That(dtoPossibleMoves.Moves).IsNotNull();
                Check.That(dtoPossibleMoves.Moves.Count).IsStrictlyGreaterThan(0);
            }
            else
            {
                Check.That(dtoMove.IdUserPlayed).IsNotEqualTo(client.User!.Id);
                Check.That(dtoChangeTurn.IdUserPlaying).IsEqualTo(client.User!.Id);
            }
        }
    }

    [Then("Le client suivant joue")]
    public void ThenLeClientSuivantJoue()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");
        foreach (ClientMock client in Clients)
        {
            MessageFrame frameChangeTurn = client.GetMessageFrameByCommandType(FrameCommandType.ChangePlayerTurn);
            PlayerTurnDto dtoChangeTurn = frameChangeTurn.GetObjectFromMessage<PlayerTurnDto>();
            Check.That(dtoChangeTurn.IdUserPlaying).IsStrictlyGreaterThan(0);
            Check.ThatCode(() => client.GetMessageFrameByCommandType(FrameCommandType.PlayerPlays)).Throws<MessageFrameNotFoundException>();
        }
    }

    [Then(@"Le client reçoit une erreur de type ""([^""]*)""")]
    public void ThenLeClientRecoitUneErreurDeType(ErrorTypes error)
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");
        foreach (ClientMock client in Clients)
        {
            if (client.User!.Id == game.PlayerPlaying.Id)
            {
                ResponseDto dto = client.LastFrameSended!.Frames.First().GetObjectFromMessage<ResponseDto>();
                Check.That(dto.OkResult).IsFalse();
                Check.That(dto.ErrorType).IsEqualTo(error);
            }
        }
    }

    [Then("La partie se finit")]
    public void ThenLaPartieSeFinit()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frameEndGame = client.GetMessageFrameByCommandType(FrameCommandType.EndGame);
            EndGameDto dtoEndGame = frameEndGame.GetObjectFromMessage<EndGameDto>();
            Check.That(dtoEndGame.NormalFinish).IsEqualTo(true);
            Check.That(dtoEndGame.Rankings[0].Id).IsEqualTo(Game!.LastGameMove.PlayerId);
        }
    }

    [Then("Le même client rejoue")]
    public void ThenLeMemeClientRejoue()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.PlayerPlays);
            PlayerPlayDto dto = frame.GetObjectFromMessage<PlayerPlayDto>();
            Check.That(dto.IdUserPlayed).IsEqualTo(game.PlayerPlaying.Id);
        }
    }

    [Then("La partie est bloquée et le classement est envoyé")]
    public void ThenLaPartieEstBloqueeEtLeClassementEstEnvoye()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.EndGame);
            EndGameDto dto = frame.GetObjectFromMessage<EndGameDto>();
            Check.That(dto.NormalFinish).IsEqualTo(true);
            Check.That(dto.Rankings.Count).IsEqualTo(3);
        }
    }

    [Then("Un joueur a posé tous ses dominos et le classement est envoyé")]
    public void ThenUnJoueurAPoseTousSesDominosEtLeClassementEstEnvoye()
    {
        foreach (ClientMock client in Clients)
        {
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.EndGame);
            EndGameDto dto = frame.GetObjectFromMessage<EndGameDto>();
            Check.That(dto.NormalFinish).IsEqualTo(true);
            Check.That(dto.Rankings.Count).IsEqualTo(3);
        }
    }

    [Then("Le score des joueurs est mis à jour")]
    public async Task ThenLeScoreDesJoueursEstMisAJour()
    {
        Game game = GetService<IGameManager, GameManager>().GetById(Game!.Id) ?? throw new GameNotFoundForPlayerException("Player isn't in a game");
        foreach (GamePlayer player in game.Players)
        {
            UserEntity entity = await DbContext.Users.FirstAsync(u => u.Id == player.Id);
            Check.That(entity.Score).IsEqualTo(9890); // both players have 9890 of score at the start of the game
        }
    }

    [Then(@"Le score des clients est mis à jour après la partie")]
    public async Task ThenLeScoreDesClientsEstMisAJourApresLaPartie()
    {
        foreach (ClientMock client in Clients)
        {
            UserEntity entity = await DbContext.Users.FirstAsync(u => u.Id == client.User!.Id);
            Check.That(entity.Score).IsStrictlyGreaterThan(9890); // both players have 9890 of score at the start of the game
            MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.EndGame);
            EndGameDto dto = frame.GetObjectFromMessage<EndGameDto>();
            Check.That(dto.Rankings.Count).IsEqualTo(2);
            Check.That(dto.Rankings[0].Gain).IsStrictlyGreaterThan(1 / 3 * 220); // minimum distribution for a ranked game for 2 players
            Check.That(dto.Rankings[1].Gain).IsStrictlyGreaterThan(1 / 3 * 220);
        }
    }
}