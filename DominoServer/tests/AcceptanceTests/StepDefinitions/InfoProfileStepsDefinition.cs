using Application.Interfaces;
using CommonTests.Fixtures;
using DominoCommon.com.domino.common.Scripts;
using DominoCommon.com.domino.common.Scripts.Enums;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;
using Infrastructure.Database.Entities;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "InfoProfile")]
public class InfoProfileStepsDefinition : BaseStepsDefinition
{
    private string newPseudo = string.Empty;
    private string newBirthdate = string.Empty;
    private string newNationality = string.Empty;

    private long historyGameId;
    private const int gameBet = 220;
    private const long winnerId = 1;
    private const int gameDuration = 600;
    private const int turnDuration = 30;

    protected InfoProfileStepsDefinition(ScenarioContext context) : base(context)
    {
        RegisterHandlers();
        DbContext.Nationalities.Add(new NationalityEntity()
        {
            Name = "Australia"
        });
        DbContext.SaveChanges();
    }

    [Given(@"Une partie est sauvegardé avec les clients (.*) et (.*)")]
    public async Task GivenUnePartieEstSauvegardeAvecLesClientsEt(int clientId1, int clientId2)
    {
        GameEntity gameEntity = new()
        {
            StartTime = DateTime.MinValue,
            EndTime = DateTime.MinValue,
            GameMode = GameMode.UnRanked,
            Bet = gameBet,
            WinnerId = winnerId,
            GameDuration = gameDuration,
            TurnDuration = turnDuration,
        };
        await DbContext.Games.AddAsync(gameEntity);
        await DbContext.SaveChangesAsync();


        gameEntity.Users.Add(await DbContext.Users.FirstAsync(u => u.Id == GetClientWithId(clientId1).User!.Id));
        gameEntity.Users.Add(await DbContext.Users.FirstAsync(u => u.Id == GetClientWithId(clientId2).User!.Id));

        await DbContext.SaveChangesAsync();

        historyGameId = gameEntity.Id;
    }

    [When("Le client (.*) demande à accéder à ses informations")]
    public async Task WhenLeClientDemandeAAccederASesInformations(int clientId)
    {
        await SendRequest(MessageFrameBuilder.GetInfoProfileFrame(), GetClientWithId(clientId));
    }

    [When(@"Le client (.*) demande à modifier ses informations avec le login ""([^""]*)"", la date de naissance ""([^""]*)"" et la nationalité ""([^""]*)""")]
    public async Task WhenLeClientDemandeAModifierSesInformationsAvecLeLoginLaDateDeNaissanceEtLaNationalite(int clientId, string login, string birthdate, string nationality)
    {
        newPseudo = login;
        newBirthdate = birthdate;
        newNationality = nationality;
        UpdateProfileDto dto = new()
        {
            Login = login,
            Birthdate = birthdate,
            Nationality = nationality
        };
        await SendRequest(MessageFrameBuilder.UpdateProfileFrame(dto), GetClientWithId(clientId));
    }

    [When(@"Le client (.*) demande à modifier son mot de passe ""([^""]*)"" par ""([^""]*)""")]
    public async Task WhenLeClientDemandeAModifierSonMotDePassePar(int clientId, string actualPassword, string newPassword)
    {
        PasswordChangeDto dto = new()
        {
            ActualPassword = actualPassword,
            NewPassword = newPassword
        };
        await SendRequest(MessageFrameBuilder.UpdatePasswordFrame(dto), GetClientWithId(clientId));
    }

    [When(@"Le client (.*) demande à accéder aux informations du client ""([^""]*)""")]
    public async Task WhenLeClientDemandeAAccederAuxInformationsDuClient(int clientId1, string login2)
    {
        GetOtherUserInfoDto dto = new()
        {
            Login = login2
        };
        await SendRequest(MessageFrameBuilder.GetOtherUserInfoFrame(dto), GetClientWithId(clientId1));
    }

    [When(@"Le client (.*) demande à accéder à son historique de parties")]
    public async Task WhenLeClientDemandeAAccederASonHistoriqueDeParties(int clientId)
    {
        await SendRequest(MessageFrameBuilder.GetAllHistoryGames(), GetClientWithId(clientId));
    }

    [When(@"Le client (.*) demande à accéder aux détails d'une partie")]
    public async Task WhenLeClientDemandeAAccederAuxDetailsDunePartie(int clientId)
    {
        GetHistoryGameDto dto = new()
        {
            Id = historyGameId
        };
        await SendRequest(MessageFrameBuilder.GetHistoryGame(dto), GetClientWithId(clientId));
    }

    [Then("Le client (.*) reçoit bien ses informations")]
    public void ThenLeClientRecoitBienSesInformations(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.Response);
        Check.That(frame.CommandType).IsEqualTo(FrameCommandType.Response);
        Check.That(frame.SubCommandType).IsEqualTo(FrameCommandType.GetInfoProfile);

        ResponseObjectDto<InfoProfileDto> dto = VerifyOkResultForClient<ResponseObjectDto<InfoProfileDto>>(client);
        Check.That(dto.Object.User.Email).IsEqualTo(client.User!.Email);
        Check.That(dto.Object.User.Login).IsEqualTo(client.User!.Login);
        Check.That(dto.Object.User.Score).IsEqualTo(client.User!.Score);
        Check.That(dto.Object.User.DateSignup).IsEqualTo(client.User!.DateSignup.ToString(Constants.LongFormatDate));
        Check.That(dto.Object.User.BirthDate).IsEqualTo(client.User!.Birthdate.ToString(Constants.ShortFormatDate));
        Check.That(dto.Object.User.Nationality).IsEqualTo(client.User!.Nationality);

