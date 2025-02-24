using DominoCommon.com.domino.common.Scripts.ExtensionMethods;
using DominoCommon.com.domino.common.Scripts.Networking;
using DominoCommon.com.domino.common.Scripts.Networking.Dto;
using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

namespace UnitTests;

public class JsonConverterTests
{
    [Fact]
    public void MustConvertCorrectly()
    {
        string json = new LoggingDto
        {
            Login = "login",
            Password = "password"
        }.ToJson();
        LoggingDto testObj = json.FromJsonToObject<LoggingDto>();
        Check.That(testObj.Login).IsEqualTo("login");
        Check.That(testObj.Password).IsEqualTo("password");
    }

    [Fact]
    public void MustNotSerializeIgnoreFieldWhenNull()
    {
        string json = new ResponseDto
        {
            OkResult = true
        }.ToJson();
        Check.That(json).DoesNotContain("e:");

        string json2 = new ResponseDto
        {
            OkResult = false,
            ErrorType = ErrorTypes.Unknown
        }.ToJson();
        bool check = json2.Contains("e:") || json2.Contains("e\":");
        Check.That(check).IsTrue();
    }
}
