namespace Infrastructure.Database.Entities;

public class ResetPasswordCodeEntity
{
    public int Id { get; set; }
    public string EmailAddress { get; set; }
    public string Code { get; set; }
    public DateTime DateGenerated { get; set; }
} 