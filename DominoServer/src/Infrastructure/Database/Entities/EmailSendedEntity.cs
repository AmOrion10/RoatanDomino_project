using Domain;

namespace Infrastructure.Database.Entities;

public class EmailSendedEntity
{
    public int Id { get; set; }
    public string ToEmailAddress { get; set; }
    public DateTime DateSended { get; set; }
    public EmailType EmailType { get; set; }

    public EmailSended ToDomainEntity()
    {
        return new EmailSended
        {
            Id = Id,
            DateSended = DateSended,
            ToEmail = ToEmailAddress,
            Type = EmailType
        };
    }
}