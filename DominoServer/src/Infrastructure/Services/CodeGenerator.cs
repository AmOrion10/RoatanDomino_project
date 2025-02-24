using Application.Interfaces;

namespace Infrastructure.Services;

public class CodeGenerator(IRandomGenerator randomGenerator) : ICodeGenerator
{
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int LengthLobbyCode = 6;
    private const int LengthResetPasswordCode = 8;
    private readonly List<string> lobbyCodes = [];

    public string GenerateCodeForLobby()
    {
        string generatedCode = GenerateCode(LengthLobbyCode);
        lock (lobbyCodes)
        {
            while (lobbyCodes.Contains(generatedCode))
            {
                generatedCode = GenerateCode(LengthLobbyCode);
            }
            lobbyCodes.Add(generatedCode);
        }
        return generatedCode;
    }

    public void FreeLobbyCode(string code)
    {
        lock (lobbyCodes)
        {
            lobbyCodes.Remove(code);
        }
    }

    public string GenerateCodeForResetPassword()
    {
        return GenerateCode(LengthResetPasswordCode);
    }

    private string GenerateCode(int length)
    {
        IEnumerable<char> chars = Enumerable.Range(0, length)
            .Select(x => Chars[randomGenerator.Next(0, Chars.Length)]);
        return new string(chars.ToArray());
    }
}
