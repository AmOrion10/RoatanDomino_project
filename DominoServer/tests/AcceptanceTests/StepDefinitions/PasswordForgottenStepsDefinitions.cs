using Application.Interfaces;

using CommonTests.Fixtures;

using Domain;

using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;
using DominoCommon.com.domino.common.Scripts.Networking.Frame.MessageFrame;

using Infrastructure.Database.Entities;

namespace AcceptanceTests.StepDefinitions;

[Binding]
[Scope(Feature = "PasswordForgotten")]
public class PasswordForgottenStepsDefinitions : BaseStepsDefinition
{
    private const string ResetCode = "ABCDEF01";
    private string? emailAddressAsked;

    protected PasswordForgottenStepsDefinitions(ScenarioContext context) : base(context)
    {
        RegisterHandlers();
    }

    [Given(@"Il existe déjà un demande de reset de mot de passe pour l'adresse email ""([^""]*)""")]
    public void GivenIlExisteDejaUnDemandeDeResetDeMotDePassePourLadresseEmail(string emailAddress)
    {
        DbContext.EmailSended.Add(new EmailSendedEntity
        {
            DateSended = DateTime.Now,
            ToEmailAddress = emailAddress,
            EmailType = EmailType.ResetPassword
        });
        DbContext.ResetPasswordCode.Add(new ResetPasswordCodeEntity
        {
            Code = ResetCode,
            EmailAddress = emailAddress
        });
        DbContext.SaveChanges();
        emailAddressAsked = emailAddress;
    }

    [Given(@"Il existe déjà un demande de reset de mot de passe pour l'adresse email ""([^""]*)"" il y a (.*) secondes")]
    public void GivenIlExisteDejaUnDemandeDeResetDeMotDePassePourLadresseEmailIlYaSecondes(string emailAddress, int seconds)
    {
        DbContext.EmailSended.Add(new EmailSendedEntity
        {
            DateSended = DateTime.Now.AddSeconds(-seconds),
            ToEmailAddress = emailAddress,
            EmailType = EmailType.ResetPassword
        });
        DbContext.ResetPasswordCode.Add(new ResetPasswordCodeEntity
        {
            Code = ResetCode,
            EmailAddress = emailAddress
        });
        DbContext.SaveChanges();
        emailAddressAsked = emailAddress;
    }

    [When(@"Le client (.*) demande un reset de mot de passe pour l'adresse email ""([^""]*)""")]
    public async Task WhenLeClientDemandeUnResetDeMotDePassePourLadresseEmail(int clientId, string emailAddress)
    {
        await SendRequest(MessageFrameBuilder.AskResetPasswordFrame(new AskResetPasswordDto
        {
            Email = emailAddress
        }), GetClientWithId(clientId));
        emailAddressAsked = emailAddress;
    }

    [When("Le client (.*) envoie un nouveau bon mot de passe avec le code valide")]
    public async Task WhenLeClientEnvoieUnNouveauBonMotDePasseAvecLeCodeValide(int clientId)
    {
        await SendRequest(MessageFrameBuilder.ResetPasswordFrame(new ResetPasswordDto
        {
            Code = ResetCode,
            Password = "GoodPassword8",
            Email = emailAddressAsked
        }), GetClientWithId(clientId));
    }

    [When("Le client (.*) envoie un nouveau mauvais mot de passe avec le code valide")]
    public async Task WhenLeClientEnvoieUnNouveauMauvaisMotDePasseAvecLeCodeValide(int clientId)
    {
        await SendRequest(MessageFrameBuilder.ResetPasswordFrame(new ResetPasswordDto
        {
            Code = ResetCode,
            Password = "bad",
            Email = emailAddressAsked
        }), GetClientWithId(clientId));
    }

    [When("Le client (.*) envoie son nouveau mot de passe avec le code non valide")]
    public async Task WhenLeClientEnvoieSonNouveauMotDePasseAvecLeCodeNonValide(int clientId)
    {
        await SendRequest(MessageFrameBuilder.ResetPasswordFrame(new ResetPasswordDto
        {
            Code = "WRONGCODE",
            Password = "GoodPassword8",
            Email = emailAddressAsked
        }), GetClientWithId(clientId));
    }

    [Then(@"Un mail est envoyé à l'adresse ""([^""]*)""")]
    public void ThenUnMailEstEnvoyeALadresse(string emailAddress)
    {
        Check.That(GetService<IMailSender, MailSenderMock>().MailsSended).Contains(emailAddress);
        Check.That(DbContext.EmailSended.Any(u => u.ToEmailAddress.Equals(emailAddress))).IsTrue();
    }

    [Then("Le mot de passe du client a bien changé")]
    public void ThenLeMotDePasseDuClientABienChange()
    {
        Check.That(DbContext.Users.First(u => u.Email.Equals(emailAddressAsked)).Password).IsNotEqualTo(emailAddressAsked);
    }

    [Then(@"Le mot de passe du client reste inchangé")]
    public void ThenLeMotDePasseDuClientResteInchange()
    {
        Check.That(DbContext.Users.First(u => u.Email.Equals(emailAddressAsked)).Password).IsEqualTo(emailAddressAsked);
    }
}