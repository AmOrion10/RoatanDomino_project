namespace Application;

public class AskResetPasswordResult(string email)
{
    public string Email { get; set; } = email;
    public string? Code { get; set; }
    public string? Login { get; set; }
}