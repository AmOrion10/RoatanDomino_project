using CommonTests.Fixtures;
using Infrastructure.Services;

namespace UnitTests;

public class CodeGeneratorTests
{
    private readonly RandomGeneratorMock randomGeneratorMock = new();
    private readonly CodeGenerator codeGenerator;

    public CodeGeneratorTests()
    {
        codeGenerator = new CodeGenerator(randomGeneratorMock);
    }

    [Fact]
    public void ShouldNotGenerateSameCodeTwice()
    {
        string code1 = codeGenerator.GenerateCodeForLobby();
        string code2 = codeGenerator.GenerateCodeForLobby();
        Check.That(code1).IsNotEqualTo(code2);
    }

    [Fact]
    public void ShouldLoopUntilCodeWasNotBeenGenerated()
    {
        randomGeneratorMock.SetRandomsNext([0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5, 0, 2, 3, 4, 5]);
        string code1 = codeGenerator.GenerateCodeForLobby();
        string code2 = codeGenerator.GenerateCodeForLobby();
        Check.That(code1).IsNotEqualTo(code2);
    }
}