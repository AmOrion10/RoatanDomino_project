using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

public class TutorialNavigator : MonoBehaviour
{
    [SerializeField] private Color circleSelectedColor;
    [SerializeField] private Color circleDefaultColor;
    [SerializeField] private List<Button> circles;
    [SerializeField] private List<GameObject> tutorialSteps;

    private int step = 0;

    public void Next()
    {
        BeforeUpdateStep();
        step = Math.Min(circles.Count, step + 1);
        AfterUpdateStep();
    }

    public void Previous()
    {
        BeforeUpdateStep();
        step = Math.Max(0, step - 1);
        AfterUpdateStep();
    }

    public void GoToStep(int newStep)
    {
        BeforeUpdateStep();
        step = newStep;
        AfterUpdateStep();
    }

    private void BeforeUpdateStep()
    {
        circles[step].GetComponent<Image>().color = circleDefaultColor;
        tutorialSteps[step].SetActive(false);
    }

    private void AfterUpdateStep()
    {
        circles[step].GetComponent<Image>().color = circleSelectedColor;
        tutorialSteps[step].SetActive(true);
    }
}
