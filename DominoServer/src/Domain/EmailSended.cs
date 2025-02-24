namespace Domain;

public class EmailSended
{
    public int Id { get; set; }
    public DateTime DateSended { get; set; }
    public required string ToEmail { get; set; }
    public EmailType Type { get; set; }
}