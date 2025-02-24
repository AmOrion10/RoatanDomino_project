namespace Application.Interfaces;

public interface ICodeGenerator
{
    string GenerateCodeForLobby();
    void FreeLobbyCode(string code);
    string GenerateCodeForResetPassword();
}