        StatisticDto statisticDto = dto.Object.Statistic;
        Check.That(statisticDto.NumberOfGamePlayed).IsEqualTo(0);
        Check.That(statisticDto.NumberOfGameWon).IsEqualTo(0);
        Check.That(statisticDto.NumberOfGameLost).IsEqualTo(0);
    }

    [Then(@"Le client (.*) a bien modifié ses informations")]
    public void ThenLeClientABienModifieSesInformations(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        MessageFrame frame = client.GetLastMessageFrameByCommandType(FrameCommandType.Response);
        Check.That(frame.CommandType).IsEqualTo(FrameCommandType.Response);
        Check.That(frame.SubCommandType).IsEqualTo(FrameCommandType.GetInfoProfile);

        ResponseObjectDto<InfoProfileDto> dto = VerifyOkResultForClient<ResponseObjectDto<InfoProfileDto>>(client);

        Check.That(dto.Object.User.Login).IsEqualTo(client.User!.Login);
        Check.That(dto.Object.User.Login).IsEqualTo(newPseudo);

        Check.That(dto.Object.User.BirthDate).IsEqualTo(client.User!.Birthdate.ToString(Constants.ShortFormatDate));
        Check.That(dto.Object.User.BirthDate).IsEqualTo(newBirthdate);

        Check.That(dto.Object.User.Nationality).IsEqualTo(client.User!.Nationality);

        Check.That(dto.Object.Statistic).IsNotNull();
    }

    [Then(@"Le client (.*) a bien modifié son mot de passe")]
    public async Task ThenLeClientABienModifieSonMotDePasse(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        UserEntity user = await DbContext.Users.FirstAsync(u => u.Id == client.User!.Id);
        Check.That(VerifyPassword(user, "GuigsLeGuigs1")).IsEqualTo(true);
    }

    [Then(@"Le client (.*) reçoit bien les informations du client (.*)")]
    public void ThenLeClientRecoitBienLesInformationsDuClient(int clientId1, int clientId2)
    {
        ClientMock client1 = GetClientWithId(clientId1);
        ClientMock client2 = GetClientWithId(clientId2);
        MessageFrame frame = client1.GetMessageFrameByCommandType(FrameCommandType.Response);
        Check.That(frame.CommandType).IsEqualTo(FrameCommandType.Response);
        Check.That(frame.SubCommandType).IsEqualTo(FrameCommandType.GetOtherUserInfo);

        ResponseObjectDto<OtherUserInfoDto> dto = VerifyOkResultForClient<ResponseObjectDto<OtherUserInfoDto>>(client1);
        Check.That(dto.Object.Login).IsEqualTo(client2.User!.Login);
        Check.That(dto.Object.Score).IsEqualTo(client2.User!.Score);
        Check.That(dto.Object.Nationality).IsEqualTo(client2.User!.Nationality);
    }

    [Then(@"Le client (.*) reçoit la liste de ses parties jouées")]
    public void ThenLeClientRecoitLaListeDeSesPartiesJouees(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.Response);
        Check.That(frame.CommandType).IsEqualTo(FrameCommandType.Response);
        Check.That(frame.SubCommandType).IsEqualTo(FrameCommandType.GetAllHistoryGames);

        ResponseObjectDto<AllHistoryGamesDto> dto = VerifyOkResultForClient<ResponseObjectDto<AllHistoryGamesDto>>(client);
        Check.That(dto.Object.HistoryGames.Count).IsStrictlyGreaterThan(0);
        Check.That(dto.Object.HistoryGames[0].EndTime).IsEqualTo(DateTime.MinValue.ToString(Constants.LongFormatDate));
        Check.That(dto.Object.HistoryGames[0].Ranked).IsEqualTo(false); // GameMode.Unranked
    }

    [Then(@"Le client (.*) reçoit toutes les informations de la partie")]
    public void ThenLeClientRecoitToutesLesInformationsDeLaPartie(int clientId)
    {
        ClientMock client = GetClientWithId(clientId);
        MessageFrame frame = client.GetMessageFrameByCommandType(FrameCommandType.Response);
        Check.That(frame.CommandType).IsEqualTo(FrameCommandType.Response);
        Check.That(frame.SubCommandType).IsEqualTo(FrameCommandType.GetHistoryGame);

        ResponseObjectDto<HistoryGameDto> dto = VerifyOkResultForClient<ResponseObjectDto<HistoryGameDto>>(client);
        Check.That(dto.Object.StartTime).IsEqualTo(DateTime.MinValue.ToString(Constants.LongFormatDate));
        Check.That(dto.Object.EndTime).IsEqualTo(DateTime.MinValue.ToString(Constants.LongFormatDate));
        Check.That(dto.Object.TotalBet).IsEqualTo(gameBet);
        Check.That(dto.Object.GeneralTimer).IsEqualTo(gameDuration);
        Check.That(dto.Object.PlayerTurnTimer).IsEqualTo(turnDuration);
        Check.That(dto.Object.Ranked).IsEqualTo(false); // GameMode.Unranked
        Check.That(dto.Object.WinnerId).IsEqualTo(clientId); // Winner is the client 1
        Check.That(dto.Object.Players.Count).IsEqualTo(2);
        Check.That(dto.Object.Moves.Count).IsEqualTo(0);
    }

    public static bool VerifyPassword(UserEntity user, string password)
    {
        return PasswordHasher.Verify(password, user.Password);
    }
}