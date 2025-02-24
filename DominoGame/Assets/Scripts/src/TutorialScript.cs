using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TutorialScript : MonoBehaviour
{
    // Start is called before the first frame update
    private Animator animator;
    private int textInd = 0;
    private TextMeshProUGUI Text1;
    private int domsO1;
    private int domsO2;
    void Start()
    {
        GameObject tutoText1 = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.name == "TutoText");
        Text1 = tutoText1.GetComponent<TextMeshProUGUI>();

        animator = GetComponent<Animator>();
        animator.Play("TutorialAnim");
        domsO1 = 5;
        domsO2 = 4;
    }
    public void ContinueAnimation()
    {
        if (animator != null)
        {
            animator.speed = 1;
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void StopAnimation()
    {
        if (animator != null)
        {
            animator.speed = 0;
        }
    }
    
    public void ChangeText()
    {
        switch ( textInd ) 
        {
            case 0:
                Text1.text = Translation.GetTranslation(Translation.StringType.BeginTutorial);
                break;
            case 1:
                Text1.text = Translation.GetTranslation(Translation.StringType.Case1Tutorial);
                break;
            case 2:
                Text1.text = Translation.GetTranslation(Translation.StringType.Case2Tutorial);
                Text1.fontSize = 26;
                break;
            case 3:
                Text1.text = Translation.GetTranslation(Translation.StringType.Case3Tutorial);
                break;
            case 4:
                Text1.text = Translation.GetTranslation(Translation.StringType.Case4Tutorial);
                Text1.fontSize = 20;
                break;
            case 5:
                Text1.text = Translation.GetTranslation(Translation.StringType.Case5Tutorial);
                Text1.fontSize = 28;
                break;
            case 6:
                Text1.text = Translation.GetTranslation(Translation.StringType.Case6Tutorial);
                Text1.fontSize = 26;
                break;
            case 7:
                Text1.text = Translation.GetTranslation(Translation.StringType.Case7Tutorial);
                Text1.fontSize = 22;
                break;
            default:
                break;
        }
        textInd++;
    }

    public void QuitTutorial()
    {
        SceneManager.LoadScene("MenuPlayScene");
    }
    public void UpdateDominoesLeftOpp()
    {
        GameObject domsRest01 = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.name == "DominoesRestO1");
        GameObject domsRest02 = Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.name == "DominoesRestO2");
        TextMeshProUGUI DominoesRestantO1= domsRest01.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI DominoesRestantO2=domsRest02.GetComponent<TextMeshProUGUI>();
        domsO1--;
        domsO2--;
        DominoesRestantO1.text = $"{domsO1}";
        DominoesRestantO2.text = $"{domsO2}";
    }
}
