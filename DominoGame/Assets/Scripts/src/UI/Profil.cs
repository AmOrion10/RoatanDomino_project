using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

public class Profil : MonoBehaviour
{
    [SerializeField] private List<Button> navigationButtons;
    [SerializeField] private List<GameObject> navigationPanels;

    private IClient client;

    private void Start()
    {
        client = Networking.Instance.GetClient();

        for (int i = 0; i < navigationButtons.Count; i++)
        {
            Button button = navigationButtons[i];
            GameObject panel = navigationPanels[i];
            button.onClick.AddListener(() => ButtonNavigationClicked(button, panel));
        }
    }
    
    private void ButtonNavigationClicked(Button buttonClicked, GameObject panelAssociated)
    {
        buttonClicked.interactable = false;
        foreach (Button button in navigationButtons)
        {
            if (button != buttonClicked)
            {
                button.interactable = true;
            }
        }

        panelAssociated.SetActive(true);
        foreach (GameObject panel in navigationPanels)
        {
            if (panel != panelAssociated)
            {
                panel.SetActive(false);
            }
        }
    }
}
