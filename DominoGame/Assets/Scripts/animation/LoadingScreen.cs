using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingScreen : MonoBehaviour 
{

    public Animator transition;
    private int animationStateID = 0;

    public void LoadingScreenFunction()
    {
        StartCoroutine(LoadingScreenToGame());
    }
    public void LoadingScreenFunctionInGame() {
        StartCoroutine(LoadingScreenInGame());
    }
    IEnumerator LoadingScreenToGame()
    {
        // Déclenche l'animation de transition
        transition.SetTrigger("Start");

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(2);
        asyncLoad.allowSceneActivation = false;

        string stateName = transition.GetCurrentAnimatorStateInfo(0).fullPathHash.ToString();


        while (transition.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            if (stateName != transition.GetCurrentAnimatorStateInfo(0).fullPathHash.ToString())
            {
                stateName = transition.GetCurrentAnimatorStateInfo(0).fullPathHash.ToString();
                //Quand on arrive à la bonne partie de l'animation, on autorise de changer la scène de jeu
                if (animationStateID == 1) {
                    asyncLoad.allowSceneActivation = true;
                    transition.speed = 0;
                    // attendre ici que les 4 joueurs sont trouvés
                    break;
                }
                animationStateID++;
            }
            yield return null;
        }
        asyncLoad.allowSceneActivation = true; //failsafe
    }

    IEnumerator LoadingScreenInGame()
    {
        // Déclenche l'animation de transition
        transition.SetTrigger("Start");

        //Met l'animation à la bonne partie
        transition.Play(Animator.StringToHash("TransitionPartie_End"), 0, 0);

        // Attendre que l'animation soit terminée
        while (transition.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                transition.Play(Animator.StringToHash("TransitionPartie_End"), 0, 1);
            }
            yield return null;
        }
    }
}
