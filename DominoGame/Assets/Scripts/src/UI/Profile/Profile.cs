using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

public class Profile : MonoBehaviour
{
    [SerializeField] private List<Button> buttons;
    [SerializeField] private List<GameObject> panels;

    private void Start()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            Button button = buttons[i];
            GameObject panel = panels[i];
            button.onClick.AddListener(() => ButtonClicked(button, panel));
        }
    }
    
    private void ButtonClicked(Button buttonClicked, GameObject panelAssociated)
    {
        buttonClicked.interactable = false;
        foreach (Button button in buttons)
        {
            if (button != buttonClicked)
            {
                button.interactable = true;
            }
        }

        panelAssociated.SetActive(true);
        foreach (GameObject panel in panels)
        {
            if (panel != panelAssociated)
            {
                panel.SetActive(false);
            }
        }
    }
}
