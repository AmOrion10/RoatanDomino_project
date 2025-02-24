using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SliderScript : MonoBehaviour
{
    public Slider slider;
    public TMP_Text percentageText;

    void Update()
    {
        float percentageFilled = slider.value / slider.maxValue * 100f;
        percentageText.text = percentageFilled.ToString("F0") + "%";
    }
}
