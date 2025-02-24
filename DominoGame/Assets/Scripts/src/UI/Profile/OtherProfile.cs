using DominoCommon.com.domino.common.Scripts.Networking.Dto.User;

using TMPro;

using UnityEngine;

public class OtherProfile : MonoBehaviour
{
    [SerializeField] private TMP_Text loginPlayerText;
    [SerializeField] private TMP_Text nationalityPlayerText;
    [SerializeField] private TMP_Text signupDatePlayerText;
    [SerializeField] private TMP_Text playerGamePlayedText;
    [SerializeField] private TMP_Text playerGameWonText;
    [SerializeField] private TMP_Text playerGameLostText;
    [SerializeField] private TMP_Text playerEloText;

    public void SetUp(OtherUserInfoDto dto)
    {
        loginPlayerText.text = dto.Login;
        nationalityPlayerText.text = dto.Nationality;
        signupDatePlayerText.text = dto.DateSignup;
        playerGamePlayedText.text = dto.Statistic.NumberOfGamePlayed.ToString();
        playerGameWonText.text = dto.Statistic.NumberOfGameWon.ToString();
        playerGameLostText.text = dto.Statistic.NumberOfGameLost.ToString();
        playerEloText.text = dto.Score.ToString();
    }
}
